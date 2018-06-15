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
    [ExportCodeRefactoringProvider("EncapsulateField", LanguageNames.CSharp)]
    public class EncapsulateFieldRefactoring : ICodeRefactoringProvider
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

            // Verify is the selected token an identifier within field declaration
            if (token.Kind == SyntaxKind.IdentifierToken && parentNode.Kind == SyntaxKind.VariableDeclarator && token.Span.Start <= textSpan.End && textSpan.End <= token.Span.End)
            {
                VariableDeclaratorSyntax variableDeclarator = (VariableDeclaratorSyntax)parentNode;

                FieldDeclarationSyntax fieldDeclaration = parentNode.FirstAncestorOrSelf<FieldDeclarationSyntax>();

                // Skip if field has diagnostics
                if (fieldDeclaration == null || fieldDeclaration.HasDiagnostics)
                {
                    return null;
                }

                ISemanticModel model = document.GetSemanticModel(cancellationToken);

                ISymbol declaredSymbol = model.GetDeclaredSymbol(variableDeclarator);

                ClassDeclarationSyntax typeDeclaration = fieldDeclaration.FirstAncestorOrSelf<ClassDeclarationSyntax>();
                if (typeDeclaration == null)
                {
                    return null;
                }

                // Verify is there already a getter property that returns the value of field
                if (typeDeclaration.ChildNodes().OfType<PropertyDeclarationSyntax>().Any(n => IsGetterProperty(n, declaredSymbol, model)))
                {
                    return null;
                }

                // Verify is there already a getter method that returns the value of field
                if (typeDeclaration.ChildNodes().OfType<MethodDeclarationSyntax>().Any(n => IsGetterMethod(n, declaredSymbol, model)))
                {
                    return null;
                }

                return new CodeRefactoring(
                                new[] { new EncapsulateFieldAction(document, declaredSymbol, fieldDeclaration, variableDeclarator) }
                                , variableDeclarator.Identifier.Span);
            }

            return null;
        }

        bool IsGetterProperty(PropertyDeclarationSyntax node, ISymbol symbol, ISemanticModel model)
        {
            return node.AccessorList.Accessors.Any(n => IsGetterAccessor(n, symbol, model));
        }

        bool IsGetterAccessor(AccessorDeclarationSyntax node, ISymbol symbol, ISemanticModel model)
        {
            if (node.Kind != SyntaxKind.GetAccessorDeclaration)
            {
                return false;
            }

            BlockSyntax body = node.Body;
            if (body == null || body.Statements == null || body.Statements.Count != 1)
            {
                return false;
            }

            if (body.Statements[0].Kind != SyntaxKind.ReturnStatement)
            {
                return false;
            }

            ReturnStatementSyntax returnStatement = (ReturnStatementSyntax)body.Statements[0];

            ISymbol returnedSymbol = model.GetSymbolInfo(returnStatement.Expression).Symbol;
            if (returnedSymbol == null)
            {
                return false;
            }

            return returnedSymbol.Equals(symbol);
        }

        bool IsGetterMethod(MethodDeclarationSyntax node, ISymbol symbol, ISemanticModel model)
        {
            BlockSyntax body = node.Body;
            if (body == null || body.Statements == null || body.Statements.Count != 1)
            {
                return false;
            }

            if (body.Statements[0].Kind != SyntaxKind.ReturnStatement)
            {
                return false;
            }

            ReturnStatementSyntax returnStatement = (ReturnStatementSyntax)body.Statements[0];

            ISymbol returnedSymbol = model.GetSymbolInfo(returnStatement.Expression).Symbol;
            if (returnedSymbol == null)
            {
                return false;
            }

            return returnedSymbol.Equals(symbol);
        }
    }
}
