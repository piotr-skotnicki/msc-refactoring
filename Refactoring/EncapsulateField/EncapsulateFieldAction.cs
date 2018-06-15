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
    class EncapsulateFieldAction : ICodeAction
    {
        IDocument document;
        ISymbol fieldSymbol;
        FieldDeclarationSyntax fieldDeclaration;
        VariableDeclaratorSyntax variableDeclarator;

        public EncapsulateFieldAction(IDocument document, ISymbol fieldSymbol, FieldDeclarationSyntax fieldDeclaration, VariableDeclaratorSyntax variableDeclarator)
        {
            this.document = document;
            this.fieldSymbol = fieldSymbol;
            this.fieldDeclaration = fieldDeclaration;
            this.variableDeclarator = variableDeclarator;
        }

        public string Description
        {
            get
            {
                return String.Format("Encapsulate field `{0}'", this.fieldSymbol.Name);
            }
        }

        public CodeActionEdit GetEdit(CancellationToken cancellationToken)
        {
            SyntaxNode root = (SyntaxNode)this.document.GetSyntaxRoot(cancellationToken);
            ISemanticModel model = this.document.GetSemanticModel(cancellationToken);

            string getterName = "GetField";

            ClassDeclarationSyntax typeDeclaration = this.fieldDeclaration.FirstAncestorOrSelf<ClassDeclarationSyntax>();

            bool isStatic = this.fieldDeclaration.Modifiers.Any(n => n.Kind == SyntaxKind.StaticKeyword);

            EncapsulateFieldRewriter visitor = new EncapsulateFieldRewriter(model, cancellationToken, this.fieldSymbol, typeDeclaration, this.fieldDeclaration, this.variableDeclarator, isStatic, getterName);
            SyntaxNode newRoot = visitor.Visit(root);

            return new CodeActionEdit(document.UpdateSyntaxRoot(newRoot));
        }

        public ImageSource Icon
        {
            get { return null; }
        }

        class EncapsulateFieldRewriter : SyntaxRewriter
        {
            ISemanticModel model;
            CancellationToken cancellationToken;
            ISymbol fieldSymbol;
            TypeDeclarationSyntax typeDeclaration;
            FieldDeclarationSyntax fieldDeclaration;
            VariableDeclaratorSyntax variableDeclarator;
            bool isStatic;
            string getterName;

            public EncapsulateFieldRewriter(ISemanticModel model, CancellationToken cancellationToken, ISymbol fieldSymbol, ClassDeclarationSyntax typeDeclaration, FieldDeclarationSyntax fieldDeclaration, VariableDeclaratorSyntax variableDeclarator, bool isStatic, string getterName)
            {
                this.model = model;
                this.cancellationToken = cancellationToken;
                this.fieldSymbol = fieldSymbol;
                this.typeDeclaration = typeDeclaration;
                this.fieldDeclaration = fieldDeclaration;
                this.variableDeclarator = variableDeclarator;
                this.isStatic = isStatic;
                this.getterName = getterName;
            }

            public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
            {
                ISymbol referencedSymbol = this.model.GetSymbolInfo(node, this.cancellationToken).Symbol;
                if (referencedSymbol != null && referencedSymbol.Equals(this.fieldSymbol))
                {
                    return Syntax.IdentifierName(this.getterName)
                                 .WithLeadingTrivia(node.GetLeadingTrivia())
                                 .WithTrailingTrivia(node.GetTrailingTrivia());
                }
                return base.VisitIdentifierName(node);
            }

            public override SyntaxNode VisitFieldDeclaration(FieldDeclarationSyntax node)
            {
                FieldDeclarationSyntax processedNode = (FieldDeclarationSyntax)base.VisitFieldDeclaration(node);

                if (node.Equals(this.fieldDeclaration))
                {
                    // Replace all access modifiers with private
                    SyntaxTokenList fieldModifiers = Syntax.TokenList(processedNode.Modifiers.Where(n => !IsAccessModifier(n)))
                                                           .Add(Syntax.Token(SyntaxKind.PrivateKeyword));

                    processedNode = processedNode.WithModifiers(fieldModifiers)
                                                 .WithAdditionalAnnotations(CodeAnnotations.Formatting);
                }

                return processedNode;
            }

            public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
            {
                ClassDeclarationSyntax processedNode = (ClassDeclarationSyntax)base.VisitClassDeclaration(node);

                if (node.Equals(this.typeDeclaration))
                {
                    SyntaxTokenList getterModifiers = Syntax.TokenList(Syntax.Token(SyntaxKind.PublicKeyword));

                    if (isStatic)
                    {
                        getterModifiers = getterModifiers.Add(Syntax.Token(SyntaxKind.StaticKeyword));
                    }

                    // Create property
                    PropertyDeclarationSyntax propertyDeclaration = Syntax.PropertyDeclaration(this.fieldDeclaration.Declaration.Type, this.getterName)
                                                                        .WithModifiers(getterModifiers)
                                                                        .WithTrailingTrivia(Syntax.EndOfLine(""))
                                                                        .WithAccessorList(
                                                                           Syntax.AccessorList(Syntax.List<AccessorDeclarationSyntax>(
                                                                               Syntax.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                                                                                     .WithBody(
                                                                                        Syntax.Block(Syntax.ReturnStatement(Syntax.IdentifierName(this.variableDeclarator.Identifier)))
                                                                                     ),
                                                                                Syntax.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                                                                                     .WithBody(
                                                                                        Syntax.Block(
                                                                                          Syntax.ExpressionStatement(
                                                                                            Syntax.BinaryExpression(
                                                                                              SyntaxKind.AssignExpression,
                                                                                              Syntax.IdentifierName(this.variableDeclarator.Identifier),
                                                                                              Syntax.IdentifierName("value"))
                                                                                          ) 
                                                                                        ) 
                                                                                     )
                                                                           )))
                                                                        .WithAdditionalAnnotations(CodeAnnotations.Formatting);
                    
                    ClassDeclarationSyntax newTypeDeclaration = processedNode.AddMembers(propertyDeclaration);
                    return newTypeDeclaration;
                }

                return processedNode;
            }

            bool IsAccessModifier(SyntaxToken modifier)
            {
                return modifier.Kind == SyntaxKind.PublicKeyword
                        || modifier.Kind == SyntaxKind.ProtectedKeyword
                        || modifier.Kind == SyntaxKind.PrivateKeyword
                        || modifier.Kind == SyntaxKind.InternalKeyword;
            }
        }
    }
}
