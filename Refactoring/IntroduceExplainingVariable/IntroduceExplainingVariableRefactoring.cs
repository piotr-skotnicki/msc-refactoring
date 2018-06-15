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
    [ExportCodeRefactoringProvider("IntroduceExplainingVariable", LanguageNames.CSharp)]
    public class IntroduceExplainingVariableRefactoring : ICodeRefactoringProvider
    {
        public CodeRefactoring GetRefactoring(IDocument document, TextSpan textSpan, CancellationToken cancellationToken)
        {
            SyntaxNode root = (SyntaxNode)document.GetSyntaxRoot(cancellationToken);

            // If nothing selected, only cursor placed, increase the ending position by one
            if (textSpan.Length == 0)
            {
                textSpan = new TextSpan(textSpan.Start, 1);
            }

            // Verify is the selected node a conditional expression within IfStatement
            ExpressionSyntax condition = root.DescendantNodes(textSpan)
                                             .OfType<ExpressionSyntax>()
                                             .Where(e => 
                                                 {
                                                     IfStatementSyntax @if = e.FirstAncestorOrSelf<IfStatementSyntax>();
                                                     if (@if != null && @if.Condition != null)
                                                     {
                                                         return @if.Condition.DescendantNodesAndSelf().Contains(e);
                                                     }
                                                     return false;
                                                 })
                                             .FirstOrDefault();

            if (condition == null)
            {
                // Not a conditional expression
                return null;
            }

            if (condition.HasDiagnostics)
            {
                return null;
            }

            IfStatementSyntax ifStatement = condition.FirstAncestorOrSelf<IfStatementSyntax>();
                        
            return new CodeRefactoring(
                            new[] { new IntroduceExplainingVariableAction(document, ifStatement) }
                            , ifStatement.Condition.Span);
        }
    }
}
