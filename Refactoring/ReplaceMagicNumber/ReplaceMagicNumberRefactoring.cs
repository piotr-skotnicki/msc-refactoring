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
    [ExportCodeRefactoringProvider("ReplaceMagicNumber", LanguageNames.CSharp)]
    public class ReplaceMagicNumberRefactoring : ICodeRefactoringProvider
    {
        public CodeRefactoring GetRefactoring(IDocument document, TextSpan textSpan, CancellationToken cancellationToken)
        {
            SyntaxNode root = (SyntaxNode)document.GetSyntaxRoot(cancellationToken);
            SyntaxToken token = root.FindToken(textSpan.Start, findInsideTrivia: true);
                        
            // Verify is the selected token a numeric literal
            if (token.Kind == SyntaxKind.NumericLiteralToken && token.Span.Start <= textSpan.End && textSpan.End <= token.Span.End)
            {
                LiteralExpressionSyntax literalExpression = token.Parent as LiteralExpressionSyntax;

                if (literalExpression != null
                    && literalExpression.Token != null
                    && literalExpression.Token.Value != null)
                {
                    return new CodeRefactoring(
                                    new[] { new ReplaceMagicNumberAction(document, literalExpression) }
                                    , literalExpression.Span);
                }
            }

            return null;
        }
    }
}
