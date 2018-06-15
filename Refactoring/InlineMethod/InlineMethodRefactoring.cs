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
    [ExportCodeRefactoringProvider("InlineMethod", LanguageNames.CSharp)]
    public class InlineMethodRefactoring : ICodeRefactoringProvider
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
            
            // Verify is the selected token an identifier of a method
            if (token.Kind == SyntaxKind.IdentifierToken && parentNode.Kind == SyntaxKind.IdentifierName && token.Span.Start <= textSpan.End && textSpan.End <= token.Span.End)
            {
                IdentifierNameSyntax identifier = (IdentifierNameSyntax)parentNode;

                InvocationExpressionSyntax invocationExpression = identifier.FirstAncestorOrSelf<InvocationExpressionSyntax>();
                if (invocationExpression == null || invocationExpression.HasDiagnostics)
                {
                    return null;
                }
                
                ISemanticModel model = document.GetSemanticModel(cancellationToken);

                ISymbol methodSymbol = model.GetSymbolInfo(invocationExpression.Expression, cancellationToken).Symbol;
                if (methodSymbol == null)
                {
                    return null;
                }

                // Check is the method defined in source, so that its body can be read and inlined
                CommonLocation methodDeclarationLocation = methodSymbol.Locations.First();
                if (methodDeclarationLocation == null || !methodDeclarationLocation.IsInSource)
                {
                    return null;
                }

                // Get method declaration based on location
                int position = methodDeclarationLocation.SourceSpan.Start;
                MethodDeclarationSyntax methodDeclaration = methodDeclarationLocation.SourceTree.GetRoot().FindToken(position).Parent.FirstAncestorOrSelf<MethodDeclarationSyntax>();
                if (methodDeclaration == null)
                {
                    return null;
                }
                
                BlockSyntax methodBody = methodDeclaration.Body;
                
                // Method can be inlined only if it has only one statement, which is return statement
                if (methodBody.Statements.Count != 1 || methodBody.Statements[0].Kind != SyntaxKind.ReturnStatement)
                {
                    return null;
                }

                ReturnStatementSyntax returnStatement = (ReturnStatementSyntax)methodBody.Statements[0];
                if (returnStatement == null || returnStatement.Expression == null)
                {
                    return null;
                }

                // If inlined method's invocation expression is the only expression in statement (so its direct parent is ExpressionStatement)
                // then the expression in ReturnStatement should be one of: invocation, object creation, increment, decrement, assignment (or complex assignment).
                if (invocationExpression.Parent != null && invocationExpression.Parent.Kind == SyntaxKind.ExpressionStatement)
                {
                    if (!IsIndependentExpression(returnStatement.Expression))
                    {
                        return null;
                    }
                }

                // If method is polymorphic, then it should not be inlined. Late binding does not take place.
                MethodSymbol declaredMethodSymbol = (MethodSymbol)model.GetDeclaredSymbol(methodDeclaration);
                if (declaredMethodSymbol.OverriddenMethod != null
                    || declaredMethodSymbol.MethodKind == MethodKind.ExplicitInterfaceImplementation
                    || declaredMethodSymbol.IsVirtual
                    || declaredMethodSymbol.IsOverride)
                {
                    return null;
                }

                var analysis = model.AnalyzeStatementDataFlow(returnStatement);
                                
                // If parameter is assigned value, it must be either `out' or `ref' to preserve semantics when method is inlined
                // That is, if parameter is modified, the change is reflected before inlining and after inlining

                foreach (var parameter in methodDeclaration.ParameterList.Parameters)
                {
                    ISymbol parameterSymbol = model.GetDeclaredSymbol(parameter, cancellationToken);

                    // Check is the parameter changed in method's body
                    if (analysis.WrittenInside.Contains(parameterSymbol))
                    {
                        // Verify if the modified parameter defined with either `ref' or `out' keyword
                        if (!parameter.Modifiers.Any(n => n.Kind == SyntaxKind.OutKeyword || n.Kind == SyntaxKind.RefKeyword))
                        {
                            return null;
                        }
                    }
                }

                return new CodeRefactoring(
                            new[] { new InlineMethodAction(document, invocationExpression, methodDeclaration, returnStatement.Expression) }
                            , invocationExpression.Span);
            }

            return null;
        }

        private bool IsIndependentExpression(ExpressionSyntax expression)
        {
            SyntaxKind kind = expression.Kind;
            return kind == SyntaxKind.AssignExpression
                   || kind == SyntaxKind.AddAssignExpression
                   || kind == SyntaxKind.AndAssignExpression
                   || kind == SyntaxKind.ExclusiveOrAssignExpression
                   || kind == SyntaxKind.SubtractAssignExpression
                   || kind == SyntaxKind.MultiplyAssignExpression
                   || kind == SyntaxKind.ModuloAssignExpression
                   || kind == SyntaxKind.DivideAssignExpression
                   || kind == SyntaxKind.LeftShiftAssignExpression
                   || kind == SyntaxKind.RightShiftAssignExpression
                   || kind == SyntaxKind.OrAssignExpression
                   || kind == SyntaxKind.InvocationExpression
                   || kind == SyntaxKind.ObjectCreationExpression
                   || kind == SyntaxKind.PreIncrementExpression
                   || kind == SyntaxKind.PostIncrementExpression
                   || kind == SyntaxKind.PreDecrementExpression
                   || kind == SyntaxKind.PostDecrementExpression;
        }
    }
}
