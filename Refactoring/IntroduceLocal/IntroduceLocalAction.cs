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
    class IntroduceLocalAction : ICodeAction
    {
        IDocument document;
        ExpressionSyntax expression;

        public IntroduceLocalAction(IDocument document, ExpressionSyntax expression)
        {
            this.document = document;
            this.expression = expression;
        }

        public string Description
        {
            get
            {
                return String.Format("Introduce variable");
            }
        }

        public CodeActionEdit GetEdit(CancellationToken cancellationToken)
        {
            SyntaxNode root = (SyntaxNode)this.document.GetSyntaxRoot(cancellationToken);
            ISemanticModel model = this.document.GetSemanticModel(cancellationToken);
            
            // New variable's name
            const string variableName = "newVariable";

            // Get resultant type of expresison
            ITypeSymbol typeSymbol = model.GetTypeInfo(this.expression, cancellationToken).Type;

            if (typeSymbol == null)
            {
                return null;
            }

            StatementSyntax expressionStatement = this.expression.FirstAncestorOrSelf<StatementSyntax>();

            if (expressionStatement == null)
            {
                return null;
            }
            
            // Parse type name
            TypeSyntax variableType = Syntax.ParseTypeName(typeSymbol.ToMinimalDisplayString(expressionStatement.GetLocation(), model));

            // Create new variable definition with expression as initializer
            VariableDeclaratorSyntax variableDeclarator = Syntax.VariableDeclarator(variableName)
                                                            .WithInitializer(Syntax.EqualsValueClause(this.expression));

            VariableDeclarationSyntax variableDeclaration = Syntax.VariableDeclaration(variableType)
                                                              .AddVariables(variableDeclarator);

            LocalDeclarationStatementSyntax declarationStatement = Syntax.LocalDeclarationStatement(variableDeclaration)
                                                                        .WithAdditionalAnnotations(CodeAnnotations.Formatting);
            
            // Replace expression with reference to new variable
            IdentifierNameSyntax variableReference = Syntax.IdentifierName(variableName)
                                                        .WithLeadingTrivia(this.expression.GetLeadingTrivia())
                                                        .WithTrailingTrivia(this.expression.GetTrailingTrivia());

            // Get parent statement (hopefully BlockSyntax)
            StatementSyntax parentStatement = expressionStatement.Parent as StatementSyntax;

            if (parentStatement == null)
            {
                return null;
            }

            StatementSyntax updatedStatement = null;
            // If expression's parent is ExpressionStatement, it means there is no need to put there new variable's reference again.
            // Considers:
            // foo(); -> int newVariable = foo();        , not int newVariable = foo(); newVariable;
            // new A(); -> A newVariable = new A();      , not A newVariable = new A(); newVariable;

            if (this.expression.Parent.Kind != SyntaxKind.ExpressionStatement)
            {
                updatedStatement = expressionStatement.ReplaceNode(this.expression, variableReference);
            }
            else
            {
                // Otherwise updatedStatement will be null, so original expression will be wiped out :)
            }

            // If statement is not contained within block, block should be created to store variable declaration and its original statement
            if (parentStatement.Kind != SyntaxKind.Block)
            {
                StatementSyntax newStatement = Syntax.Block(Syntax.List<StatementSyntax>(declarationStatement, updatedStatement))
                                                   .WithAdditionalAnnotations(CodeAnnotations.Formatting);

                SyntaxNode newRoot = root.ReplaceNode(expressionStatement, newStatement);

                return new CodeActionEdit(document.UpdateSyntaxRoot(newRoot));
            }
            else
            {
                // Insert variable's declaration statement at proper position right before old statement
                BlockSyntax block = parentStatement as BlockSyntax;

                int index = block.Statements.IndexOf(expressionStatement);

                BlockSyntax newBlock = block.ReplaceNode(expressionStatement, updatedStatement);

                newBlock = newBlock.WithStatements(newBlock.Statements.Insert(index, declarationStatement));

                SyntaxNode newRoot = root.ReplaceNode(block, newBlock);

                return new CodeActionEdit(document.UpdateSyntaxRoot(newRoot));
            }
        }

        public ImageSource Icon
        {
            get { return null; }
        }
    }
}
