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
    [ExportCodeRefactoringProvider("RenameLocal", LanguageNames.CSharp)]
    public class RenameLocalRefactoring : ICodeRefactoringProvider
    {
        public CodeRefactoring GetRefactoring(IDocument document, TextSpan textSpan, CancellationToken cancellationToken)
        {
            string newName = "newVariableName";
            SyntaxNode root = (SyntaxNode)document.GetSyntaxRoot(cancellationToken);
            SyntaxToken token = root.FindToken(textSpan.Start, findInsideTrivia: true);

            // Verify is the selected token an identifier
            if (token.Kind == SyntaxKind.IdentifierToken && token.Span.Start <= textSpan.End && textSpan.End <= token.Span.End)
            {
                ISemanticModel model = document.GetSemanticModel(cancellationToken);
                ISymbol symbol = null;
                CommonSyntaxNode parentNode = token.Parent;

                // Local variable node-reference can be either IdentifierName node or VariableDeclarator node
                if (parentNode is IdentifierNameSyntax)
                {
                    CommonSymbolInfo symbolInfo = model.GetSymbolInfo(parentNode, cancellationToken);
                    symbol = symbolInfo.Symbol;
                }
                else if (parentNode is VariableDeclaratorSyntax)
                {
                    symbol = model.GetDeclaredSymbol(parentNode, cancellationToken);
                }
                
                if (symbol == null)
                {
                    return null;
                }

                // Verify is the symbol of local variable kind
                if (symbol.Kind != CommonSymbolKind.Local)
                {
                    return null;
                }

                // Verify is there any other local variable of the same name
                IList<ISymbol> visibleSymbols = model.LookupSymbols(token.Span.Start, name: newName, container: symbol.ContainingType);

                bool isNameReserved = visibleSymbols
                                        .Any(otherSymbol =>
                                              (otherSymbol.Kind == CommonSymbolKind.Local|| otherSymbol.Kind == CommonSymbolKind.Parameter)
                                              && !otherSymbol.Equals(symbol));
                        
                if (!isNameReserved)
                {
                    return new CodeRefactoring(
                        new[] { new RenameLocalAction(document, token, symbol, newName) },
                        token.Span);
                }
            }

            return null;
        }
    }
}
