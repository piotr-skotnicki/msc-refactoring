using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Media;
using Roslyn.Compilers;
using Roslyn.Compilers.Common;
using Roslyn.Compilers.CSharp;
using Roslyn.Services;
using Roslyn.Services.Editor;

namespace Refactoring
{
    class ReverseConditionalAction : ICodeAction
    {
        IDocument document;
        IfStatementSyntax ifStatement;

        public ReverseConditionalAction(IDocument document, IfStatementSyntax ifStatement)
        {
            this.document = document;
            this.ifStatement = ifStatement;
        }

        public string Description
        {
            get
            {
                return String.Format("Reverse conditional");
            }
        }

        public CodeActionEdit GetEdit(CancellationToken cancellationToken)
        {
            SyntaxNode root = (SyntaxNode)this.document.GetSyntaxRoot(cancellationToken);
            ISemanticModel model = this.document.GetSemanticModel(cancellationToken);

            // For IfStatement with condition-less else clause, the condition is negated
            // Considers:
            // if (condition) blockA else blockB -> if (!(condition)) blockB else blockA
            if (!(this.ifStatement.Else.Statement is IfStatementSyntax))
            {
                StatementSyntax blockA = this.ifStatement.Statement;
                StatementSyntax blockB = this.ifStatement.Else.Statement;
                ExpressionSyntax condition = this.ifStatement.Condition;

                ExpressionSyntax newCondition = InverseExpression(condition);                               

                // When creating new IfStatement, utilize the original nodes and trivias as much as possible
                IfStatementSyntax newIfStatement = this.ifStatement.WithCondition(newCondition)
                                                                   .WithStatement(blockB)
                                                                   .WithElse(this.ifStatement.Else.WithStatement(blockA));

                SyntaxNode newRoot = root.ReplaceNode(this.ifStatement, newIfStatement);

                return new CodeActionEdit(this.document.UpdateSyntaxRoot(newRoot));
            }
            else
            {
                // For ElseClause with another IfStatement, the statements itself are swapped
                // Considers:
                // if (conditionA) blockA else if (conditionB) blockB -> if (conditionB) blockB else if (conditionA) blockA

                IfStatementSyntax elseClauseIf = (IfStatementSyntax)this.ifStatement.Else.Statement;

                StatementSyntax blockA = this.ifStatement.Statement;
                StatementSyntax blockB = elseClauseIf.Statement;
                ExpressionSyntax conditionA = this.ifStatement.Condition;
                ExpressionSyntax conditionB = elseClauseIf.Condition;
                
                // When creating new IfStatement, utilize the original nodes and trivias as much as possible
                IfStatementSyntax newIfStatement = this.ifStatement.WithCondition(conditionB)
                                                                   .WithStatement(blockB)
                                                                   .WithElse(this.ifStatement.Else.WithStatement(
                                                                         elseClauseIf.WithCondition(conditionA)
                                                                                     .WithStatement(blockA))
                                                                    );

                SyntaxNode newRoot = root.ReplaceNode(this.ifStatement, newIfStatement);

                return new CodeActionEdit(this.document.UpdateSyntaxRoot(newRoot));
            }
        }

        private ExpressionSyntax InverseExpression(ExpressionSyntax condition)
        {
            if (condition.Kind == SyntaxKind.TrueLiteralExpression)
            {
                // Considers:
                // if (true) -> if (false)
                return Syntax.LiteralExpression(SyntaxKind.FalseLiteralExpression)
                               .WithLeadingTrivia(condition.GetLeadingTrivia())
                               .WithTrailingTrivia(condition.GetTrailingTrivia());
            }
            else if (condition.Kind == SyntaxKind.FalseLiteralExpression)
            {
                // Considers:
                // if (false) -> if (true)
                return Syntax.LiteralExpression(SyntaxKind.TrueLiteralExpression)
                               .WithLeadingTrivia(condition.GetLeadingTrivia())
                               .WithTrailingTrivia(condition.GetTrailingTrivia());
            }
            else if (condition is PrefixUnaryExpressionSyntax)
            {
                PrefixUnaryExpressionSyntax unaryExpression = (PrefixUnaryExpressionSyntax)condition;
                // If the condition is already negated, then the negation should be removed rather than doubled
                // Considers:
                // if (!condition) blockA else blockB -> if (condition) blockB else blockA

                if (unaryExpression.Kind == SyntaxKind.LogicalNotExpression)
                {
                    return RemoveParenthesis(unaryExpression.Operand);
                }
            }
            else if (condition is BinaryExpressionSyntax)
            {
                BinaryExpressionSyntax binaryExpression = (BinaryExpressionSyntax)condition;

                if (IsRelationalExpression(binaryExpression))
                {
                    // Relational operators are substituted with complement form:
                    // Considers:
                    // a == b -> a != b
                    // a > b  -> a <= b
                    // a < b  -> a >= b
                    SyntaxToken @operator = binaryExpression.OperatorToken;
                    BinaryExpressionSyntax newCondition = binaryExpression.WithOperatorToken(
                                                              Syntax.Token(InverseOperator(@operator.Kind))
                                                              .WithLeadingTrivia(@operator.LeadingTrivia)
                                                              .WithTrailingTrivia(@operator.TrailingTrivia)
                                                           );
                    return newCondition;
                }
                else if ((binaryExpression.Kind == SyntaxKind.LogicalAndExpression
                           || binaryExpression.Kind == SyntaxKind.LogicalOrExpression)
                        && IsNotBinaryLogicalExpression(binaryExpression.Left)
                        && IsNotBinaryLogicalExpression(binaryExpression.Right))
                {
                    // Use DeMorgan law (but only for simple binary expressions)
                    // Considers:
                    // a || b -> !a && !b
                    // a && b -> !a || !b
                    SyntaxToken @operator = binaryExpression.OperatorToken;
                    BinaryExpressionSyntax newCondition = binaryExpression.WithOperatorToken(
                                                                            Syntax.Token(DeMorgan(@operator.Kind))
                                                                                .WithLeadingTrivia(@operator.LeadingTrivia)
                                                                                .WithTrailingTrivia(@operator.TrailingTrivia))
                                                                          .WithLeft(InverseExpression(binaryExpression.Left))
                                                                          .WithRight(InverseExpression(binaryExpression.Right));
                    return newCondition;
                }
            }
            
            // If none of above categories, then negate entire expression, add parenthesis if required, clean and copy trivias
            if (condition.Kind == SyntaxKind.IdentifierName)
            {
                // Considers:
                // a -> !a
                return Syntax.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, condition.WithLeadingTrivia().WithTrailingTrivia())
                                .WithLeadingTrivia(condition.GetLeadingTrivia())
                                .WithTrailingTrivia(condition.GetTrailingTrivia());
            }
            else
            {
                // Considers:
                // a ? b : c -> !(a ? b : c)
                return Syntax.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, Syntax.ParenthesizedExpression(condition.WithLeadingTrivia().WithTrailingTrivia()))
                                .WithLeadingTrivia(condition.GetLeadingTrivia())
                                .WithTrailingTrivia(condition.GetTrailingTrivia());
            }
        }

        private bool IsNotBinaryLogicalExpression(ExpressionSyntax expressionSyntax)
        {
            SyntaxKind kind = expressionSyntax.Kind;
            return kind != SyntaxKind.LogicalAndExpression && kind != SyntaxKind.LogicalOrExpression;
        }

        private SyntaxKind InverseOperator(SyntaxKind @operator)
        {
            switch (@operator)
            {
                // ==, !=
                case SyntaxKind.EqualsEqualsToken: return SyntaxKind.ExclamationEqualsToken;
                case SyntaxKind.ExclamationEqualsToken: return SyntaxKind.EqualsEqualsToken;

                // >=, <
                case SyntaxKind.GreaterThanEqualsToken: return SyntaxKind.LessThanToken;
                case SyntaxKind.LessThanToken: return SyntaxKind.GreaterThanEqualsToken;

                // >, <=
                case SyntaxKind.GreaterThanToken: return SyntaxKind.LessThanEqualsToken;
                case SyntaxKind.LessThanEqualsToken: return SyntaxKind.GreaterThanToken;
            }

            throw new ArgumentException();
        }

        private SyntaxKind DeMorgan(SyntaxKind @operator)
        {
            switch (@operator)
            {
                case SyntaxKind.AmpersandAmpersandToken: return SyntaxKind.BarBarToken;
                case SyntaxKind.BarBarToken: return SyntaxKind.AmpersandAmpersandToken;
            }

            throw new ArgumentException();
        }

        private bool IsRelationalExpression(BinaryExpressionSyntax binaryExpression)
        {
            SyntaxKind kind = binaryExpression.Kind;
            return kind == SyntaxKind.EqualsExpression
                || kind == SyntaxKind.NotEqualsExpression
                || kind == SyntaxKind.GreaterThanExpression
                || kind == SyntaxKind.GreaterThanOrEqualExpression
                || kind == SyntaxKind.LessThanExpression
                || kind == SyntaxKind.LessThanOrEqualExpression;
        }

        private ExpressionSyntax RemoveParenthesis(ExpressionSyntax expressionSyntax)
        {
            if (expressionSyntax.Kind == SyntaxKind.ParenthesizedExpression)
            {
                ParenthesizedExpressionSyntax parenthesizedExpression = (ParenthesizedExpressionSyntax)expressionSyntax;
                return RemoveParenthesis(parenthesizedExpression.Expression);
            }
            return expressionSyntax;
        }

        public ImageSource Icon
        {
            get { return null; }
        }
    }
}