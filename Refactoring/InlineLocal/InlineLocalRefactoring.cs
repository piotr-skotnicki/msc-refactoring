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
    [ExportCodeRefactoringProvider("InlineLocal", LanguageNames.CSharp)]
    public class InlineLocalRefactoring : ICodeRefactoringProvider
    {
        public CodeRefactoring GetRefactoring(IDocument document, TextSpan textSpan, CancellationToken cancellationToken)
        {
            SyntaxNode root = (SyntaxNode)document.GetSyntaxRoot(cancellationToken);

            // If nothing selected, only cursor placed, increase the ending position by one
            if (textSpan.Length == 0)
            {
                textSpan = new TextSpan(textSpan.Start, 1);
            }

            // Get nodes for highlighted code
            IEnumerable<VariableDeclaratorSyntax> selectedNodes = root.DescendantNodes(textSpan).OfType<VariableDeclaratorSyntax>();
            
            // Select the node that spans the selected text most
            // Note: Here I have reversed the ranges ! node.Span.Contains, not textSpan.Contains
            VariableDeclaratorSyntax selectedNode = null;
            int bestSpan = -1;
            foreach (var node in selectedNodes)
            {
                if (node.Span.Contains(textSpan))
                {
                    int spanWidth = node.Span.Intersection(textSpan).Value.Length;
                    if (spanWidth > bestSpan)
                    {
                        bestSpan = spanWidth;
                        selectedNode = node;
                    }
                }
            }
            
            if (selectedNode == null)
            {
                return null;
            }
            
            VariableDeclaratorSyntax declarator = selectedNode;

            // Verify does the variable declarator has value assigned
            if (declarator.Initializer == null || declarator.HasDiagnostics)
            {
                return null;
            }

            ISemanticModel model = document.GetSemanticModel(cancellationToken);

            ISymbol declaredSymbol = model.GetDeclaredSymbol(declarator);

            LocalDeclarationStatementSyntax declarationStatement = declarator.FirstAncestorOrSelf<LocalDeclarationStatementSyntax>();

            // Note: declarator might be within ForStatement, but cannot be inlined
            if (declarationStatement == null)
            {
                return null;
            }

            var analysis = model.AnalyzeStatementDataFlow(declarationStatement);

            // Verify that the variable is never re-assigned
            if (analysis.WrittenOutside.Contains(declaredSymbol))
            {
                return null;
            }

            // Variable inlineing is not allowed if variable is never used (side effects never take place then!)
            if (!analysis.ReadOutside.Contains(declaredSymbol))
            {
                return null;
            }

            BlockSyntax block = declarationStatement.FirstAncestorOrSelf<BlockSyntax>();
            if (block == null)
            {
                return null;
            }

            // Verify that the variables used in declaration are not re-assigned within variable usage scope
            // Consider:
            // int a = 1;
            // int b = a;
            // int c = b;
            // a = 2;
            // int d = b;
            // `b' cannot be inlined because `d' would get other value

            // Note: ChildNodes instead of DescendantNodes, because it must not be any other (nested) block
            var blockAnalysis = model.AnalyzeStatementsDataFlow(declarationStatement, block.ChildNodes().OfType<StatementSyntax>().Last());
            foreach (var initializerSymbol in model.AnalyzeExpressionDataFlow(declarator.Initializer.Value).ReadInside)
            {
                if (blockAnalysis.WrittenInside.Contains(initializerSymbol))
                {
                    return null;
                }
            }

            return new CodeRefactoring(
                            new[] { new InlineLocalAction(document, declarator) }
                            , declarator.Span);
        }
    }
}
