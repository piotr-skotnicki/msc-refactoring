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
    [ExportCodeRefactoringProvider("ExtractMethod", LanguageNames.CSharp)]
    public class ExtractMethodRefactoring : ICodeRefactoringProvider
    {
        public CodeRefactoring GetRefactoring(IDocument document, TextSpan textSpan, CancellationToken cancellationToken)
        {
            SyntaxNode root = (SyntaxNode)document.GetSyntaxRoot(cancellationToken);

            // Get nodes for highlighted code (only nodes of expression type can be extracted to method)
            IEnumerable<ExpressionSyntax> selectedNodes = root.DescendantNodes(textSpan).OfType<ExpressionSyntax>();

            // Note: This does not work always.
            // Consider int a = 1 + 2 + 3; and extracting 2 + 3
            // There is no such expression like 2 + 3, rather there is ((1 + 2) + 3)

            // Select the node that spans the selected text most
            ExpressionSyntax selectedNode = null;
            int bestSpan = -1;
            foreach (var node in selectedNodes)
            {
                if (textSpan.Contains(node.Span))
                {
                    int spanWidth = node.Span.End - node.Span.Start;
                    if (spanWidth > bestSpan)
                    {
                        bestSpan = spanWidth;
                        selectedNode = node;
                    }
                }
            }

            // If no expression fits in selection, raise no refactoring
            if (selectedNode == null)
            {
                return null;
            }

            // Verify is the expression within statement. IdentifierName within method's parameter list is also expression, but can not be extracted.
            if (selectedNode.FirstAncestorOrSelf<StatementSyntax>() == null)
            {
                return null;
            }

            // (1) Only top most member access expression can be extracted
            if (selectedNode.Ancestors().OfType<MemberAccessExpressionSyntax>().Any(n => n.Name.DescendantNodesAndSelf().Contains(selectedNode)))
            {
                return null;
            }

            // (2) Selected node must not be (note `Equals', not `Contains') an expression of method invocation
            if (selectedNode.Ancestors().OfType<InvocationExpressionSyntax>().Any(n => n.Expression.Equals(selectedNode)))
            {
                return null;
            }

            // (3) Left hand side of assignment (=, +=, -=, etc.) cannot be extracted
            if (selectedNode.Ancestors().OfType<BinaryExpressionSyntax>().Any(n => IsComplexAssignment(n) && n.Left.Equals(selectedNode)))
            {
                return null;
            }
            
            return new CodeRefactoring(
                            new[] { new ExtractMethodAction(document, selectedNode) }
                            , selectedNode.Span);
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
    }
}
