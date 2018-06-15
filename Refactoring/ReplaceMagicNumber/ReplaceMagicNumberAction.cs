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
    class ReplaceMagicNumberAction : ICodeAction
    {
        IDocument document;
        LiteralExpressionSyntax literalExpression;

        public ReplaceMagicNumberAction(IDocument document, LiteralExpressionSyntax literalExpression)
        {
            this.document = document;
            this.literalExpression = literalExpression;
        }

        public string Description
        {
            get
            {
                return String.Format("Replace magic number with symbolic constant");
            }
        }

        public CodeActionEdit GetEdit(CancellationToken cancellationToken)
        {
            SyntaxNode root = (SyntaxNode)this.document.GetSyntaxRoot(cancellationToken);
            ISemanticModel model = this.document.GetSemanticModel(cancellationToken);

            // New variable's name
            const string magicNumberName = "MAGIC_NUMBER";

            // Get resultant type of literal
            TypeSymbol typeSymbol = model.GetTypeInfo(this.literalExpression, cancellationToken).Type as TypeSymbol;

            if (typeSymbol == null)
            {
                return null;
            }

            StatementSyntax closestStatement = this.literalExpression.FirstAncestorOrSelf<StatementSyntax>();

            // TODO: handle for-loops and if statements
            BlockSyntax closestBlock = this.literalExpression.FirstAncestorOrSelf<BlockSyntax>();

            // Parse type name
            TypeSyntax variableType = Syntax.ParseTypeName(typeSymbol.ToMinimalDisplayString(closestStatement.GetLocation(), model));

            // Create new variable definition with expression as initializer
            VariableDeclaratorSyntax variableDeclarator = Syntax.VariableDeclarator(magicNumberName)
                                                            .WithInitializer(Syntax.EqualsValueClause(this.literalExpression));

            VariableDeclarationSyntax variableDeclaration = Syntax.VariableDeclaration(variableType)
                                                              .AddVariables(variableDeclarator);

            // Create declaration statement with `const' modifier
            LocalDeclarationStatementSyntax declarationStatement = Syntax.LocalDeclarationStatement(variableDeclaration)
                                                                     .WithModifiers(Syntax.TokenList(Syntax.Token(SyntaxKind.ConstKeyword)))
                                                                     .WithAdditionalAnnotations(CodeAnnotations.Formatting);
                        
            ReplaceMagicNumberRewriter visitor = new ReplaceMagicNumberRewriter(this.literalExpression, magicNumberName, model, cancellationToken);
            BlockSyntax newBlock = (BlockSyntax)visitor.Visit(closestBlock);

            // Insert variable's declaration statement at proper position
            newBlock = newBlock.WithStatements(newBlock.Statements.Insert(0, declarationStatement));

            SyntaxNode newRoot = root.ReplaceNode(closestBlock, newBlock);

            return new CodeActionEdit(document.UpdateSyntaxRoot(newRoot));
        }

        public ImageSource Icon
        {
            get { return null; }
        }

        class ReplaceMagicNumberRewriter : SyntaxRewriter
        {
            LiteralExpressionSyntax literalExpression;
            SyntaxToken literalToken;
            string magicNumberName;
            ISemanticModel model;
            CancellationToken cancellationToken;

            public ReplaceMagicNumberRewriter(LiteralExpressionSyntax literalExpression, string magicNumberName, ISemanticModel model, CancellationToken cancellationToken)
            {
                this.literalExpression = literalExpression;
                this.magicNumberName = magicNumberName;
                this.literalToken = this.literalExpression.Token;
                this.model = model;
                this.cancellationToken = cancellationToken;
            }

            public override SyntaxNode VisitLiteralExpression(LiteralExpressionSyntax node)
            {
                // Check are the real objects (boxed to `object' type) equal
                if (node != null
                    && node.Token != null
                    && node.Token.Value != null
                    && node.Token.Value.Equals(this.literalToken.Value))
                {
                    // Replace literal with reference to symbolic constant
                    IdentifierNameSyntax symbolicConstantReference = Syntax.IdentifierName(magicNumberName)
                                                                        .WithLeadingTrivia(node.GetLeadingTrivia())
                                                                        .WithTrailingTrivia(node.GetTrailingTrivia());

                    return symbolicConstantReference;
                }

                return base.VisitLiteralExpression(node);
            }
        }
    }
}
