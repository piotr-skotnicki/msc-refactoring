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
    [ExportCodeRefactoringProvider("MakeMethodStatic", LanguageNames.CSharp)]
    public class MakeMethodStaticRefactoring : ICodeRefactoringProvider
    {
        public CodeRefactoring GetRefactoring(IDocument document, TextSpan textSpan, CancellationToken cancellationToken)
        {
            SyntaxNode root = (SyntaxNode)document.GetSyntaxRoot(cancellationToken);
            SyntaxToken token = root.FindToken(textSpan.Start, findInsideTrivia: true);

            SyntaxNode parentNode = token.Parent;

            if (parentNode == null)
            {
                return null;
            }

            if (parentNode.Kind == SyntaxKind.MethodDeclaration && token.Span.Start <= textSpan.End && textSpan.End <= token.Span.End)
            {
                MethodDeclarationSyntax methodDeclaration = (MethodDeclarationSyntax)parentNode;

                // Break analysis if declaration statement already has `static' modifier
                if (methodDeclaration.Modifiers.Any(SyntaxKind.StaticKeyword))
                {
                    return null;
                }

                // Break analysis, if method uses `base.' qualified expression, as it cannot be replaced by anything else
                // It is not possible to refer to .base via variable, e.g. a.base.x = 1;
                if (methodDeclaration.DescendantNodes().OfType<BaseExpressionSyntax>().Any())
                {
                    return null;
                }

                return new CodeRefactoring(
                                new[] { new MakeMethodStaticAction(document, methodDeclaration) }
                                , methodDeclaration.Identifier.Span);
            }

            return null;
        }
    }
}
