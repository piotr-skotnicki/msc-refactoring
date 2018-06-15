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
    class InlineLocalAction : ICodeAction
    {
        IDocument document;
        VariableDeclaratorSyntax declaratorNode;

        public InlineLocalAction(IDocument document, VariableDeclaratorSyntax declaratorNode)
        {
            this.document = document;
            this.declaratorNode = declaratorNode;
        }

        public string Description
        {
            get
            {
                return String.Format("Inline variable");
            }
        }

        public CodeActionEdit GetEdit(CancellationToken cancellationToken)
        {
            SyntaxNode root = (SyntaxNode)this.document.GetSyntaxRoot(cancellationToken);
            ISemanticModel model = this.document.GetSemanticModel(cancellationToken);

            ISymbol declaredSymbol = (Symbol)model.GetDeclaredSymbol(this.declaratorNode);

            // Get entire declaration
            VariableDeclarationSyntax variableDeclaration = (VariableDeclarationSyntax)this.declaratorNode.Parent;

            // Get declaration statement
            LocalDeclarationStatementSyntax declarationStatement = (LocalDeclarationStatementSyntax)variableDeclaration.Parent;

            // Get associated BlockSyntax the variable is declared inside
            BlockSyntax block = this.declaratorNode.FirstAncestorOrSelf<BlockSyntax>();

            // Get the declared type of expression
            TypeSyntax variableTypeNode = variableDeclaration.Type;

            bool castInlinedExpression = false;
            CommonTypeInfo typeInfo = model.GetTypeInfo(this.declaratorNode.Initializer.Value, cancellationToken);
            ITypeSymbol expressionType = typeInfo.Type;
            ITypeSymbol declaredType = typeInfo.ConvertedType;

            // If the variable is declared with `var' keyword, there is no need to cast the initializer expression within replacement later on
            if (!variableTypeNode.IsVar)
            {
                // If expression's resultant type is different than declared one, cast is obligatory so as not to change type context
                // Considers:
                // float f = 5;
                // Expression type: System.Int32
                // Declared type: System.Single
                if (!expressionType.Equals(declaredType))
                {
                    castInlinedExpression = true;
                }
            }

            VariableInliner visitor = new VariableInliner(model, declaredSymbol, this.declaratorNode, castInlinedExpression, declarationStatement);
            SyntaxNode newBlock = visitor.Visit(block);
            
            SyntaxNode newRoot = root.ReplaceNode(block, newBlock);
           
            return new CodeActionEdit(document.UpdateSyntaxRoot(newRoot));
        }

        public ImageSource Icon
        {
            get { return null; }
        }

        class VariableInliner : SyntaxRewriter
        {
            ISemanticModel model;
            ISymbol declaredSymbol;
            VariableDeclaratorSyntax variableDeclarator;
            ExpressionSyntax expressionSyntax;
            bool castInlinedExpression;
            LocalDeclarationStatementSyntax localDeclarationStatementSyntax;
            ITypeSymbol typeSymbol;

            public VariableInliner(ISemanticModel model, ISymbol declaredSymbol, VariableDeclaratorSyntax variableDeclarator, bool castInlinedExpression, LocalDeclarationStatementSyntax localDeclarationStatementSyntax)
            {
                this.model = model;
                this.declaredSymbol = declaredSymbol;
                this.variableDeclarator = variableDeclarator;
                this.expressionSyntax = variableDeclarator.Initializer.Value;
                this.castInlinedExpression = castInlinedExpression;
                this.localDeclarationStatementSyntax = localDeclarationStatementSyntax;
                this.typeSymbol = model.GetTypeInfo(this.expressionSyntax).ConvertedType;
            }

            public override SyntaxNode VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
            {
                // If this is the declaration statement of inlined variable
                if (node.Equals(this.localDeclarationStatementSyntax))
                {
                    // If the declaration statement contains only one declarator (with inlined variable)
                    // it can be simply removed
                    // Considers:
                    // { int a = 1; } -> {}
                    if (node.Declaration.Variables.Count == 1)
                    {
                        return null;
                    }
                    else
                    {
                        // Process declaration children (possibly replace identifiers in other declarators)
                        // Considers:
                        // int a = 2, b = a; -> int b = 2;
                        LocalDeclarationStatementSyntax newDeclaration = (LocalDeclarationStatementSyntax)base.VisitLocalDeclarationStatement(node);

                        // Now that I have processed children, I can process the declaration itself by removing the unneeded declarator

                        // Get the inlined variable declarator from new declaration statement
                        VariableDeclaratorSyntax newInlinedVariableDeclarator = newDeclaration.Declaration.Variables
                                                                                    .Where(n => n.Identifier.ValueText == this.variableDeclarator.Identifier.ValueText)
                                                                                    .Single();

                        // Remove declarator from the declaration
                        newDeclaration = newDeclaration.ReplaceNode(newInlinedVariableDeclarator, null);

                        return newDeclaration;
                    }
                }
                else
                {
                    return base.VisitLocalDeclarationStatement(node);
                }
            }

            public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
            {
                ISymbol symbol = this.model.GetSymbolInfo(node).Symbol;

                if (symbol != null)
                {
                    // If this identifier expression is the variable we are looking for
                    if (symbol.Equals(this.declaredSymbol))
                    {
                        ExpressionSyntax newNode = this.expressionSyntax;

                        SyntaxNode parentNode = node.Parent;

                        if (newNode.Kind != SyntaxKind.ParenthesizedExpression)
                        {
                            // Surround with parenthesis if required
                            // Considers:
                            // int a = b = 1; a.ToString(); -> (b = 1).ToString();
                            // float a = 2; float b = 5 / a; -> float b = 5 / (float)2;
                            // int a = 1 + 2; int b = a * 3; -> int b = (1 + 2) * 3;
                            // int a = c == true ? 1 : 4; int b = a + 3; -> int b = (c == true ? 1 : 4) + 3;
                            // Visual Studio Roslyn Refactoring Extension does not properly handle those samples

                            if ((this.expressionSyntax is BinaryExpressionSyntax || this.expressionSyntax is ConditionalExpressionSyntax)
                                && (parentNode is BinaryExpressionSyntax || this.castInlinedExpression || parentNode.Kind == SyntaxKind.MemberAccessExpression)
                                )
                            {
                                newNode = Syntax.ParenthesizedExpression(this.expressionSyntax);
                            }
                        }

                        // Cast expression to preserve type semantics if necessary
                        if (this.castInlinedExpression)
                        {
                            TypeSyntax typeSyntax = Syntax.ParseTypeName(this.typeSymbol.ToMinimalDisplayString(node.GetLocation(), model));
                            newNode = Syntax.CastExpression(typeSyntax, newNode);

                            // If inlined expression is used in a context with operator of higher precedence than cast operator, then again parenthesis is needed
                            // Considers:
                            // a[0]  ->  >(< (IList<int>)a >)< [0]  , not (IList<int>)a[0]
                            // a.b   ->  >(< (A)a >)< .b            , not (A)a.b
                            // a++   ->  >(< (A)a >)< ++            , not (A)a++
                            if (parentNode.Kind == SyntaxKind.ElementAccessExpression
                                || parentNode.Kind == SyntaxKind.MemberAccessExpression
                                || parentNode.Kind == SyntaxKind.PostDecrementExpression
                                || parentNode.Kind == SyntaxKind.PostIncrementExpression)
                            {
                                newNode = Syntax.ParenthesizedExpression(newNode);
                            }
                        }

                        return CodeAnnotations.Formatting.AddAnnotationTo(newNode);
                    }
                }

                return node;
            }
        }
    }
}
