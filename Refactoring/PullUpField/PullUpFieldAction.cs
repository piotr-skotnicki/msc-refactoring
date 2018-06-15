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
    class PullUpFieldAction : ICodeAction
    {
        IDocument document;
        private FieldDeclarationSyntax fieldDeclaration;
        private TypeDeclarationSyntax containingTypeDeclaration;
        private BaseTypeDeclarationSyntax baseTypeDeclaration;

        public PullUpFieldAction(IDocument document, FieldDeclarationSyntax fieldDeclaration, TypeDeclarationSyntax containingTypeDeclaration, BaseTypeDeclarationSyntax baseTypeDeclaration)
        {
            this.document = document;
            this.fieldDeclaration = fieldDeclaration;
            this.containingTypeDeclaration = containingTypeDeclaration;
            this.baseTypeDeclaration = baseTypeDeclaration;
        }

        public string Description
        {
            get
            {
                return String.Format("Move field to the super class `{0}'", containingTypeDeclaration.Identifier.ValueText);
            }
        }

        public CodeActionEdit GetEdit(CancellationToken cancellationToken)
        {
            SyntaxNode root = (SyntaxNode)this.document.GetSyntaxRoot(cancellationToken);
            ISemanticModel model = this.document.GetSemanticModel(cancellationToken);

            // Change accessibility if needed.
            // Considers:
            // private -> protected
            // protected -> protected
            // internal protected -> internal protected
            // internal -> internal
            // public -> public
            FieldDeclarationSyntax newFieldDeclaration = this.fieldDeclaration.ReplaceToken(
                                                                            Syntax.Token(SyntaxKind.PrivateKeyword),
                                                                            Syntax.Token(SyntaxKind.ProtectedKeyword));

            ISolution solution = this.document.Project.Solution;

            if (solution == null)
            {
                return null;
            }

            // Find document instance where the base type is declared
            IDocument baseTypeDocument = solution.GetDocument(this.baseTypeDeclaration.SyntaxTree);

            if (baseTypeDocument == null)
            {
                return null;
            }

            PullUpFieldRewriter visitor = new PullUpFieldRewriter(model, this.fieldDeclaration, newFieldDeclaration, this.containingTypeDeclaration, this.baseTypeDeclaration);
            SyntaxNode newRoot = visitor.Visit(root);
            ISolution updatedSolution = this.document.Project.Solution.UpdateDocument(this.document.Id, newRoot);

            // If base type is declared in other document, syntax rewriter is executed also for this document
            if (baseTypeDocument.Id != this.document.Id)
            {
                PullUpFieldRewriter baseVisitor = new PullUpFieldRewriter(model, this.fieldDeclaration, newFieldDeclaration, this.containingTypeDeclaration, this.baseTypeDeclaration);
                SyntaxNode newBaseRoot = baseVisitor.Visit((SyntaxNode)baseTypeDocument.GetSyntaxRoot(cancellationToken));
                updatedSolution = updatedSolution.UpdateDocument(baseTypeDocument.Id, newBaseRoot);
            }

            return new CodeActionEdit(updatedSolution);
        }

        public ImageSource Icon
        {
            get { return null; }
        }

        class PullUpFieldRewriter : SyntaxRewriter
        {
            ISemanticModel model;
            FieldDeclarationSyntax fieldDeclaration;
            FieldDeclarationSyntax newFieldDeclaration;
            BaseTypeDeclarationSyntax containingTypeDeclaration;
            BaseTypeDeclarationSyntax baseTypeDeclaration;

            public PullUpFieldRewriter(ISemanticModel model, FieldDeclarationSyntax fieldDeclaration, FieldDeclarationSyntax newFieldDeclaration, BaseTypeDeclarationSyntax containingTypeDeclaration, BaseTypeDeclarationSyntax baseTypeDeclaration)
            {
                this.model = model;
                this.fieldDeclaration = fieldDeclaration;
                this.newFieldDeclaration = newFieldDeclaration;
                this.containingTypeDeclaration = containingTypeDeclaration;
                this.baseTypeDeclaration = baseTypeDeclaration;
            }

            public override SyntaxNode VisitFieldDeclaration(FieldDeclarationSyntax node)
            {
                if (node.Equals(this.fieldDeclaration))
                {
                    // Remove field declaration from current location
                    return null;
                }
                return base.VisitFieldDeclaration(node);
            }

            public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
            {
                ClassDeclarationSyntax processedNode = (ClassDeclarationSyntax)base.VisitClassDeclaration(node);
                if (node.Equals(this.baseTypeDeclaration))
                {
                    // Insert field in base type declaration
                    ClassDeclarationSyntax newClassDeclaration = processedNode.AddMembers(this.newFieldDeclaration);
                    return newClassDeclaration;
                }
                return processedNode;
            }
        }
    }
}
