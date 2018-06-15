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
    class MakeMethodStaticAction : ICodeAction
    {
        IDocument document;
        MethodDeclarationSyntax methodDeclaration;

        public MakeMethodStaticAction(IDocument document, MethodDeclarationSyntax methodDeclaration)
        {
            this.document = document;
            this.methodDeclaration = methodDeclaration;
        }

        public string Description
        {
            get
            {
                return String.Format("Make method static");
            }
        }

        public CodeActionEdit GetEdit(CancellationToken cancellationToken)
        {
            SyntaxNode root = (SyntaxNode)this.document.GetSyntaxRoot(cancellationToken);
            ISemanticModel model = this.document.GetSemanticModel(cancellationToken);

            MethodSymbol methodSymbol = model.GetDeclaredSymbol(this.methodDeclaration, cancellationToken) as MethodSymbol;

            if (methodSymbol == null)
            {
                return null;
            }

            MakeMethodStaticRewriter visitor = new MakeMethodStaticRewriter(model, methodSymbol, cancellationToken);
            SyntaxNode newRoot = visitor.Visit(root);
            
            return new CodeActionEdit(document.UpdateSyntaxRoot(newRoot));
        }
        
        public ImageSource Icon
        {
            get { return null; }
        }
        
        // Converts method to be static
        class MakeMethodStaticRewriter : SyntaxRewriter
        {
            ISemanticModel model;            
            MethodSymbol methodSymbol;
            CancellationToken cancellationToken;

            public MakeMethodStaticRewriter(ISemanticModel model, MethodSymbol methodSymbol, CancellationToken cancellationToken)
            {
                this.model = model;
                this.methodSymbol = methodSymbol;
                this.cancellationToken = cancellationToken;
            }

            public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
            {
                MethodSymbol visitedMethodSymbol = model.GetDeclaredSymbol(node, this.cancellationToken) as MethodSymbol;

                // If this is the rewritten method, redeclare it to be static, and replace class references with new parameter within body
                if (visitedMethodSymbol != null && visitedMethodSymbol.Equals(this.methodSymbol))
                {
                    // Get type that the method is declared in
                    NamedTypeSymbol containingType = this.methodSymbol.ContainingType;

                    // Add whitespace before first modifier
                    SyntaxToken firstToken = node.GetFirstToken();
                    SyntaxTriviaList leadingTrivia = firstToken.LeadingTrivia;
                    MethodDeclarationSyntax newMethodDeclaration = node.ReplaceToken(firstToken, firstToken.WithLeadingTrivia(Syntax.Whitespace(" ")));

                    // Add `static' modifier keyword at the first place
                    SyntaxToken staticKeyword = Syntax.Token(SyntaxKind.StaticKeyword)
                                                .WithLeadingTrivia(leadingTrivia);

                    newMethodDeclaration = newMethodDeclaration
                                                .WithModifiers(newMethodDeclaration.Modifiers.Insert(0, staticKeyword));

                    // Introduce new parameter of method's container type 
                    SyntaxToken newParameterNameToken = Syntax.Identifier("self");
                    IdentifierNameSyntax newParameterName = Syntax.IdentifierName(newParameterNameToken);

                    TypeSyntax containingTypeSyntax = Syntax.ParseTypeName(containingType.ToMinimalDisplayString(node.ParameterList.GetLocation(), model));

                    ParameterSyntax newParameterNode = Syntax.Parameter(newParameterNameToken)
                                                        .WithType(containingTypeSyntax);

                    SeparatedSyntaxList<ParameterSyntax> parametersList = newMethodDeclaration.ParameterList.Parameters.Insert(0, newParameterNode);
                    ParameterListSyntax parameters = Syntax.ParameterList(parametersList).WithAdditionalAnnotations(CodeAnnotations.Formatting);

                    newMethodDeclaration = newMethodDeclaration.WithParameterList(parameters);

                    // Process the methods body to update all references to itself and containing type's members
                    NameQualifier nameQualifier = new NameQualifier(model, containingType, methodSymbol, newParameterNameToken, cancellationToken);
                    BlockSyntax processedBody = (BlockSyntax)nameQualifier.Visit(node.Body);

                    // Replace the body with the processed one
                    newMethodDeclaration = newMethodDeclaration.WithBody(processedBody);

                    return newMethodDeclaration;
                }
                else
                {
                    // Otherwise continue with visiting its children, to replace methods invocations
                    return base.VisitMethodDeclaration(node);
                }
            }

            public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
            {
                ExpressionSyntax expression = node.Expression;

                CommonSymbolInfo methodSymbolInfo = this.model.GetSymbolInfo(expression);

                MethodSymbol visitedMethodSymbol = methodSymbolInfo.Symbol as MethodSymbol;

                // Verify does the invocation refer to searched method
                if (visitedMethodSymbol != null && visitedMethodSymbol.Equals(this.methodSymbol))
                {
                    if (expression.Kind == SyntaxKind.MemberAccessExpression)
                    {
                        MemberAccessExpressionSyntax memberAccess = (MemberAccessExpressionSyntax)expression;

                        // Move all from the beginning (except for the method name) to parameter
                        // Considers:
                        // a.b.foo(47); -> B.foo(a.b, 47);
                        ArgumentListSyntax argumentsList = node.ArgumentList;

                        ArgumentSyntax newArgument = Syntax.Argument(memberAccess.Expression);

                        SeparatedSyntaxList<ArgumentSyntax> arguments = argumentsList.Arguments.Insert(0, newArgument);
                        argumentsList = Syntax.ArgumentList(arguments).WithAdditionalAnnotations(CodeAnnotations.Formatting);

                        InvocationExpressionSyntax newInvocation = node.WithArgumentList(argumentsList);

                        // Replace invocation expression with qualified (class) name
                        TypeSyntax typeSyntax = Syntax.ParseTypeName(this.methodSymbol.ContainingType.ToMinimalDisplayString(expression.GetLocation(), this.model))
                                                   .WithLeadingTrivia(memberAccess.Expression.GetLeadingTrivia());

                        newInvocation = newInvocation.WithExpression(Syntax.MemberAccessExpression(SyntaxKind.MemberAccessExpression, typeSyntax, memberAccess.Name));
                        return newInvocation;
                    }
                    else if (expression.Kind == SyntaxKind.IdentifierName)
                    {
                        IdentifierNameSyntax identifierName = (IdentifierNameSyntax)expression;

                        // Unqualified reference, `this' should be passed as first parameter
                        // Considers:
                        // foo(47); -> B.foo(this, 47);
                        ArgumentListSyntax argumentsList = node.ArgumentList;

                        ArgumentSyntax newArgument = Syntax.Argument(Syntax.ThisExpression());

                        SeparatedSyntaxList<ArgumentSyntax> arguments = argumentsList.Arguments.Insert(0, newArgument);
                        argumentsList = Syntax.ArgumentList(arguments).WithAdditionalAnnotations(CodeAnnotations.Formatting);

                        InvocationExpressionSyntax newInvocation = node.WithArgumentList(argumentsList);

                        // Replace invocation expression with qualified (class) name
                        TypeSyntax typeSyntax = Syntax.ParseTypeName(this.methodSymbol.ContainingType.ToMinimalDisplayString(expression.GetLocation(), this.model))
                                                   .WithLeadingTrivia(identifierName.GetLeadingTrivia());

                        newInvocation = newInvocation.WithExpression(Syntax.MemberAccessExpression(SyntaxKind.MemberAccessExpression, typeSyntax, identifierName));
                        return newInvocation;
                    }
                }

                return base.VisitInvocationExpression(node);
            }
        }

        // Qualifies access to non-static fields within rewritten method
        class NameQualifier : SyntaxRewriter
        {
            ISemanticModel model;
            NamedTypeSymbol containingType;
            MethodSymbol methodSymbol;
            SyntaxToken newParameterNameToken;
            CancellationToken cancellationToken;

            public NameQualifier(ISemanticModel model, NamedTypeSymbol containingType, MethodSymbol methodSymbol, SyntaxToken newParameterNameToken, CancellationToken cancellationToken)
            {
                this.model = model;
                this.containingType = containingType;
                this.methodSymbol = methodSymbol;
                this.newParameterNameToken = newParameterNameToken;
                this.cancellationToken = cancellationToken;
            }

            public override SyntaxNode VisitThisExpression(ThisExpressionSyntax node)
            {
                // Considers:
                // this.a.b.c -> newParameter.a.b.c
                return Syntax.IdentifierName(this.newParameterNameToken)
                                .WithLeadingTrivia(node.GetLeadingTrivia())
                                .WithTrailingTrivia(node.GetTrailingTrivia());
            }

            public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
            {
                ISymbol referencedSymbol = this.model.GetSymbolInfo(node, this.cancellationToken).Symbol;
                
                // Static members don't have to be qualified
                if (referencedSymbol == null || referencedSymbol.IsStatic)
                {
                    return base.VisitIdentifierName(node);
                }
                
                // Check is the name already qualified. If so, no further action is needed (only top-most identifier may be qualified)
                // Considers:
                // a.>b<.foo() -> a.b.foo()
                if (node.Ancestors().OfType<MemberAccessExpressionSyntax>().Any(n => n.Name.DescendantNodesAndSelf().Contains(node)))
                {
                    return base.VisitIdentifierName(node);
                }

                // Considers:
                // >a<.b.foo() -> newParameter.a.b.foo()
                if (referencedSymbol.Kind == CommonSymbolKind.Field
                    || referencedSymbol.Kind == CommonSymbolKind.Method
                    || referencedSymbol.Kind == CommonSymbolKind.Property
                    || referencedSymbol.Kind == CommonSymbolKind.Event)
                {
                    // Special case: constructor of different type is an unqualified and non-static member
                    if (referencedSymbol.Kind == CommonSymbolKind.Method)
                    {
                        MethodSymbol method = (MethodSymbol)referencedSymbol;
                        if (method.MethodKind == MethodKind.Constructor)
                        {
                            return base.VisitIdentifierName(node);
                        }
                    }

                    // Create new context with leading trivia of old context
                    IdentifierNameSyntax parameterIdentifier = Syntax.IdentifierName(this.newParameterNameToken)
                                                                    .WithLeadingTrivia(node.GetLeadingTrivia());

                    // And remove leading trivia from old context
                    IdentifierNameSyntax context = node.WithLeadingTrivia();

                    return Syntax.MemberAccessExpression(SyntaxKind.MemberAccessExpression, parameterIdentifier, context);
                }

                return base.VisitIdentifierName(node);
            }

            public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
            {
                ISymbol referencedSymbol = this.model.GetSymbolInfo(node).Symbol;

                // If this is a recursive invocation, then it must not be qualified with parameter name (statics are not referenced via instance access)
                // But the context shall be moved to parameter
                // Considers:
                // this.foo() -> (intermediate form from VisitThis) parameterName.foo() -> foo(parameterName)
                // a.b.c.foo() -> foo(a.b.c);
                // foo() -> (intermediate form from VisitIdentifier) parameterName.foo() -> foo(parameterName)

                // Is this a recursion
                if (referencedSymbol.Equals(this.methodSymbol))
                {
                    // Do other stuff with this node (VisitThis and VisitIdentifier) as described above
                    InvocationExpressionSyntax processedInvocation = (InvocationExpressionSyntax)base.VisitInvocationExpression(node);

                    ExpressionSyntax expression = processedInvocation.Expression;

                    // After processing it must be member access expression for sure
                    // Otherwise something is wrong
                    System.Diagnostics.Debug.Assert(expression.Kind == SyntaxKind.MemberAccessExpression);

                    MemberAccessExpressionSyntax memberAccess = (MemberAccessExpressionSyntax)expression;

                    // Move all from the beginning (except for the method name) to parameter
                    // Considers:
                    // a.b.foo(47); -> foo(a.b, 47);
                    ArgumentListSyntax argumentsList = processedInvocation.ArgumentList;

                    ArgumentSyntax newArgument = Syntax.Argument(memberAccess.Expression);

                    SeparatedSyntaxList<ArgumentSyntax> arguments = argumentsList.Arguments.Insert(0, newArgument);
                    argumentsList = Syntax.ArgumentList(arguments).WithAdditionalAnnotations(CodeAnnotations.Formatting);

                    processedInvocation = processedInvocation.WithArgumentList(argumentsList);

                    // Replace invocation expression (as it refers to static recursive method)
                    // That is, leave only method name and copy leading trivia from a.b
                    // Considers:
                    // a.b.foo -> foo
                    processedInvocation = processedInvocation.WithExpression(memberAccess.Name.WithLeadingTrivia(memberAccess.Expression.GetLeadingTrivia()));

                    return processedInvocation;
                }
                else
                {
                    return base.VisitInvocationExpression(node);
                }
            }
        }
    }
}
