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
    [ExportCodeRefactoringProvider("MakeSingleton", LanguageNames.CSharp)]
    public class MakeSingletonRefactoring : ICodeRefactoringProvider
    {
        public CodeRefactoring GetRefactoring(IDocument document, TextSpan textSpan, CancellationToken cancellationToken)
        {
            SyntaxNode root = (SyntaxNode)document.GetSyntaxRoot(cancellationToken);
            SyntaxToken token = root.FindToken(textSpan.Start, findInsideTrivia: true);

            SyntaxNode parentNode = token.Parent;

            if (parentNode == null)
            {
                return null;
            }

            // Verify is the selected token an identifier within class declaration
            if (token.Kind == SyntaxKind.IdentifierToken && parentNode.Kind == SyntaxKind.ClassDeclaration && token.Span.Start <= textSpan.End && textSpan.End <= token.Span.End)
            {
                ClassDeclarationSyntax classDeclarationNode = (ClassDeclarationSyntax)parentNode;
                ISemanticModel model = document.GetSemanticModel(cancellationToken);

                // Get symbol for the class
                ISymbol classSymbol = (Symbol)model.GetDeclaredSymbol(classDeclarationNode);
                if (classSymbol == null)
                {
                    return null;
                }

                // Take only instance (non-static) constructors
                var constructorDeclarations = classDeclarationNode.DescendantNodes()
                                                .OfType<ConstructorDeclarationSyntax>()
                                                .Where(n => !n.Modifiers.Any(SyntaxKind.StaticKeyword));

                // Class can be converted to Singleton if it has default instance ctor, or one ctor that has no parameters
                // Note: static constructors shall not be taken into account!

                if (constructorDeclarations.Count() == 0)
                {
                    return new CodeRefactoring(
                                    new[] { new MakeSingletonAction(document, classSymbol, classDeclarationNode) }
                                    , classDeclarationNode.Identifier.Span);
                }
                else if (constructorDeclarations.Count() == 1)
                {
                    ConstructorDeclarationSyntax constructor = constructorDeclarations.First();

                    if (constructor.ParameterList.Parameters.Count == 0)
                    {
                        return new CodeRefactoring(
                                        new[] { new MakeSingletonAction(document, classSymbol, classDeclarationNode) }
                                        , classDeclarationNode.Identifier.Span);
                    }
                }
            }

            return null;
        }
    }
}
