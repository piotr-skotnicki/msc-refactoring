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
    class RemoveAssignmentToParameterAction : ICodeAction
    {
        IDocument document;
        ParameterSyntax parameterDeclaration;

        public RemoveAssignmentToParameterAction(IDocument document, ParameterSyntax parameterDeclaration)
        {
            this.document = document;
            this.parameterDeclaration = parameterDeclaration;
        }

        public string Description
        {
            get
            {
                return String.Format("Remove assignment to parameter");
            }
        }

        public CodeActionEdit GetEdit(CancellationToken cancellationToken)
        {
            SyntaxNode root = (SyntaxNode)this.document.GetSyntaxRoot(cancellationToken);
            ISemanticModel model = this.document.GetSemanticModel(cancellationToken);

            ParameterSymbol parameterSymbol = (ParameterSymbol)model.GetDeclaredSymbol(this.parameterDeclaration);

            // Can be either constructor or regular method
            BaseMethodDeclarationSyntax baseMethodDeclaration = this.parameterDeclaration.FirstAncestorOrSelf<BaseMethodDeclarationSyntax>();

            BlockSyntax body = baseMethodDeclaration.Body;
            
            // Create temporary variable for parameter
            const string tempVariableName = "tempVariable";

            // Considers:
            // void foo(int a) {} -> void foo(int a) { int tempVariable = a; }
            VariableDeclaratorSyntax variableDeclarator = Syntax.VariableDeclarator(tempVariableName)
                                                               .WithInitializer(Syntax.EqualsValueClause(Syntax.IdentifierName(parameterDeclaration.Identifier)));

            VariableDeclarationSyntax variableDeclaration = Syntax.VariableDeclaration(parameterDeclaration.Type)
                                                                 .WithVariables(Syntax.SeparatedList<VariableDeclaratorSyntax>(variableDeclarator));

            LocalDeclarationStatementSyntax declarationStatement = Syntax.LocalDeclarationStatement(variableDeclaration);

            // Format newly created code
            declarationStatement = CodeAnnotations.Formatting.AddAnnotationTo(declarationStatement);

            // Run visitor
            RemoveAssignmentToParameterRewriter visitor = new RemoveAssignmentToParameterRewriter(model, body, parameterSymbol, tempVariableName, cancellationToken);
            BlockSyntax newBody = (BlockSyntax)visitor.Visit(body);

            // Prepend new variable's declaration statement
            newBody = newBody.WithStatements(newBody.Statements.Insert(0, declarationStatement));

            SyntaxNode newRoot = root.ReplaceNode(body, newBody);

            return new CodeActionEdit(document.UpdateSyntaxRoot(newRoot));
        }

        public ImageSource Icon
        {
            get { return null; }
        }

        class RemoveAssignmentToParameterRewriter : SyntaxRewriter
        {
            ISemanticModel model;
            BlockSyntax body;
            ParameterSymbol parameterSymbol;
            string variableName;
            CancellationToken cancellationToken;

            public RemoveAssignmentToParameterRewriter(ISemanticModel model, BlockSyntax body, ParameterSymbol parameterSymbol, string variableName, CancellationToken cancellationToken)
            {
                this.model = model;
                this.body = body;
                this.parameterSymbol = parameterSymbol;
                this.variableName = variableName;
                this.cancellationToken = cancellationToken;
            }

            public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
            {
                // Get related symbol
                ISymbol symbol = this.model.GetSymbolInfo(node).Symbol;

                // Check does the symbol refer to stored parameter
                if (symbol != null && symbol.Equals(this.parameterSymbol))
                {
                    // Create reference to temporary variable
                    IdentifierNameSyntax tempVariableReference = Syntax.IdentifierName(this.variableName)
                                                                          .WithLeadingTrivia(node.GetLeadingTrivia())
                                                                          .WithTrailingTrivia(node.GetTrailingTrivia());

                    // Check is the IdentifierNameExpression on the left side of binary expression
                    if (node.Parent != null && node.Parent is BinaryExpressionSyntax)
                    {
                        BinaryExpressionSyntax binaryExpression = (BinaryExpressionSyntax)node.Parent;
                        if (IsComplexAssignment(binaryExpression) && binaryExpression.Left != null && binaryExpression.Left.Equals(node))
                        {
                            // Assignment operation is rewritten to utilize new temporary variable
                            return tempVariableReference;
                        }
                    }

                    // Verify has the parameter symbol been already assigned new value
                    // If not, then no replacement should take place (we can still read the original value directly from parameter)
                    StatementSyntax parentStatement = node.FirstAncestorOrSelf<StatementSyntax>();

                    if (IsInConditionExpression(parentStatement, this.parameterSymbol))
                    {
                        return tempVariableReference;
                    }

                    if (IsAlreadyAssignedNewValue(parentStatement, this.parameterSymbol))
                    {   
                        return tempVariableReference;
                    }
                }

 	            return base.VisitIdentifierName(node);
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

            bool IsInConditionExpression(StatementSyntax statement, ISymbol symbol)
            {
                if (statement.Kind == SyntaxKind.IfStatement)
                {
                    IfStatementSyntax ifStatement = (IfStatementSyntax)statement;
                    return this.model.AnalyzeExpressionDataFlow(ifStatement.Condition).WrittenInside.Contains(symbol);
                }
                else if (statement.Kind == SyntaxKind.WhileStatement)
                {
                    WhileStatementSyntax whileStatement = (WhileStatementSyntax)statement;
                    return this.model.AnalyzeExpressionDataFlow(whileStatement.Condition).WrittenInside.Contains(symbol);
                }
                else if (statement.Kind == SyntaxKind.DoStatement)
                {
                    DoStatementSyntax doStatement = (DoStatementSyntax)statement;
                    return this.model.AnalyzeExpressionDataFlow(doStatement.Condition).WrittenInside.Contains(symbol);
                }
                else if (statement.Kind == SyntaxKind.ForStatement)
                {
                    ForStatementSyntax forStatement = (ForStatementSyntax)statement;
                    if (this.model.AnalyzeExpressionDataFlow(forStatement.Condition).WrittenInside.Contains(symbol))
                        return true;

                    if (forStatement.Initializers.Any(n => this.model.AnalyzeExpressionDataFlow(n).WrittenInside.Contains(symbol)))
                        return true;

                    if (forStatement.Incrementors.Any(n => this.model.AnalyzeExpressionDataFlow(n).WrittenInside.Contains(symbol)))
                        return true;
                }
                return false;
            }

            bool IsAlreadyAssignedNewValue(StatementSyntax statement, ISymbol symbol)
            {
                StatementSyntax parentStatement = statement.Parent as StatementSyntax;
                if (parentStatement == null)
                {
                    return false;
                }

                if (IsInConditionExpression(statement, this.parameterSymbol))
                {
                    return true;
                }
                
                if (parentStatement.Kind == SyntaxKind.Block)
                {
                    // For block statement, obtain a range of statements

                    BlockSyntax block = (BlockSyntax)parentStatement;

                    StatementSyntax firstStatementInScope = block.Statements.First();

                    int index = block.Statements.IndexOf(statement);
                    if (index == 0)
                    {
                        // If this is the only statement in block, simply analyze it
                        var analysis = this.model.AnalyzeStatementDataFlow(statement);

                        if (analysis.WrittenInside.Contains(symbol))
                        {
                            return true;
                        }

                        return IsAlreadyAssignedNewValue(parentStatement, symbol);
                    }
                    else
                    {
                        // If last statement can have its own scope, it should be handled separately, because RegionDataFlowAnalysis considers entire block if given as a e.g. `last' statement
                        if (HasOwnBlock(statement))
                        {
                            // Move index to point to the statement right before any of specified in if scoped statements
                            --index;
                        }

                        // Analyze the range of statements
                        StatementSyntax lastStatementInScope = block.Statements.ElementAt(index);

                        var analysis = this.model.AnalyzeStatementsDataFlow(firstStatementInScope, lastStatementInScope);

                        if (analysis.WrittenInside.Contains(symbol))
                        {
                            return true;
                        }

                        return IsAlreadyAssignedNewValue(parentStatement, symbol);
                    }
                }
                else
                {
                    // For non-block, simply analyze the single statement (possibly within block-less for or if)

                    var analysis = this.model.AnalyzeStatementDataFlow(statement);

                    if (analysis.WrittenInside.Contains(symbol))
                    {
                        return true;
                    }

                    return IsAlreadyAssignedNewValue(parentStatement, symbol);
                }
            }

            bool HasOwnBlock(StatementSyntax statement)
            {
                return statement.Kind == SyntaxKind.WhileStatement
                        || statement.Kind == SyntaxKind.ForStatement
                        || statement.Kind == SyntaxKind.IfStatement
                        || statement.Kind == SyntaxKind.DoStatement
                        || statement.Kind == SyntaxKind.Block;
            }
        }
    }
}