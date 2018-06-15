using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Media;
using Roslyn.Compilers;
using Roslyn.Compilers.Common;
using Roslyn.Compilers.CSharp;
using Roslyn.Services;
using Roslyn.Services.Editor;

namespace Refactoring
{
    class InlineMethodAction : ICodeAction
    {
        IDocument document;
        InvocationExpressionSyntax invocationExpression;
        MethodDeclarationSyntax methodDeclaration;
        ExpressionSyntax returnExpression;

        public InlineMethodAction(IDocument document, InvocationExpressionSyntax invocationExpression, MethodDeclarationSyntax methodDeclaration, ExpressionSyntax returnExpression)
        {
            this.document = document;
            this.invocationExpression = invocationExpression;
            this.methodDeclaration = methodDeclaration;
            this.returnExpression = returnExpression;
        }

        public string Description
        {
            get
            {
                return String.Format("Inline method `{0}'", methodDeclaration.Identifier.ValueText);
            }
        }

        public CodeActionEdit GetEdit(CancellationToken cancellationToken)
        {
            SyntaxNode root = (SyntaxNode)this.document.GetSyntaxRoot(cancellationToken);
            ISemanticModel model = this.document.GetSemanticModel(cancellationToken);
            
            // TODO: If method uses class fields, and its name is in target context (i.e. local variable or parameter), it should be qualified with `this.' to eliminate ambiguity

            // Casting rationale
            // Consider:
            // double foo(float a, int b) { return a + b + 4; }
            // foo(1 + 2, 3) -> (double)((float)(1 + 2) + 3 + 4)

            Dictionary<ParameterSymbol, ExpressionSyntax> map = new Dictionary<ParameterSymbol, ExpressionSyntax>();
            Dictionary<ParameterSymbol, TypeSyntax> mapParameterToTypeSyntax = new Dictionary<ParameterSymbol, TypeSyntax>();

            int parametersCount = this.methodDeclaration.ParameterList.Parameters.Count;
            int argumentsCount = this.invocationExpression.ArgumentList.Arguments.Count;

            // Map each argument to related parameter
            // Also, map each ParameterSymbol to declared TypeSyntax, so in case of GenericType it can be visited to replace TypeParameters to actual types
            // TODO: mapParameterToTypeSyntax not used
            for (int i = 0; i < argumentsCount; ++i)
            {
                ArgumentSyntax argument = this.invocationExpression.ArgumentList.Arguments[i];
                if (argument.NameColon != null)
                {
                    // This is named argument, we obtain symbol based on its identifier
                    ParameterSymbol parameterSymbol = (ParameterSymbol)model.GetSymbolInfo(argument.NameColon.Identifier, cancellationToken).Symbol;
                    map.Add(parameterSymbol, argument.Expression);
                    mapParameterToTypeSyntax.Add(parameterSymbol, this.methodDeclaration.ParameterList.Parameters[i].Type);
                }
                else
                {
                    // This is not named argument, we obtain symbol based on its position on the list
                    ParameterSymbol parameterSymbol = (ParameterSymbol)model.GetDeclaredSymbol(this.methodDeclaration.ParameterList.Parameters[i], cancellationToken);
                    map.Add(parameterSymbol, argument.Expression);
                    mapParameterToTypeSyntax.Add(parameterSymbol, this.methodDeclaration.ParameterList.Parameters[i].Type);
                }
            }

            // If not all arguments specified and still no diagnostics reported, then default values of parameters are used
            // Note: I need to filter out the parameters that has not been specified, I don't know which positions are not used due to possibility of NameColon usage
            if (argumentsCount < parametersCount)
            {
                foreach (var parameter in this.methodDeclaration.ParameterList.Parameters)
                {
                    ParameterSymbol parameterSymbol = (ParameterSymbol)model.GetDeclaredSymbol(parameter, cancellationToken);
                    // Verify is the parameter not already mapped to an expression
                    if (!map.ContainsKey(parameterSymbol))
                    {
                        // Map parameter to its default value
                        map.Add(parameterSymbol, parameter.Default.Value);
                    }
                }
            }

            // If method is a template, map also type parameters to actual type symbols
            // Note: Symbol is obtained from invocation expression so that type parameters are specified
            IDictionary<TypeParameterSymbol, TypeSymbol> mapTypeParameters = new Dictionary<TypeParameterSymbol, TypeSymbol>();
            MethodSymbol methodSymbol = (MethodSymbol)model.GetSymbolInfo(this.invocationExpression, cancellationToken).Symbol;
            ReadOnlyArray<TypeParameterSymbol> typeParameters = methodSymbol.TypeParameters;
            ReadOnlyArray<TypeSymbol> typeArguments = methodSymbol.TypeArguments;
            for (int i = 0; i < typeParameters.Count; ++i)
            {
                mapTypeParameters.Add(typeParameters[i], typeArguments[i]);
            }

            InlineMethodRewriter visitor = new InlineMethodRewriter(model, cancellationToken, map, mapTypeParameters);
            ExpressionSyntax newExpression = (ExpressionSyntax)visitor.Visit(this.returnExpression);

            // If resultant type of expression is different than method's return type, additional cast is needed
            ITypeSymbol returnType = model.GetTypeInfo(this.methodDeclaration.ReturnType, cancellationToken).Type;
            ITypeSymbol returnExpressionType = model.GetTypeInfo(this.returnExpression, cancellationToken).Type;
            bool castExpression = false;

            if (!returnExpressionType.Equals(returnType))
            {
                castExpression = true;
            }

            if (newExpression.Kind != SyntaxKind.ParenthesizedExpression)
            {
                // Surround with parenthesis if required
                SyntaxNode parentNode = this.invocationExpression.Parent;
                if ((newExpression is BinaryExpressionSyntax || newExpression is ConditionalExpressionSyntax)
                    && (parentNode is BinaryExpressionSyntax || castExpression || parentNode.Kind == SyntaxKind.MemberAccessExpression)
                    )
                {
                    newExpression = Syntax.ParenthesizedExpression(newExpression);
                }
            }

            if (castExpression)
            {
                TypeSyntax returnTypeSyntax = Syntax.ParseTypeName(returnType.ToMinimalDisplayString(this.invocationExpression.GetLocation(), model));
                newExpression = Syntax.CastExpression(returnTypeSyntax, newExpression);
            }

            // Copy leading and trailing trivia from method's invocation expression
            newExpression = newExpression.WithLeadingTrivia(this.invocationExpression.GetLeadingTrivia())
                                         .WithTrailingTrivia(this.invocationExpression.GetTrailingTrivia());

            SyntaxNode newRoot = root.ReplaceNode(this.invocationExpression, newExpression);

            return new CodeActionEdit(document.UpdateSyntaxRoot(newRoot));
        }

        public ImageSource Icon
        {
            get { return null; }
        }

        class InlineMethodRewriter : SyntaxRewriter
        {
            ISemanticModel model;
            CancellationToken cancellationToken;
            IDictionary<ParameterSymbol, ExpressionSyntax> map;
            IDictionary<TypeParameterSymbol, TypeSymbol> mapTypeParameters;

            public InlineMethodRewriter(ISemanticModel model, CancellationToken cancellationToken, IDictionary<ParameterSymbol, ExpressionSyntax> map, IDictionary<TypeParameterSymbol, TypeSymbol> mapTypeParameters)
            {
                this.model = model;
                this.cancellationToken = cancellationToken;
                this.map = map;
                this.mapTypeParameters = mapTypeParameters;
            }

            public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
            {
                ISymbol symbol = this.model.GetSymbolInfo(node, this.cancellationToken).Symbol;

                if (symbol.Kind == CommonSymbolKind.Parameter)
                {
                    ParameterSymbol parameterSymbol = (ParameterSymbol)symbol;

                    ExpressionSyntax mappedExpression = this.map[parameterSymbol];
                    ExpressionSyntax inlinedExpression = mappedExpression;

                    // If argument type is different than method's parameter type, additional cast is needed
                    ITypeSymbol parameterType = parameterSymbol.Type;
                    ITypeSymbol argumentType = this.model.GetTypeInfo(mappedExpression, this.cancellationToken).Type;
                    
                    // TODO: If this is generic type, i.e. IList<T>, visit its identifiers
                    if (parameterType.Kind == CommonSymbolKind.NamedType && ((NamedTypeSymbol)parameterType).IsGenericType)
                    {
                        
                    }

                    // If parameter type is TypeParameter (template parameter), then it is same as argument type
                    if (parameterType.Kind == CommonSymbolKind.TypeParameter)
                    {
                        parameterType = argumentType;
                    }

                    bool castInlinedExpression = false;

                    if (!parameterType.Equals(argumentType))
                    {
                        castInlinedExpression = true;
                    }
                    
                    SyntaxNode parentNode = node.Parent;

                    if (mappedExpression.Kind != SyntaxKind.ParenthesizedExpression)
                    {
                        // Surround with parenthesis if required
                        if ((mappedExpression is BinaryExpressionSyntax || mappedExpression is ConditionalExpressionSyntax)
                            && (parentNode is BinaryExpressionSyntax || castInlinedExpression || parentNode.Kind == SyntaxKind.MemberAccessExpression)
                            )
                        {
                            inlinedExpression = Syntax.ParenthesizedExpression(inlinedExpression);
                        }
                    }

                    // Cast expression to preserve type semantics if necessary
                    if (castInlinedExpression)
                    {
                        TypeSyntax typeSyntax = Syntax.ParseTypeName(parameterType.ToMinimalDisplayString(node.GetLocation(), this.model));
                        inlinedExpression = Syntax.CastExpression(typeSyntax, inlinedExpression);
                        
                        // If inlined expression is used in a context with operator of higher precedence than cast operator, then again parenthesis is needed
                        // Considers:
                        // a[0]  ->  >(< (IList<int>)a >)< [0]  , not (IList<int>)a[0]
                        // a.b   ->  >(< (A)a >)< .b            , not (A)a.b
                        // a++   ->  >(< (A)a >)< ++            , not (A)a++
                        if (parentNode.Kind == SyntaxKind.ElementAccessExpression
                            || parentNode.Kind == SyntaxKind.MemberAccessExpression
                            || parentNode.Kind == SyntaxKind.PostDecrementExpression
                            || parentNode.Kind == SyntaxKind.PostIncrementExpression)
                        {
                            inlinedExpression = Syntax.ParenthesizedExpression(inlinedExpression);
                        }
                    }

                    inlinedExpression = inlinedExpression.WithLeadingTrivia(node.GetLeadingTrivia())
                                                         .WithTrailingTrivia(node.GetTrailingTrivia());

                    return inlinedExpression;
                }
                else if (symbol.Kind == CommonSymbolKind.TypeParameter)
                {
                    // TypeParameter identifier is replaced with actual type in target scope
                    // Considers:
                    // T foo<T>() { return (T)1; }
                    // foo<int>() -> (int)1;
                    
                    TypeParameterSymbol typeParameterSymbol = (TypeParameterSymbol)symbol;

                    TypeSymbol actualTypeSymbol = mapTypeParameters[typeParameterSymbol];

                    IdentifierNameSyntax newIdentifier = Syntax.IdentifierName(actualTypeSymbol.ToMinimalDisplayString(node.GetLocation(), this.model));

                    return newIdentifier;
                }

                return base.VisitIdentifierName(node);
            }
        }
    }
}
