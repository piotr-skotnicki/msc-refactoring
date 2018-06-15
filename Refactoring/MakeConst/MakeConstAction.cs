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
    class MakeConstAction : ICodeAction
    {
        IDocument document;
        LocalDeclarationStatementSyntax declarationNode;

        public MakeConstAction(IDocument document, LocalDeclarationStatementSyntax declarationNode)
        {
            this.document = document;
            this.declarationNode = declarationNode;
        }

        public string Description
        {
            get
            {
                return String.Format("Mark variables with `const'");
            }
        }

        public CodeActionEdit GetEdit(CancellationToken cancellationToken)
        {
            SyntaxNode root = (SyntaxNode)this.document.GetSyntaxRoot(cancellationToken);
            ISemanticModel model = this.document.GetSemanticModel(cancellationToken);

            // Copy leading trivia from the first token of declaration node
            SyntaxToken firstToken = this.declarationNode.GetFirstToken();

            SyntaxTriviaList leadingTrivia = firstToken.LeadingTrivia;

            LocalDeclarationStatementSyntax newDeclaration = this.declarationNode.ReplaceToken(firstToken, firstToken.WithLeadingTrivia(Syntax.Whitespace(" ")));

            SyntaxToken constKeyword = Syntax.Token(SyntaxKind.ConstKeyword)
                                        .WithLeadingTrivia(leadingTrivia);

            // Add `const' modifier keyword
            newDeclaration = newDeclaration
                                .WithModifiers(newDeclaration.Modifiers.Insert(0, constKeyword))
                                .WithAdditionalAnnotations(CodeAnnotations.Formatting);

            SyntaxNode newRoot = root.ReplaceNode(this.declarationNode, newDeclaration);
            
            return new CodeActionEdit(document.UpdateSyntaxRoot(newRoot));
        }

        public ImageSource Icon
        {
            get { return null; }
        }
    }
}
