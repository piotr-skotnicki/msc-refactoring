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
    class ReplaceConstructorWithFactoryMethodAction : ICodeAction
    {
        IDocument document;
        ConstructorDeclarationSyntax ctorDeclaration;
        ClassDeclarationSyntax typeDeclaration;

        public ReplaceConstructorWithFactoryMethodAction(IDocument document, ConstructorDeclarationSyntax ctorDeclaration, ClassDeclarationSyntax typeDeclaration)
        {
            this.document = document;
            this.ctorDeclaration = ctorDeclaration;
            this.typeDeclaration = typeDeclaration;
        }

        public string Description
        {
            get
            {
                return String.Format("Replace constructor with factory method");
            }
        }

        public CodeActionEdit GetEdit(CancellationToken cancellationToken)
        {
            SyntaxNode root = (SyntaxNode)this.document.GetSyntaxRoot(cancellationToken);
            ISemanticModel model = this.document.GetSemanticModel(cancellationToken);

            ISymbol typeSymbol = model.GetDeclaredSymbol(this.typeDeclaration, cancellationToken);

            string typeName = typeSymbol.Name;
            string methodName = String.Format("Create{0}", typeName);

            // Note: typeSyntax will already contain template parameters, same as class definition
            TypeSyntax typeSyntax = Syntax.ParseTypeName(typeSymbol.ToMinimalDisplayString(this.ctorDeclaration.GetLocation(), model));
            
            // Filter out access modifiers
            var accessModifiers = this.ctorDeclaration.Modifiers.Where(IsAccessModifier);

            // Define modifiers, access modifier same as in constructor + static keyword
            SyntaxTokenList modifiers = Syntax.TokenList(accessModifiers)
                                              .Add(Syntax.Token(SyntaxKind.StaticKeyword));

            // Forward parameters to arguments, from ctor declaration to ctor call
            SeparatedSyntaxList<ArgumentSyntax> arguments = Syntax.SeparatedList<ArgumentSyntax>();

            foreach (ParameterSyntax parameter in this.ctorDeclaration.ParameterList.Parameters)
            {
                ArgumentSyntax argument = Syntax.Argument(Syntax.IdentifierName(parameter.Identifier));

                // Perfect forwarding
                // If parameter if `ref' or `out', it must be forwarded with same keyword
                if (parameter.Modifiers.Any(SyntaxKind.OutKeyword))
                {
                    argument = argument.WithRefOrOutKeyword(Syntax.Token(SyntaxKind.OutKeyword));
                }
                else if (parameter.Modifiers.Any(SyntaxKind.RefKeyword))
                {
                    argument = argument.WithRefOrOutKeyword(Syntax.Token(SyntaxKind.RefKeyword));
                }
                arguments = arguments.Add(argument);
            }

            // If class is a parametrized type (template), factory method should also handle those parameters
            // TODO:
            // class A<T,U> { public A(){} } -> class A<T,U> { private A(){} public static A<V,Z> CreateA<V,Z>(){...} }

            // Create object creation expression
            ObjectCreationExpressionSyntax objectCreationExpression = Syntax.ObjectCreationExpression(typeSyntax)
                                                                            .WithArgumentList(Syntax.ArgumentList(arguments));

            // Create simple factory method, return new TYPE(ARGUMENTS);
            BlockSyntax methodBody = Syntax.Block(Syntax.List<StatementSyntax>(Syntax.ReturnStatement(objectCreationExpression)));

            // Define factory method
            MethodDeclarationSyntax factoryMethod = Syntax.MethodDeclaration(typeSyntax, methodName)
                                                          .WithBody(methodBody)
                                                          .WithModifiers(modifiers)
                                                          .WithParameterList(this.ctorDeclaration.ParameterList)
                                                          .WithLeadingTrivia(Syntax.ElasticCarriageReturnLineFeed)
                                                          .WithAdditionalAnnotations(CodeAnnotations.Formatting);

            ReplaceConstructorWithFactoryMethodRewriter visitor = new ReplaceConstructorWithFactoryMethodRewriter(model, typeSymbol, cancellationToken, methodName, factoryMethod, ctorDeclaration, this.typeDeclaration);

            SyntaxNode newRoot = visitor.Visit(root);

            return new CodeActionEdit(document.UpdateSyntaxRoot(newRoot));
        }

        bool IsAccessModifier(SyntaxToken modifier)
        {
            return modifier.Kind == SyntaxKind.PublicKeyword
                    || modifier.Kind == SyntaxKind.ProtectedKeyword
                    || modifier.Kind == SyntaxKind.PrivateKeyword
                    || modifier.Kind == SyntaxKind.InternalKeyword;
        }

        public ImageSource Icon
        {
            get { return null; }
        }

        class ReplaceConstructorWithFactoryMethodRewriter : SyntaxRewriter
        {
            ISemanticModel model;
            ISymbol typeSymbol;
            CancellationToken cancellationToken;
            string factoryMethodName;
            MethodDeclarationSyntax factoryMethod;
            ConstructorDeclarationSyntax ctorDeclaration;
            ClassDeclarationSyntax typeDeclaration;

            public ReplaceConstructorWithFactoryMethodRewriter(ISemanticModel model, ISymbol typeSymbol, CancellationToken cancellationToken, string factoryMethodName, MethodDeclarationSyntax factoryMethod, ConstructorDeclarationSyntax ctorDeclaration, ClassDeclarationSyntax typeDeclaration)
            {
                this.model = model;
                this.typeSymbol = typeSymbol;
                this.cancellationToken = cancellationToken;
                this.factoryMethodName = factoryMethodName;
                this.factoryMethod = factoryMethod;
                this.ctorDeclaration = ctorDeclaration;
                this.typeDeclaration = typeDeclaration;
            }

            public override SyntaxNode VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
            {
                ConstructorDeclarationSyntax processedNode = (ConstructorDeclarationSyntax)base.VisitConstructorDeclaration(node);

                if (this.ctorDeclaration.Equals(node))
                {
                    // Change constructor's accessibility to private
                    ConstructorDeclarationSyntax newCtorDeclaration = processedNode.WithModifiers(Syntax.Token(SyntaxKind.PrivateKeyword))
                                                                                   .WithAdditionalAnnotations(CodeAnnotations.Formatting);
                    return newCtorDeclaration;
                }

                return processedNode;
            }

            public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
            {
                ClassDeclarationSyntax processedNode = (ClassDeclarationSyntax)base.VisitClassDeclaration(node);

                if (this.typeDeclaration.Equals(node))
                {
                    // Add factory method do class declaration
                    return processedNode.AddMembers(this.factoryMethod);
                }

                return processedNode;
            }

            public override SyntaxNode VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
            {
                ObjectCreationExpressionSyntax processedNode = (ObjectCreationExpressionSyntax)node;

                ITypeSymbol symbol = this.model.GetTypeInfo(node.Type, this.cancellationToken).Type;
                if (symbol != null && this.typeSymbol.Equals(symbol))
                {
                    // Replace object creation with factory method call
                    // Considers:
                    // new TYPE(ARGS) -> TYPE.FactoryMethodName(ARGS)
                    InvocationExpressionSyntax invocationExpression = Syntax.InvocationExpression(
                                                                              Syntax.MemberAccessExpression(
                                                                                      SyntaxKind.MemberAccessExpression,
                                                                                      node.Type,
                                                                                      Syntax.IdentifierName(this.factoryMethodName)))
                                                                             .WithArgumentList(processedNode.ArgumentList)
                                                                             .WithLeadingTrivia(processedNode.GetLeadingTrivia())
                                                                             .WithTrailingTrivia(processedNode.GetTrailingTrivia());

                    return invocationExpression;
                }

                return processedNode;
            }

        }
    }
}
