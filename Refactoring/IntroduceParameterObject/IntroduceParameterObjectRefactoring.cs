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
    [ExportCodeRefactoringProvider("IntroduceParameterObject", LanguageNames.CSharp)]
    public class IntroduceParameterObjectRefactoring : ICodeRefactoringProvider
    {
        public CodeRefactoring GetRefactoring(IDocument document, TextSpan textSpan, CancellationToken cancellationToken)
        {
            SyntaxNode root = (SyntaxNode)document.GetSyntaxRoot(cancellationToken);
            ISemanticModel model = document.GetSemanticModel(cancellationToken);

            // Verify that the selection is only within ParameterList
            IEnumerable<ParameterSyntax> selectedParameters = root.DescendantNodes(textSpan).OfType<ParameterSyntax>();

            if (selectedParameters.Count() == 0)
            {
                return null;
            }

            // Get arbitrary node for further processing
            ParameterSyntax selectedNode = selectedParameters.FirstOrDefault();

            if (selectedNode == null)
            {
                return null;
            }

            // Get ParameterList node
            ParameterListSyntax parameterList = selectedNode.FirstAncestorOrSelf<ParameterListSyntax>();

            if (parameterList == null)
            {
                return null;
            }
            
            // All selected nodes should have the same parent which is ParameterList
            bool haveSameParent = selectedParameters.All(
                                    (n) =>
                                    {
                                        ParameterListSyntax parent = n.FirstAncestorOrSelf<ParameterListSyntax>();
                                        return parent != null && parent.Equals(parameterList);
                                    });

            if (!haveSameParent)
            {
                return null;
            }

            // Parameters that are defined within OperatorDeclaration should not be compressed to ParameterObject
            if (parameterList.FirstAncestorOrSelf<ConversionOperatorDeclarationSyntax>() != null
                || parameterList.FirstAncestorOrSelf<OperatorDeclarationSyntax>() != null)
            {
                return null;
            }

            // Parameters that are defined within Lambda, delegate's delaration or anonymous methods should not be compressed to ParameterObject
            if (parameterList.FirstAncestorOrSelf<ParenthesizedLambdaExpressionSyntax>() != null
                || parameterList.FirstAncestorOrSelf<SimpleLambdaExpressionSyntax>() != null
                || parameterList.FirstAncestorOrSelf<DelegateDeclarationSyntax>() != null
                || parameterList.FirstAncestorOrSelf<AnonymousMethodExpressionSyntax>() != null)
            {
                return null;
            }

            // Parameters that are either `out' or `ref' cannot be compressed,
            // because C# is not able to store pointers to them in a parameter object
            if (selectedParameters.Any(n => n.Modifiers.Any(m => m.Kind == SyntaxKind.RefKeyword || m.Kind == SyntaxKind.OutKeyword)))
            {
                return null;
            }

            // Get parent declaration: Method/Ctor
            BaseMethodDeclarationSyntax baseMethodDeclaration = parameterList.FirstAncestorOrSelf<BaseMethodDeclarationSyntax>();

            if (baseMethodDeclaration == null)
            {
                return null;
            }

            ISymbol baseMethodSymbol = model.GetDeclaredSymbol(baseMethodDeclaration);

            if (baseMethodSymbol == null)
            {
                return null;
            }

            if (baseMethodSymbol is MethodSymbol)
            {
                MethodSymbol methodSymbol = (MethodSymbol)baseMethodSymbol;

                // Check is the method an implementation of the a super class method
                // If so, the parameters cannot be compressed, as they are parts of polymorphic signature
                if (methodSymbol.OverriddenMethod != null)
                {
                    return null;
                }

                // TODO: This does not work for implicitly implemented interface member
                if (methodSymbol.MethodKind == MethodKind.ExplicitInterfaceImplementation)
                {
                    return null;
                }
            }
            
            return new CodeRefactoring(new[] { new IntroduceParameterObjectAction(document, selectedParameters) });
        }
    }
}
