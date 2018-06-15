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
    [ExportCodeRefactoringProvider("ReplaceConstructorWithFactoryMethod", LanguageNames.CSharp)]
    public class ReplaceConstructorWithFactoryMethodRefactoring : ICodeRefactoringProvider
    {
        public CodeRefactoring GetRefactoring(IDocument document, TextSpan textSpan, CancellationToken cancellationToken)
        {
            SyntaxNode root = (SyntaxNode)document.GetSyntaxRoot(cancellationToken);
            SyntaxToken token = root.FindToken(textSpan.Start, findInsideTrivia: true);
            
            // Verify is the selected token an identifier
            if (token.Kind == SyntaxKind.IdentifierToken && token.Span.Start <= textSpan.End && textSpan.End <= token.Span.End)
            {
                ISemanticModel model = document.GetSemanticModel(cancellationToken);
                CommonSyntaxNode parentNode = token.Parent;

                // Verify that the selected node is an identifier of constructor
                if (!(parentNode is ConstructorDeclarationSyntax))
                {
                    return null;
                }

                ConstructorDeclarationSyntax ctorDeclaration = (ConstructorDeclarationSyntax)parentNode;

                if (ctorDeclaration.HasDiagnostics)
                {
                    return null;
                }

                // This refactoring does not apply to static constructors
                if (ctorDeclaration.Modifiers.Any(m => m.Kind == SyntaxKind.StaticKeyword))
                {
                    return null;
                }

                // Get the containing type
                ClassDeclarationSyntax typeDeclaration = ctorDeclaration.FirstAncestorOrSelf<ClassDeclarationSyntax>();

                if (typeDeclaration == null)
                {
                    return null;
                }
                                                
                return new CodeRefactoring(
                                new[] { new ReplaceConstructorWithFactoryMethodAction(document, ctorDeclaration, typeDeclaration) },
                                token.Span);
            }

            return null;
        }
    }
}
