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
    [ExportSyntaxNodeCodeIssueProvider("MakeConst", LanguageNames.CSharp, typeof(LocalDeclarationStatementSyntax))]
    public class MakeConstIssue : ICodeIssueProvider
    {
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken)
        {
            LocalDeclarationStatementSyntax declarationStatement = (LocalDeclarationStatementSyntax)node;

            // Break analysis if declaration statement already has `const' modifier
            if (declarationStatement.Modifiers.Any(SyntaxKind.ConstKeyword))
            {
                yield break;
            }

            TypeSyntax typeSyntax = declarationStatement.Declaration.Type;

            // Break analysis if variable is implicitly typed with `var'
            if (typeSyntax.IsVar)
            {
                yield break;
            }

            ISemanticModel model = document.GetSemanticModel(cancellationToken);

            ITypeSymbol type = model.GetTypeInfo(typeSyntax).ConvertedType;

            if (type == null)
            {
                yield break;
            }

            // Verify that all declared variables within statement are initialized with constant value
            foreach (var variable in declarationStatement.Declaration.Variables)
            {
                if (variable.Initializer == null)
                {
                    yield break;
                }

                var constValue = model.GetConstantValue(variable.Initializer.Value);
                if (!constValue.HasValue)
                {
                    yield break;
                }

                // If literal value is of string type, declared type must be string as well
                // Considers:
                // object foo = "bar"; -> no change allowed
                // string foo = "bar"; -> const string foo = "bar";
                if (constValue.Value is String && type.SpecialType != SpecialType.System_String)
                {
                    yield break;
                }
            }

            // Verify that the variable is not assigned other value
            var statementAnalysis = model.AnalyzeStatementDataFlow(declarationStatement);

            foreach (var variable in declarationStatement.Declaration.Variables)
            {
                ISymbol symbol = model.GetDeclaredSymbol(variable);

                if (statementAnalysis.WrittenOutside.Contains(symbol))
                {
                    yield break;
                }
            }

            yield return new CodeIssue(CodeIssue.Severity.Info, declarationStatement.Declaration.Type.Span, "Can be made `const'", new MakeConstAction(document, declarationStatement));
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
