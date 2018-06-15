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
    [ExportSyntaxNodeCodeIssueProvider("RemoveParameter", LanguageNames.CSharp, typeof(ParameterSyntax))]
    public class RemoveParameterIssue : ICodeIssueProvider
    {
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken)
        {
            ParameterSyntax parameterDeclaration = (ParameterSyntax)node;

            ISemanticModel model = document.GetSemanticModel(cancellationToken);
            
            // Get parent declaration: Method/Ctor/Dtor/Operator/ConversionOperator
            BaseMethodDeclarationSyntax baseMethodDeclaration = parameterDeclaration.FirstAncestorOrSelf<BaseMethodDeclarationSyntax>();

            if (baseMethodDeclaration == null)
            {
                // TODO: Indexers have Parameters, but does not derive BaseMethodDeclarationSyntax
                // This is where indexers fall into !!
                // DelegateDeclarationSyntax also is here, but that's ok, I cannot modify delegate
                yield break;
            }

            // Operators and Conversion Operators should not be touched !
            if (baseMethodDeclaration.Kind == SyntaxKind.OperatorDeclaration
                || baseMethodDeclaration.Kind == SyntaxKind.ConversionOperatorDeclaration)
            {
                yield break;
            }

            // Make sure body is defined
            if (baseMethodDeclaration.Body == null)
            {
                yield break;
            }

            ISymbol baseMethodSymbol = model.GetDeclaredSymbol(baseMethodDeclaration);

            if (baseMethodSymbol == null)
            {
                yield break;
            }

            if (baseMethodSymbol is MethodSymbol)
            {
                MethodSymbol methodSymbol = (MethodSymbol)baseMethodSymbol;

                // Check is the method an implementation of the a super class method
                // If so, the parameter cannot be removed, as it is a part of polymorphic signature
                if (methodSymbol.OverriddenMethod != null)
                {
                    yield break;
                }
                
                // TODO: This does not work for implicitly implemented interface member
                if (methodSymbol.MethodKind == MethodKind.ExplicitInterfaceImplementation)
                {
                    yield break;
                }
            }

            // Get the symbol for parameter declaration
            ParameterSymbol parameterSymbol = model.GetDeclaredSymbol(parameterDeclaration) as ParameterSymbol;

            // Check is the parameter unused within method's body
            var analysis = model.AnalyzeStatementDataFlow(baseMethodDeclaration.Body);
            if (analysis.WrittenInside.Contains(parameterSymbol)
                || analysis.ReadInside.Contains(parameterSymbol))
            {
                yield break;
            }

            yield return new CodeIssue(CodeIssue.Severity.Info, parameterDeclaration.Span, "Parameter can be removed", new RemoveParameterAction(document, parameterDeclaration));
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