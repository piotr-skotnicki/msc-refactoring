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
    [ExportSyntaxNodeCodeIssueProvider("RemoveAssignmentToParameter", LanguageNames.CSharp, typeof(ParameterSyntax), typeof(BinaryExpressionSyntax))]
    public class RemoveAssignmentToParameterIssue : ICodeIssueProvider
    {
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken)
        {
            ISemanticModel model = document.GetSemanticModel(cancellationToken);

            // Handle ParameterSyntax vs. BinaryExpressionSyntax separately
            if (node is ParameterSyntax)
            {
                ParameterSyntax parameterDeclaration = (ParameterSyntax)node;
                ParameterSymbol parameterSymbol = (ParameterSymbol)model.GetDeclaredSymbol(parameterDeclaration, cancellationToken);
                
                // Do not raise issue for ref/out parameters
                if (parameterDeclaration.Modifiers.Any(n => n.Kind == SyntaxKind.RefKeyword || n.Kind == SyntaxKind.OutKeyword))
                {
                    yield break;
                }

                // Get containing method and its body
                BaseMethodDeclarationSyntax baseMethodDeclaration = parameterDeclaration.FirstAncestorOrSelf<BaseMethodDeclarationSyntax>();

                // TODO: indexer is not a method and is not of BaseMethodDeclarationSyntax type, but it contains ParameterSyntax. Additionally, it has two bodies.
                if (baseMethodDeclaration == null)
                {
                    // Failed to find BaseMethodDeclarationSyntax because this is Indexer 
                    yield break;
                }

                // Skip if has diagnostics
                if (baseMethodDeclaration.HasDiagnostics)
                {
                    yield break;
                }

                // Lambda expressions can be defined within some method (BaseMethodDeclarationSyntax), but the ParameterSyntax belongs to lambda, not the method!
                // Verify is the parameter defined in the method singature
                if (!baseMethodDeclaration.ParameterList.DescendantNodes().OfType<ParameterSyntax>().Contains(parameterDeclaration))
                {
                    yield break;
                }

                BlockSyntax block = baseMethodDeclaration.Body;
                if (block == null)
                {
                    yield break;
                }

                // Verify is there an assignment to parameter within method's body
                var analysis = model.AnalyzeStatementDataFlow(block);
                if (analysis.WrittenInside.Contains(parameterSymbol))
                {
                    yield return new CodeIssue(CodeIssue.Severity.Info, parameterDeclaration.Span, "Remove assignment to parameter", new RemoveAssignmentToParameterAction(document, parameterDeclaration));
                }
            }
            else if (node is BinaryExpressionSyntax)
            {
                BinaryExpressionSyntax expression = (BinaryExpressionSyntax)node;

                if (expression.HasDiagnostics)
                {
                    yield break;
                }

                // Considers:
                // void foo(int a) { a += 1; a = 2; } -> void foo(int a) { int temp = a; temp += 1; temp = 2; }

                if (IsComplexAssignment(expression))
                {
                    ISymbol lhsSymbol = model.GetSymbolInfo(expression.Left, cancellationToken).Symbol;
                    if (lhsSymbol != null)
                    {
                        // Check is the left-hand-side of assignment a parameter
                        if (lhsSymbol.Kind == CommonSymbolKind.Parameter)
                        {
                            // Get the declaration node of related symbol
                            ParameterSyntax parameterDeclaration = node.FirstAncestorOrSelf<BaseMethodDeclarationSyntax>()
                                                                       .DescendantNodes()
                                                                       .OfType<ParameterSyntax>()
                                                                       .FirstOrDefault(n => model.GetDeclaredSymbol(n, cancellationToken).Equals(lhsSymbol));

                            if (parameterDeclaration != null)
                            {
                                // Do not raise issue for ref/out parameters
                                if (parameterDeclaration.Modifiers.Any(n => n.Kind == SyntaxKind.RefKeyword || n.Kind == SyntaxKind.OutKeyword))
                                {
                                    yield break;
                                }

                                // TODO: indexer is not a method and is not of BaseMethodDeclarationSyntax type. Additionally, it has two bodies.

                                // Get containing method and its body
                                BaseMethodDeclarationSyntax baseMethodDeclaration = parameterDeclaration.FirstAncestorOrSelf<BaseMethodDeclarationSyntax>();

                                // TODO: indexer is not a method and is not of BaseMethodDeclarationSyntax type, but it contains ParameterSyntax. Additionally, it has two bodies.
                                if (baseMethodDeclaration == null)
                                {
                                    // Failed to find BaseMethodDeclarationSyntax because this is Indexer 
                                    yield break;
                                }

                                yield return new CodeIssue(CodeIssue.Severity.Info, expression.Left.Span, "Remove assignment to parameter", new RemoveAssignmentToParameterAction(document, parameterDeclaration));
                            }
                        }
                    }
                }
            }

            yield break;
        }

        bool IsComplexAssignment(BinaryExpressionSyntax node)
        {
            SyntaxKind kind = node.Kind;
            return node.Kind == SyntaxKind.AssignExpression
                   || node.Kind == SyntaxKind.AddAssignExpression
                   || node.Kind == SyntaxKind.AndAssignExpression
                   || node.Kind == SyntaxKind.ExclusiveOrAssignExpression
                   || node.Kind == SyntaxKind.SubtractAssignExpression
                   || node.Kind == SyntaxKind.MultiplyAssignExpression
                   || node.Kind == SyntaxKind.ModuloAssignExpression
                   || node.Kind == SyntaxKind.DivideAssignExpression
                   || node.Kind == SyntaxKind.LeftShiftAssignExpression
                   || node.Kind == SyntaxKind.RightShiftAssignExpression
                   || node.Kind == SyntaxKind.OrAssignExpression;
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
