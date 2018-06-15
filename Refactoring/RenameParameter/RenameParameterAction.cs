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
    class RenameParameterAction : ICodeAction
    {
        IDocument document;
        ISymbol symbol;
        String newName;

        public RenameParameterAction(IDocument document, ISymbol symbol, String newName)
        {
            this.document = document;
            this.symbol = symbol;
            this.newName = newName;
        }

        public string Description
        {
            get
            {
                return String.Format("Rename parameter `{0}' to `{1}'", this.symbol.Name, this.newName);
            }
        }

        public CodeActionEdit GetEdit(CancellationToken cancellationToken)
        {
            SyntaxNode root = (SyntaxNode)this.document.GetSyntaxRoot(cancellationToken);
            ISemanticModel model = this.document.GetSemanticModel(cancellationToken);
                        
            // Run visitor
            ParameterNameRewriter parameterNameRewriter = new ParameterNameRewriter(model, this.symbol, newName, cancellationToken);
            SyntaxNode newRoot = parameterNameRewriter.Visit(root);

            return new CodeActionEdit(document.UpdateSyntaxRoot(newRoot));
        }

        public ImageSource Icon
        {
            get { return null; }
        }

        class ParameterNameRewriter : SyntaxRewriter
        {
            ISemanticModel model;
            ISymbol parameterSymbol;
            string oldName;
            string newName;
            CancellationToken cancellationToken;

            public ParameterNameRewriter(ISemanticModel model, ISymbol parameterSymbol, string newName, CancellationToken cancellationToken)
            {
                this.model = model;
                this.parameterSymbol = parameterSymbol;
                this.oldName = this.parameterSymbol.Name;
                this.newName = newName;
                this.cancellationToken = cancellationToken;
            }

            public override SyntaxNode VisitParameter(ParameterSyntax node)
            {
                // Get declared symbol
                ISymbol referencedSymbol = this.model.GetDeclaredSymbol(node, this.cancellationToken);

                if (referencedSymbol != null)
                {
                    // Verify does the name refer to searched symbol
                    if (referencedSymbol.Equals(this.parameterSymbol))
                    {
                        // Rename the identifier token
                        SyntaxToken identifierToken = node.Identifier;
                        return node.WithIdentifier(Syntax.Identifier(identifierToken.LeadingTrivia,
                                                                     this.newName,
                                                                     identifierToken.TrailingTrivia));
                    }
                }

                // Perform default behavior
                return base.VisitParameter(node);
            }
            
            public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
            {
                // This method handles NameColonSyntax as well as regular references to parameter
                // Considers:
                // foo(a: 1); -> foo(b: 1);
                // int c = a; -> int c = b;

                // Get related symbol
                CommonSymbolInfo symbolInfo = this.model.GetSymbolInfo(node, this.cancellationToken);
                ISymbol referencedSymbol = symbolInfo.Symbol;

                if (referencedSymbol != null)
                {
                    // Verify does the name refer to searched symbol
                    if (referencedSymbol.Equals(this.parameterSymbol))
                    {
                        // Rename the identifier token
                        SyntaxToken identifierToken = node.Identifier;
                        return node.WithIdentifier(Syntax.Identifier(identifierToken.LeadingTrivia,
                                                                     this.newName,
                                                                     identifierToken.TrailingTrivia));
                    }
                }       

                // Perform default behavior
                return base.VisitIdentifierName(node);
            }
        }
    }
}
