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
    class RenameLocalAction : ICodeAction
    {
        IDocument document;
        SyntaxToken token;
        ISymbol symbol;
        String newName;

        public RenameLocalAction(IDocument document, SyntaxToken token, ISymbol symbol, String newName)
        {
            this.document = document;
            this.token = token;
            this.symbol = symbol;
            this.newName = newName;
        }

        public string Description
        {
            get
            {
                return String.Format("Rename local variable `{0}' to `{1}'", this.symbol.Name, this.newName);
            }
        }

        public CodeActionEdit GetEdit(CancellationToken cancellationToken)
        {
            SyntaxNode root = (SyntaxNode)this.document.GetSyntaxRoot(cancellationToken);
            ISemanticModel model = this.document.GetSemanticModel(cancellationToken);

            // Get method/ctor where the local variable is defined in
            BaseMethodDeclarationSyntax declaringMethod = this.token.Parent.FirstAncestorOrSelf<BaseMethodDeclarationSyntax>();

            RenameLocalRewriter visitor = new RenameLocalRewriter(model, this.symbol, this.newName, cancellationToken);

            BaseMethodDeclarationSyntax newDeclaringMethod = (BaseMethodDeclarationSyntax)visitor.Visit(declaringMethod);

            SyntaxNode newRoot = root.ReplaceNode(declaringMethod, newDeclaringMethod);

            return new CodeActionEdit(document.UpdateSyntaxRoot(newRoot));
        }

        public ImageSource Icon
        {
            get { return null; }
        }

        class RenameLocalRewriter : SyntaxRewriter
        {
            ISemanticModel model;
            ISymbol renamedSymbol;
            string newName;
            CancellationToken cancellationToken;

            public RenameLocalRewriter(ISemanticModel model, ISymbol renamedSymbol, string newName, CancellationToken cancellationToken)
            {
                this.model = model;
                this.renamedSymbol = renamedSymbol;
                this.newName = newName;
                this.cancellationToken = cancellationToken;
            }

            public override SyntaxNode VisitVariableDeclarator(VariableDeclaratorSyntax node)
            {
                VariableDeclaratorSyntax processedNode = (VariableDeclaratorSyntax)base.VisitVariableDeclarator(node);
                
                ISymbol declaredSymbol = this.model.GetDeclaredSymbol(node, this.cancellationToken);
                if (this.renamedSymbol.Equals(declaredSymbol))
                {
                    // Update renamed variable declarator.
                    // Considers:
                    // int a = 1; -> int newVariableName = 1;

                    SyntaxToken identifier = node.Identifier;

                    SyntaxToken newIdentifier = Syntax.Identifier(identifier.LeadingTrivia, this.newName, identifier.TrailingTrivia);

                    VariableDeclaratorSyntax newDeclarator = processedNode.WithIdentifier(newIdentifier);

                    return newDeclarator;
                }

                return processedNode;
            }

            public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
            {
                IdentifierNameSyntax processedNode = (IdentifierNameSyntax)base.VisitIdentifierName(node);

                ISymbol symbol = this.model.GetSymbolInfo(node, this.cancellationToken).Symbol;

                // If this is reference to renamed symbol, replace identifier
                if (this.renamedSymbol.Equals(symbol))
                {
                    SyntaxToken identifier = node.Identifier;

                    SyntaxToken newIdentifier = Syntax.Identifier(identifier.LeadingTrivia, this.newName, identifier.TrailingTrivia);

                    IdentifierNameSyntax newIdentifierName = processedNode.WithIdentifier(newIdentifier);

                    return newIdentifierName;
                }
                else if (node.Identifier.ValueText == this.newName)
                {
                    // If the Identifier refers to type name within variable declaration, no action is undertaken
                    // Consider:
                    // newVariableName myVariable; -> type name can remain as is
                    if (node.Ancestors().OfType<VariableDeclarationSyntax>().Any(n => n.Type.DescendantNodesAndSelf().Contains(node)))
                    {
                        return processedNode;
                    }

                    // If it is not a top-most expression of MemberAccessExpression, don't qualify
                    if (node.Ancestors().OfType<MemberAccessExpressionSyntax>().Any(n => n.Name.DescendantNodesAndSelf().Contains(node)))
                    {
                        return processedNode;
                    }

                    ExpressionSyntax qualifier = null;

                    if (symbol.IsStatic)
                    {
                        // If symbol is static, qualify the reference with containing type's name
                        qualifier = Syntax.ParseTypeName(symbol.ContainingType.ToMinimalDisplayString(node.GetLocation(), this.model));
                    }
                    else
                    {
                        // If symbol is instance, qualify the reference with `this' keyword
                        qualifier = Syntax.ThisExpression();
                    }

                    MemberAccessExpressionSyntax memberAccess = Syntax.MemberAccessExpression(SyntaxKind.MemberAccessExpression, qualifier, processedNode);

                    return memberAccess;
                }

                return processedNode;
            }
        }
    }
}
