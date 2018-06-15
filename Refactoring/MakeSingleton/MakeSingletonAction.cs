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
    class MakeSingletonAction : ICodeAction
    {
        IDocument document;
        ISymbol symbol;
        ClassDeclarationSyntax classDeclarationNode;

        public MakeSingletonAction(IDocument document, ISymbol symbol, ClassDeclarationSyntax classDeclarationNode)
        {
            this.document = document;
            this.symbol = symbol;
            this.classDeclarationNode = classDeclarationNode;
        }

        public string Description
        {
            get
            {
                return String.Format("Convert class `{0}' to singleton", this.symbol.Name);
            }
        }

        public CodeActionEdit GetEdit(CancellationToken cancellationToken)
        {
            SyntaxNode root = (SyntaxNode)this.document.GetSyntaxRoot(cancellationToken);
            ISemanticModel model = this.document.GetSemanticModel(cancellationToken);

            MakeSingletonRewriter visitor = new MakeSingletonRewriter(model, this.symbol, cancellationToken);
            SyntaxNode newRoot = visitor.Visit(root);

            return new CodeActionEdit(document.UpdateSyntaxRoot(newRoot));
        }

        public ImageSource Icon
        {
            get { return null; }
        }

        class MakeSingletonRewriter : SyntaxRewriter
        {
            ISemanticModel model;
            ISymbol classSymbol;
            CancellationToken cancellationToken;

            public MakeSingletonRewriter(ISemanticModel model, ISymbol classSymbol, CancellationToken cancellationToken)
            {
                this.model = model;
                this.classSymbol = classSymbol;
                this.cancellationToken = cancellationToken;
            }

            public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
            {
                ISymbol visitedClassSymbol = this.model.GetDeclaredSymbol(node, this.cancellationToken);

                if (visitedClassSymbol.Equals(this.classSymbol))
                {
                    // First process the children
                    ClassDeclarationSyntax processedNode = (ClassDeclarationSyntax)base.VisitClassDeclaration(node);
                    
                    // Simplify type name
                    string typeName = this.classSymbol.ToMinimalDisplayString(node.GetLocation(), this.model);
                    
                    // TODO: Class can be parameterized, Type<A,B> etc.

                    // Create new members
                    BlockSyntax body = (BlockSyntax)Syntax.ParseStatement(String.Format(
@"
{{
    if (instance == null)
    {{
        instance = new {0}();
    }}
    return instance;
}}", typeName));     


                    MethodDeclarationSyntax methodDeclaration = Syntax.MethodDeclaration(
                                                                    Syntax.ParseTypeName(typeName)
                                                                    , "Instance")
                                                                .WithModifiers(Syntax.TokenList(
                                                                    Syntax.Token(SyntaxKind.StaticKeyword)
                                                                    , Syntax.Token(SyntaxKind.PublicKeyword)
                                                                    ))
                                                                .WithBody(body)
                                                                .WithLeadingTrivia(Syntax.EndOfLine("\r\n"))
                                                                .WithAdditionalAnnotations(CodeAnnotations.Formatting);
                                                                //.WithAdditionalAnnotations(CodeAnnotations.NameSimplification);

                    FieldDeclarationSyntax fieldDeclaration = Syntax.FieldDeclaration(
                                                                    Syntax.VariableDeclaration(
                                                                        Syntax.ParseTypeName(typeName)
                                                                        , Syntax.SeparatedList<VariableDeclaratorSyntax>(
                                                                            Syntax.VariableDeclarator("instance")
                                                                        )))
                                                                .WithModifiers(Syntax.TokenList(
                                                                    Syntax.Token(SyntaxKind.StaticKeyword)
                                                                    , Syntax.Token(SyntaxKind.PrivateKeyword)))
                                                                .WithLeadingTrivia(Syntax.EndOfLine("\r\n"))
                                                                .WithAdditionalAnnotations(CodeAnnotations.Formatting);
                                                                //.WithAdditionalAnnotations(CodeAnnotations.NameSimplification);

                    // Update class declaration with new members
                    ClassDeclarationSyntax newClassDeclaration = processedNode.AddMembers(methodDeclaration, fieldDeclaration);

                    return newClassDeclaration;
                }
                else
                {
                    return base.VisitClassDeclaration(node);
                }
            }

            public override SyntaxNode VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
            {
                TypeSyntax typeSyntax = node.Type;

                CommonSymbolInfo constructorSymbolInfo = this.model.GetSymbolInfo(typeSyntax);
                INamedTypeSymbol classTypeSymbol = constructorSymbolInfo.Symbol.ContainingType;

                if (classTypeSymbol.Equals(this.classSymbol))
                {
                    // Replace with Instance access
                    // Considers:
                    // new A() -> A.Instance()
                    return Syntax.InvocationExpression(
                                Syntax.MemberAccessExpression(
                                    SyntaxKind.MemberAccessExpression
                                    , Syntax.ParseName(this.classSymbol.ToMinimalDisplayString(node.GetLocation(), model))
                                    , Syntax.IdentifierName("Instance")
                                    )
                                )
                                .WithLeadingTrivia(node.GetLeadingTrivia())
                                .WithTrailingTrivia(node.GetTrailingTrivia());
                }
                else
                {
                    return base.VisitObjectCreationExpression(node);
                }
            }

            public override SyntaxNode VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
            {
                ConstructorDeclarationSyntax processedDeclaration = (ConstructorDeclarationSyntax)base.VisitConstructorDeclaration(node);

                // Make the constructor declaration private if it has other access modifier
                SyntaxTokenList modifiers = processedDeclaration.Modifiers;

                // Static constructors are different story
                if (modifiers.Any(SyntaxKind.StaticKeyword))
                {
                    return processedDeclaration;
                }

                // If it is already declared as private, do nothing
                if (!modifiers.Any(SyntaxKind.PrivateKeyword))
                {
                    if (modifiers.Any(SyntaxKind.PublicKeyword) || modifiers.Any(SyntaxKind.ProtectedKeyword))
                    {
                        // If there is already access modified, replace it with `private' keyword.
                        // Considers:
                        // public A() {} -> private A() {}
                        // protected A() {} -> private A() {}

                        SyntaxToken accessKeyword = processedDeclaration.GetFirstToken(t => t.Kind == SyntaxKind.PublicKeyword || t.Kind == SyntaxKind.ProtectedKeyword);
                        
                        // Copy leading and trailing trivia
                        SyntaxToken privateKeyword = Syntax.Token(SyntaxKind.PrivateKeyword)
                                                        .WithLeadingTrivia(accessKeyword.LeadingTrivia)
                                                        .WithTrailingTrivia(accessKeyword.TrailingTrivia);
                                                        
                        // Replace the access modifier
                        ConstructorDeclarationSyntax newDeclaration = processedDeclaration.ReplaceToken(accessKeyword, privateKeyword);
                        return newDeclaration;
                    }
                    else
                    {
                        // Otherwise, insert `private' at the beginning.
                        // Considers:
                        // A() {} -> private A() {}

                        // Copy trivia from first token
                        SyntaxToken firstToken = processedDeclaration.GetFirstToken();

                        // Copy leading and trailing trivia
                        SyntaxToken privateKeyword = Syntax.Token(SyntaxKind.PrivateKeyword)
                                                        .WithLeadingTrivia(firstToken.LeadingTrivia)
                                                        .WithTrailingTrivia(firstToken.TrailingTrivia);

                        // Remove the leading trivia from first token
                        ConstructorDeclarationSyntax newDeclaration = processedDeclaration.ReplaceToken(firstToken, firstToken.WithLeadingTrivia(Syntax.Whitespace(" ")));

                        // Insert the access modifier
                        newDeclaration = newDeclaration
                                .WithModifiers(newDeclaration.Modifiers.Insert(0, privateKeyword));

                        return newDeclaration;
                    }
                }

                return processedDeclaration;
            }
        }
    }
}
