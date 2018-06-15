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
    [ExportCodeRefactoringProvider("PullUpField", LanguageNames.CSharp)]
    public class PullUpFieldRefactoring : ICodeRefactoringProvider
    {
        public CodeRefactoring GetRefactoring(IDocument document, TextSpan textSpan, CancellationToken cancellationToken)
        {
            SyntaxNode root = (SyntaxNode)document.GetSyntaxRoot(cancellationToken);
            ISemanticModel model = document.GetSemanticModel(cancellationToken);

            SyntaxToken token = root.FindToken(textSpan.Start, findInsideTrivia: true);
                        
            // Verify is the selected token an identifier within field declaration
            if (token.Kind == SyntaxKind.IdentifierToken && token.Span.Start <= textSpan.End && textSpan.End <= token.Span.End)
            {
                SyntaxNode parentNode = token.Parent;

                if (parentNode == null)
                {
                    return null;
                }

                FieldDeclarationSyntax fieldDeclaration = parentNode.FirstAncestorOrSelf<FieldDeclarationSyntax>();
                if (fieldDeclaration == null)
                {
                    return null;
                }

                // If the FieldDeclaration has some errors, then no refactoring should take place
                if (fieldDeclaration.HasDiagnostics)
                {
                    return null;
                }

                // Get the container that the field belongs to
                TypeDeclarationSyntax containingType = fieldDeclaration.FirstAncestorOrSelf<TypeDeclarationSyntax>();
                if (containingType == null)
                {
                    return null;
                }

                // Obtain TypeSymbol for class declaration
                ITypeSymbol containingTypeSymbol = model.GetDeclaredSymbol(containingType, cancellationToken) as ITypeSymbol;
                if (containingTypeSymbol == null)
                {
                    return null;
                }
                        
                // Check is there a base class that the field can be moved to
                INamedTypeSymbol baseTypeSymbol = containingTypeSymbol.BaseType;
                if (baseTypeSymbol == null)
                {
                    return null;
                }

                // Check is the class defined in source, so that it can be extended
                CommonLocation baseTypeLocation = baseTypeSymbol.Locations.First();
                if (baseTypeLocation != null && baseTypeLocation.IsInSource)
                {
                    int position = baseTypeLocation.SourceSpan.Start;
                    BaseTypeDeclarationSyntax baseTypeDeclaration = baseTypeLocation.SourceTree.GetRoot().FindToken(position).Parent.FirstAncestorOrSelf<BaseTypeDeclarationSyntax>();
                    if (baseTypeDeclaration == null)
                    {
                        return null;
                    }

                    return new CodeRefactoring(
                                    new[] { new PullUpFieldAction(document, fieldDeclaration, containingType, baseTypeDeclaration) }
                                    , fieldDeclaration.Span);
                }
            }
            
            return null;
        }
    }
}
