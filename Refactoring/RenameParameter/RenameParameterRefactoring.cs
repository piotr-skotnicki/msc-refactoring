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
    [ExportCodeRefactoringProvider("RenameParameter", LanguageNames.CSharp)]
    public class RenameParameterRefactoring : ICodeRefactoringProvider
    {
        public CodeRefactoring GetRefactoring(IDocument document, TextSpan textSpan, CancellationToken cancellationToken)
        {
            const string newName = "newParameterName";
            SyntaxNode root = (SyntaxNode)document.GetSyntaxRoot(cancellationToken);

            // If nothing selected, only cursor placed, increase the ending position by one
            if (textSpan.Length == 0)
            {
                textSpan = new TextSpan(textSpan.Start, 1);
            }

            // Get nodes for highlighted code
            IEnumerable<ParameterSyntax> selectedNodes = root.DescendantNodes(textSpan).OfType<ParameterSyntax>();
            
            // Select the node that spans the selected text most
            // Note: Here I have reversed the ranges ! node.Span.Contains, not textSpan.Contains
            ParameterSyntax selectedNode = null;
            int bestSpan = -1;
            foreach (var node in selectedNodes)
            {
                if (node.Span.Contains(textSpan))
                {
                    int spanWidth = node.Span.Intersection(textSpan).Value.Length;
                    if (spanWidth > bestSpan)
                    {
                        bestSpan = spanWidth;
                        selectedNode = node;
                    }
                }
            }
            
            if (selectedNode == null)
            {
                return null;
            }

            ParameterSyntax parameter = selectedNode;

            ISemanticModel model = document.GetSemanticModel(cancellationToken);
                
            // Parameter token's parant should be of Parameter kind
            ISymbol symbol = model.GetDeclaredSymbol(parameter);

            if (symbol == null)
            {
                return null;
            }

            // Verify is the symbol of Parameter kind
            if (symbol.Kind != CommonSymbolKind.Parameter)
            {
                return null;
            }

            BaseMethodDeclarationSyntax baseMethodDeclaration = parameter.FirstAncestorOrSelf<BaseMethodDeclarationSyntax>();

            if (baseMethodDeclaration == null)
            {
                return null;
            }

            // Verify is there any other local symbol of the same name within method scope
            IList<ISymbol> visibleSymbols = model.LookupSymbols(baseMethodDeclaration.Body.Span.Start, name: newName, container: symbol.ContainingType);

            bool isNameReserved = visibleSymbols
                                    .Any(otherSymbol =>
                                          (otherSymbol.Kind == CommonSymbolKind.Local || otherSymbol.Kind == CommonSymbolKind.Parameter)
                                          && !otherSymbol.Equals(symbol));

            if (!isNameReserved)
            {
                return new CodeRefactoring(
                    new[] { new RenameParameterAction(document, symbol, newName) }
                    , parameter.Span);
            }

            return null;
        }
    }
}
