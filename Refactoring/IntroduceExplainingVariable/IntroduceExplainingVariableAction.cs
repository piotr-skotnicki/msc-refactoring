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
    class IntroduceExplainingVariableAction : ICodeAction
    {
        IDocument document;
        IfStatementSyntax ifStatement;

        public IntroduceExplainingVariableAction(IDocument document, IfStatementSyntax ifStatement)
        {
            this.document = document;
            this.ifStatement = ifStatement;
        }

        public string Description
        {
            get
            {
                return String.Format("Introduce explaining variables");
            }
        }

        public CodeActionEdit GetEdit(CancellationToken cancellationToken)
        {
            SyntaxNode root = (SyntaxNode)this.document.GetSyntaxRoot(cancellationToken);
            ISemanticModel model = this.document.GetSemanticModel(cancellationToken);
            
            // Extract all arithmetic operands of logical expressions
            IList<ExpressionSyntax> expressions = new List<ExpressionSyntax>();
            ExtractExpressions(this.ifStatement.Condition, expressions);

            // Introduce explaining variables
            IntroduceExplainingVariableRewriter visitor = new IntroduceExplainingVariableRewriter(expressions);
            ExpressionSyntax newCondition = (ExpressionSyntax)visitor.Visit(this.ifStatement.Condition);

            newCondition = newCondition.WithAdditionalAnnotations(CodeAnnotations.Formatting);
            
            IfStatementSyntax updatedIfStatement = this.ifStatement.WithCondition(newCondition);

            // Replace If statement and insert new variables to parental scope

            StatementSyntax parentStatement = (StatementSyntax)this.ifStatement.Ancestors().First(n => n is StatementSyntax);

            if (parentStatement == null)
            {
                return null;
            }

            // If IfStatement is not contained within block, block should be created to store variables declarations and its original statement
            if (parentStatement.Kind != SyntaxKind.Block)
            {
                StatementSyntax newStatement = Syntax.Block(Syntax.List<StatementSyntax>(visitor.LocalDeclarationStatements.ToArray()).Add(updatedIfStatement))
                                                   .WithAdditionalAnnotations(CodeAnnotations.Formatting);

                SyntaxNode newRoot = root.ReplaceNode(this.ifStatement, newStatement);

                return new CodeActionEdit(document.UpdateSyntaxRoot(newRoot));
            }
            else
            {
                // Insert variables' declaration statements at proper position right before old statement
                BlockSyntax block = parentStatement as BlockSyntax;

                int index = block.Statements.IndexOf(this.ifStatement);

                BlockSyntax newBlock = block.ReplaceNode(this.ifStatement, updatedIfStatement);

                newBlock = newBlock.WithStatements(newBlock.Statements.Insert(index, visitor.LocalDeclarationStatements.ToArray()));

                SyntaxNode newRoot = root.ReplaceNode(block, newBlock);

                return new CodeActionEdit(document.UpdateSyntaxRoot(newRoot));
            }
        }

        private void ExtractExpressions(ExpressionSyntax expressionSyntax, IList<ExpressionSyntax> expressions)
        {
            if (expressionSyntax is BinaryExpressionSyntax)
            {
                BinaryExpressionSyntax binaryExpression = (BinaryExpressionSyntax)expressionSyntax;

                if (binaryExpression.Kind == SyntaxKind.LogicalAndExpression
                    || binaryExpression.Kind == SyntaxKind.LogicalOrExpression)
                {
                    ExtractExpressions(binaryExpression.Left, expressions);
                    ExtractExpressions(binaryExpression.Right, expressions);
                }
                else
                {
                    expressions.Add(binaryExpression);
                }
            }
            else if (expressionSyntax is PrefixUnaryExpressionSyntax)
            {
                PrefixUnaryExpressionSyntax unaryExpression = (PrefixUnaryExpressionSyntax)expressionSyntax;

                if (unaryExpression.Kind == SyntaxKind.LogicalNotExpression)
                {
                    ExtractExpressions(unaryExpression.Operand, expressions);
                }
            }
            else if (expressionSyntax is ParenthesizedExpressionSyntax)
            {
                ExtractExpressions(RemoveParenthesis(expressionSyntax), expressions);
            }
        }

        private ExpressionSyntax RemoveParenthesis(ExpressionSyntax expressionSyntax)
        {
            if (expressionSyntax.Kind == SyntaxKind.ParenthesizedExpression)
            {
                ParenthesizedExpressionSyntax parenthesizedExpression = (ParenthesizedExpressionSyntax)expressionSyntax;
                return RemoveParenthesis(parenthesizedExpression.Expression);
            }
            else
            {
                return expressionSyntax;
            }
        }

        public ImageSource Icon
        {
            get { return null; }
        }

        class IntroduceExplainingVariableRewriter : SyntaxRewriter
        {
            IEnumerable<ExpressionSyntax> expressions;
            IList<LocalDeclarationStatementSyntax> localDeclarationStatements = new List<LocalDeclarationStatementSyntax>();
            int order = 0;

            public IEnumerable<LocalDeclarationStatementSyntax> LocalDeclarationStatements
            {
                get { return this.localDeclarationStatements; }
            }

            public IntroduceExplainingVariableRewriter(IEnumerable<ExpressionSyntax> expressions)
            {
                this.expressions = expressions;
            }

            public override SyntaxNode VisitBinaryExpression(BinaryExpressionSyntax node)
            {
                BinaryExpressionSyntax processedNode = (BinaryExpressionSyntax)base.VisitBinaryExpression(node);

                if (expressions.Contains(node))
                {
                    // Create variable name
                    string variableName = String.Format("isVar{0}", ++order);

                    // Left and Right descendants now contain references to new variables, so a new initializer is required
                    ExpressionSyntax initializer = Syntax.BinaryExpression(processedNode.Kind, processedNode.Left, processedNode.Right);

                    // Define new variable and initialize it with logical expression,
                    // with operands of the new descendant nodes
                    VariableDeclaratorSyntax variableDeclarator = Syntax.VariableDeclarator(variableName)
                                                                        .WithInitializer(Syntax.EqualsValueClause(initializer));

                    VariableDeclarationSyntax variableDeclaration = Syntax.VariableDeclaration(Syntax.PredefinedType(Syntax.Token(SyntaxKind.BoolKeyword)))
                                                                        .AddVariables(variableDeclarator);

                    LocalDeclarationStatementSyntax declarationStatement = Syntax.LocalDeclarationStatement(variableDeclaration)
                                                                                   .WithAdditionalAnnotations(CodeAnnotations.Formatting);
                    
                    // Replace expression with reference to new variable
                    IdentifierNameSyntax variableReference = Syntax.IdentifierName(variableName)
                                                                .WithLeadingTrivia(processedNode.GetLeadingTrivia())
                                                                .WithTrailingTrivia(processedNode.GetTrailingTrivia());

                    this.localDeclarationStatements.Add(declarationStatement);

                    return variableReference;
                }

                return processedNode;
            }

            public override SyntaxNode VisitParenthesizedExpression(ParenthesizedExpressionSyntax node)
            {
                if (expressions.Contains(node.Expression))
                {
                    return Visit(node.Expression);
                }

                return base.VisitParenthesizedExpression(node);
            }

            public override SyntaxNode VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
            {
                // TODO: In `!(expression)' operand is parenthesized, so does not work at all
                if (expressions.Contains(node.Operand))
                {
                    return Visit(node.Operand);
                }

                return base.VisitPrefixUnaryExpression(node);
            }
        }
    }
}
