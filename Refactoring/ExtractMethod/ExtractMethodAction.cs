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
    class ExtractMethodAction : ICodeAction
    {
        IDocument document;
        ExpressionSyntax expression;

        public ExtractMethodAction(IDocument document, ExpressionSyntax expression)
        {
            this.document = document;
            this.expression = expression;
        }

        public string Description
        {
            get
            {
                return String.Format("Extract method");
            }
        }

        public CodeActionEdit GetEdit(CancellationToken cancellationToken)
        {
            SyntaxNode root = (SyntaxNode)this.document.GetSyntaxRoot(cancellationToken);
            ISemanticModel model = this.document.GetSemanticModel(cancellationToken);

            // New method's name
            const string methodName = "NewMethod";

            // Get the resultant type of expression being extracted into method
            TypeSymbol typeSymbol = model.GetTypeInfo(this.expression, cancellationToken).Type as TypeSymbol;
            if (typeSymbol == null)
            {
                return null;
            }
            
            // Get the container that the new method will be stored in
            // TypeDeclarationSyntax refers to classes, structs and interfaces
            TypeDeclarationSyntax containingType = this.expression.FirstAncestorOrSelf<TypeDeclarationSyntax>();
            if (containingType == null)
            {
                return null;
            }

            TypeSyntax returnType = Syntax.ParseTypeName(typeSymbol.ToMinimalDisplayString(containingType.GetLocation(), model));

            // Create new method's body
            StatementSyntax methodStatement = null;
            if (typeSymbol.SpecialType == SpecialType.System_Void)
            {
                // If return type is void (foo();), no value is returned at all
                methodStatement = Syntax.ExpressionStatement(this.expression);
            }
            else
            {
                // Create return statement
                methodStatement = Syntax.ReturnStatement(this.expression);
            }

            BlockSyntax body = Syntax.Block(Syntax.List<StatementSyntax>(methodStatement));

            // Create method declaration
            MethodDeclarationSyntax methodDeclaration = Syntax.MethodDeclaration(returnType, methodName).WithBody(body);

            SeparatedSyntaxList<ParameterSyntax> parametersList = Syntax.SeparatedList<ParameterSyntax>();
            SeparatedSyntaxList<ArgumentSyntax> argumentsList = Syntax.SeparatedList<ArgumentSyntax>();

            // Perform data flow analysis within expression
            var analysis = model.AnalyzeExpressionDataFlow(this.expression);

            // Analyze when to use `ref' and when to use `out' (`out' variable cannot be read before assignment in method, but variable cannot be passed as `ref' if is unassigned before expression in old place)
            // If variable is read in expression it is guaranteed that is has already value assigned.
            // Also, `out' variable can be written to before used as `out' argument.
            //
            // Conclusion:
            // Use `out' if variable is first written to (no matter if already has some value).
            // Otherwise, use `ref' (because variable is first read from and already has value).
            //
            // Consider:
            // void foo(out int i) { return (i=3) + i; }, can be used for both `int i;' and `int i = 1;', but not in `i + (i=3)'
            // void foo(ref int i) { return i + (i=3); }, can be used for `int i = 1;', but not in `int i;' and `(i=3) + i'
            //
            // Also note:
            // The first occurence of identifier can be used in `foo(out identifier)' context, it is not write to operation, but MUST remain as `out' or `ref'

            // Out/Ref parameters
            foreach (var variable in analysis.WrittenInside)
            {
                // Data flow analysis is applicable only for local variables (`this.a', or even `a' if it is field, both are flattened to `this' variable)
                // And `this' is of Parameter kind
                if (variable.Kind == CommonSymbolKind.Parameter)
                {
                    // If this is `this', do not pass as argument, can be referenced directly from method scope
                    if (((ParameterSymbol)variable).IsThis)
                    {
                        continue;
                    }
                }

                // Find first identifier expression that refers to analyzed symbol
                ExpressionSyntax symbolExpression = this.expression.DescendantNodesAndSelf()
                                                                   .OfType<IdentifierNameSyntax>()
                                                                   .Where(n => variable.Equals(model.GetSymbolInfo(n, cancellationToken).Symbol))
                                                                   .FirstOrDefault();

                if (symbolExpression == null)
                {
                    // Should not happen at all
                    continue;
                }

                SyntaxToken refOrOut = Syntax.Token(SyntaxKind.RefKeyword);

                // The symbolExpression is first reference to variable (due to preorder search in DescendantNodes)
                // If it is assignment, then variable should be passed as `out'
                if (symbolExpression.Ancestors().OfType<BinaryExpressionSyntax>().Any(n => IsComplexAssignment(n) && n.Left.DescendantNodesAndSelf().Contains(symbolExpression)))
                {
                    refOrOut = Syntax.Token(SyntaxKind.OutKeyword);
                }

                // If expression is used as argument (`ref sth', or `out sth'), the keyword must remain.
                ArgumentSyntax expressionAsArgument = symbolExpression.FirstAncestorOrSelf<ArgumentSyntax>();
                if (expressionAsArgument != null)
                {
                    refOrOut = expressionAsArgument.RefOrOutKeyword;
                }

                ArgumentSyntax argument = Syntax.Argument(symbolExpression)
                                            .WithRefOrOutKeyword(refOrOut);

                argumentsList = argumentsList.Add(argument);

                // Get type of new parameter
                TypeSymbol parameterType = null;

                switch (variable.Kind)
                {
                    case CommonSymbolKind.Local:
                    {
                        LocalSymbol localSymbol = (LocalSymbol)variable;
                        parameterType = localSymbol.Type;
                    }
                    break;

                    case CommonSymbolKind.Parameter:
                    {
                        ParameterSymbol parameterSymbol = (ParameterSymbol)variable;
                        parameterType = parameterSymbol.Type;
                    }
                    break;
                }

                if (parameterType == null)
                {
                    // It can be Range type variable, used in Linq
                    continue;
                }

                // Parse type name
                TypeSyntax parameterTypeSyntax = Syntax.ParseTypeName(parameterType.ToMinimalDisplayString(containingType.GetLocation(), model));

                ParameterSyntax parameter = Syntax.Parameter(Syntax.Identifier(variable.Name))
                                                .WithType(parameterTypeSyntax)
                                                .WithModifiers(Syntax.TokenList(refOrOut));

                parametersList = parametersList.Add(parameter);
            }

            // In parameters
            foreach (var variable in analysis.ReadInside)
            {   
                // Do not pass variable as `in', if it is already `out' variable
                if (analysis.WrittenInside.Contains(variable))
                {
                    continue;
                }

                // Data flow analysis is applicable only for local variables (`this.a', or even `a' if it is field, both are flattened to `this' variable)
                // And `this' is of Parameter kind
                if (variable.Kind == CommonSymbolKind.Parameter)
                {
                    // If this is `this', do not pass as argument, can be referenced directly from method scope
                    if (((ParameterSymbol)variable).IsThis)
                    {
                        continue;
                    }
                }

                // Find first identifier expression that refers to analyzed symbol
                ExpressionSyntax symbolExpression = this.expression.DescendantNodesAndSelf()
                                                                   .OfType<IdentifierNameSyntax>()
                                                                   .Where(n => variable.Equals(model.GetSymbolInfo(n, cancellationToken).Symbol))
                                                                   .FirstOrDefault();

                if (symbolExpression == null)
                {
                    // Should not happen at all
                    continue;
                }

                // Create argument to be passed to method
                ArgumentSyntax argument = Syntax.Argument(symbolExpression);

                argumentsList = argumentsList.Add(argument);

                // Get type of new parameter
                TypeSymbol parameterType = null;

                switch (variable.Kind)
                {
                    case CommonSymbolKind.Local:
                    {
                        LocalSymbol localSymbol = (LocalSymbol)variable;
                        parameterType = localSymbol.Type;
                    }
                    break;

                    case CommonSymbolKind.Parameter:
                    {
                        ParameterSymbol parameterSymbol = (ParameterSymbol)variable;
                        parameterType = parameterSymbol.Type;
                    }
                    break;
                }

                if (parameterType == null)
                {
                    // It can be Range type variable, used in Linq
                    continue;
                }

                // Parse type name
                TypeSyntax parameterTypeSyntax = Syntax.ParseTypeName(parameterType.ToMinimalDisplayString(containingType.GetLocation(), model));

                ParameterSyntax parameter = Syntax.Parameter(Syntax.Identifier(variable.Name))
                                                .WithType(parameterTypeSyntax);

                parametersList = parametersList.Add(parameter);
            }

            // Add parameter list to method declaration
            methodDeclaration = methodDeclaration.WithParameterList(Syntax.ParameterList(parametersList))
                                               .WithLeadingTrivia(Syntax.ElasticCarriageReturnLineFeed)
                                               .WithAdditionalAnnotations(CodeAnnotations.Formatting);

            // If the expression is within static method, the extracted method should be static as well
            MemberDeclarationSyntax memberDeclaration = this.expression.FirstAncestorOrSelf<MemberDeclarationSyntax>();
            if (memberDeclaration == null)
            {
                return null;
            }

            ISymbol memberSymbol = model.GetDeclaredSymbol(memberDeclaration);

            if (memberSymbol.IsStatic)
            {
                methodDeclaration = methodDeclaration.WithModifiers(methodDeclaration.Modifiers.Add(Syntax.Token(SyntaxKind.StaticKeyword)));
            }
            
            // Format arguments' list
            ArgumentListSyntax argumentListSyntax = Syntax.ArgumentList(argumentsList)
                                                        .WithAdditionalAnnotations(CodeAnnotations.Formatting);

            // Replace selected expression with new method's invocation
            InvocationExpressionSyntax methodInvocation = Syntax.InvocationExpression(Syntax.IdentifierName(methodName))
                                                            .WithArgumentList(argumentListSyntax)
                                                            .WithLeadingTrivia(this.expression.GetLeadingTrivia())
                                                            .WithTrailingTrivia(this.expression.GetTrailingTrivia());

            // If containing method is a template, type parameters should be forwarded to new method as well,
            // together with type constraints.
            // Note: I am interested only in type parameters from MethodDeclaration. Other method-like syntax (lambdas, anonymous methods, indexers, operators) don't specify type parameters.
            // Additionally, type parameters of containing type are visible to new method, becuase it is also a member of this class
            MethodDeclarationSyntax containingMethodDeclaration = this.expression.FirstAncestorOrSelf<MethodDeclarationSyntax>();
            if (containingMethodDeclaration != null && containingMethodDeclaration.TypeParameterList != null)
            {
                methodDeclaration = methodDeclaration.WithTypeParameterList(containingMethodDeclaration.TypeParameterList)
                                                     .WithConstraintClauses(containingMethodDeclaration.ConstraintClauses);

                SeparatedSyntaxList<TypeSyntax> typeArguments = Syntax.SeparatedList<TypeSyntax>();
                foreach (TypeParameterSyntax templateArg in containingMethodDeclaration.TypeParameterList.Parameters)
                {
                    typeArguments = typeArguments.Add(Syntax.ParseTypeName(templateArg.Identifier.ValueText));
                }

                TypeArgumentListSyntax typeArgumentList = Syntax.TypeArgumentList(typeArguments);
                GenericNameSyntax genericName = Syntax.GenericName(Syntax.Identifier(methodName), typeArgumentList)
                                                     .WithAdditionalAnnotations(CodeAnnotations.Formatting);
                methodInvocation = methodInvocation.WithExpression(genericName);
            }

            TypeDeclarationSyntax newContainingType = containingType.ReplaceNode(this.expression, methodInvocation);

            // Insert method declaration to containing type
            if (containingType.Kind == SyntaxKind.ClassDeclaration)
            {
                ClassDeclarationSyntax classDeclaration = (ClassDeclarationSyntax)newContainingType;
                newContainingType = classDeclaration.AddMembers(methodDeclaration);
            }
            else if (containingType.Kind == SyntaxKind.StructDeclaration)
            {
                StructDeclarationSyntax structDeclaration = (StructDeclarationSyntax)newContainingType;
                newContainingType = structDeclaration.AddMembers(methodDeclaration);
            }
            else
            {
                return null;
            }

            SyntaxNode newRoot = root.ReplaceNode(containingType, newContainingType);

            return new CodeActionEdit(document.UpdateSyntaxRoot(newRoot));
        }

        public ImageSource Icon
        {
            get { return null; }
        }

        bool IsComplexAssignment(BinaryExpressionSyntax node)
        {
            return node.Kind == SyntaxKind.AssignExpression
                   || node.Kind == SyntaxKind.AddAssignExpression
                   || node.Kind == SyntaxKind.AndAssignExpression
                   || node.Kind == SyntaxKind.ExclusiveOrAssignExpression
                   || node.Kind == SyntaxKind.SubtractAssignExpression
                   || node.Kind == SyntaxKind.MultiplyAssignExpression
                   || node.Kind == SyntaxKind.ModuloAssignExpression
                   || node.Kind == SyntaxKind.DivideAssignExpression
                   || node.Kind == SyntaxKind.LeftShiftAssignExpression
                   || node.Kind == SyntaxKind.RightShiftAssignExpression
                   || node.Kind == SyntaxKind.OrAssignExpression;
        }
    }
}
