using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using Roslyn.Compilers;
using Roslyn.Compilers.Common;
using Roslyn.Compilers.CSharp;
using Roslyn.Services;
using Roslyn.Services.Editor;

namespace Refactoring
{
    [ExportCodeRefactoringProvider("ReverseConditional", LanguageNames.CSharp)]
    public class ReverseConditionalRefactoring : ICodeRefactoringProvider
    {
        public CodeRefactoring GetRefactoring(IDocument document, TextSpan textSpan, CancellationToken cancellationToken)
        {
            SyntaxNode root = (SyntaxNode)document.GetSyntaxRoot(cancellationToken);
            SyntaxToken token = root.FindToken(textSpan.Start, findInsideTrivia: true);

            SyntaxNode parent = token.Parent;
            if (parent == null)
            {
                return null;
            }

            // Verify is the selected an if or else token or expression within if condition
            if (((token.Kind == SyntaxKind.IfKeyword || token.Kind == SyntaxKind.ElseKeyword)
                   && token.Span.Start <= textSpan.End && textSpan.End <= token.Span.End)
                || (parent.Ancestors().OfType<IfStatementSyntax>().Where(n => n.Condition.DescendantNodesAndSelf().Contains(parent)).Count() != 0))
            {
                IfStatementSyntax ifStatement = parent.FirstAncestorOrSelf<IfStatementSyntax>();

                if (ifStatement == null)
                {
                    return null;
                }

                // If the IfStatement has some errors, then no refactoring should take place
                if (ifStatement.HasDiagnostics)
                {
                    return null;
                }

                // If ElseClause is missing, then this refactoring does not apply
                if (ifStatement.Else == null)
                {
                    return null;
                }

                // If ElseClause contains another IfStatement, then it should not have another ElseClause, because there are multiple conditions then
                // Consider:
                // if (condition) {} else if (second condition) {} else if (third condition) {}
                if (ifStatement.Else.Statement is IfStatementSyntax)
                {
                    IfStatementSyntax elseClauseIf = (IfStatementSyntax)ifStatement.Else.Statement;
                    if (elseClauseIf.Else != null)
                    {
                        return null;
                    }
                }

                // Finally, we have suitable candidate for Reverse Conditional
                // Considers:
                // (a) if (condition) {} else {}
                // (b) if (condition) {} else if (second condition) {}

                return new CodeRefactoring(
                                new[] { new ReverseConditionalAction(document, ifStatement) }
                                , ifStatement.Condition.Span);
            }

            return null;
        }
    }
}
