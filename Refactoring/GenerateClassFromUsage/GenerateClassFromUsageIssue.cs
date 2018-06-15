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
using System.Windows.Media;

namespace Refactoring
{
    [ExportSyntaxNodeCodeIssueProvider("GenerateClassFromUsage", LanguageNames.CSharp, typeof(ObjectCreationExpressionSyntax))]
    public class GenerateClassFromUsageIssue : ICodeIssueProvider
    {
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken)
        {
            ISemanticModel model = document.GetSemanticModel(cancellationToken);

            ObjectCreationExpressionSyntax expression = (ObjectCreationExpressionSyntax)node;

            // Note: Do NOT use HasDiagnostics, as this expression obviously DOES produce errors (type is not known yet)

            TypeSyntax typeSyntax = expression.Type;

            CommonTypeInfo typeInfo = model.GetTypeInfo(typeSyntax, cancellationToken);

            TypeSymbol typeSymbol = (TypeSymbol)typeInfo.Type;
            if (typeSymbol == null)
            {
                yield break;
            }

            // Check if this is an unrecognized symbol
            if (typeSymbol.Kind == SymbolKind.ErrorType)
            {
                string className = string.Empty;

                // TODO: extract container info and propose solutions
                // Considers:
                // new A.B.C() -> namespace A.B { class C {} }

                // Extract top-level name
                // Considers:
                // new A.B.C<int,float>() -> C
                if (typeSyntax.Kind == SyntaxKind.IdentifierName)
                {
                    className = typeSyntax.GetText();
                }
                else if (typeSyntax.Kind == SyntaxKind.GenericName)
                {
                    GenericNameSyntax genericName = (GenericNameSyntax)typeSyntax;
                    className = genericName.Identifier.GetText();
                }
                else if (typeSyntax.Kind == SyntaxKind.QualifiedName)
                {
                    QualifiedNameSyntax qualifiedName = (QualifiedNameSyntax)typeSyntax;
                    className = qualifiedName.Right.GetText();
                }

                yield return new CodeIssue(CodeIssue.Severity.Info, expression.Span, String.Format("Generate class `{0}'", className), new GenerateClassFromUsageAction(document, expression, className));
            }

            yield break;
        }

        #region Unimplemented ICodeIssueProvider members

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxToken token, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxTrivia trivia, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
