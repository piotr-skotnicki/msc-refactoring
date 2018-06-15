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
    class GenerateClassFromUsageAction : ICodeAction
    {
        IDocument document;
        ObjectCreationExpressionSyntax expression;
        string className;

        public GenerateClassFromUsageAction(IDocument document, ObjectCreationExpressionSyntax expression, string className)
        {
            this.document = document;
            this.expression = expression;
            this.className = className;
        }

        public string Description
        {
            get
            {
                return String.Format("Generate class `{0}'", this.className);
            }
        }

        public CodeActionEdit GetEdit(CancellationToken cancellationToken)
        {
            SyntaxNode root = (SyntaxNode)this.document.GetSyntaxRoot(cancellationToken);
            ISemanticModel model = this.document.GetSemanticModel(cancellationToken);

            TypeSyntax classTypeSyntax = this.expression.Type;

            // TODO: extract container info and propose solutions

            IDictionary<TypeSymbol, String> typeParametersMap = new Dictionary<TypeSymbol, String>();
            
            SeparatedSyntaxList<ParameterSyntax> ctorParameters = Syntax.SeparatedList<ParameterSyntax>();
            SeparatedSyntaxList<TypeParameterSyntax> typeParameters = Syntax.SeparatedList<TypeParameterSyntax>();
            SyntaxList<StatementSyntax> ctorStatements = Syntax.List<StatementSyntax>();
            SyntaxList<MemberDeclarationSyntax> classMembers = Syntax.List<MemberDeclarationSyntax>();

            if (classTypeSyntax.Kind == SyntaxKind.IdentifierName)
            {

            }
            else if (classTypeSyntax.Kind == SyntaxKind.GenericName)
            {
                const string paramNames = "TUV";
                int index = 0;
                GenericNameSyntax genericName = (GenericNameSyntax)classTypeSyntax;
                TypeArgumentListSyntax typeArgumentList = genericName.TypeArgumentList;
                foreach (TypeSyntax typeArgument in typeArgumentList.Arguments)
                {
                    string typeParameterName = new string(paramNames[index % paramNames.Length], index / paramNames.Length + 1);
                    TypeParameterSyntax typeParameter = Syntax.TypeParameter(typeParameterName);
                    typeParametersMap.Add(model.GetTypeInfo(typeArgument, cancellationToken).Type as TypeSymbol, typeParameterName);
                    typeParameters = typeParameters.Add(typeParameter);
                    index += 1;
                }
            }
            else if (classTypeSyntax.Kind == SyntaxKind.QualifiedName)
            {

            }

            // Add comment
            SyntaxTrivia commentTrivia = Syntax.Comment("// TODO: Complete member initialization");
            ctorStatements = ctorStatements.Add(Syntax.EmptyStatement().WithLeadingTrivia(commentTrivia));

            int order = 0;
            ArgumentListSyntax argumentList = this.expression.ArgumentList;
            foreach (ArgumentSyntax argument in argumentList.Arguments)
            {
                // Generate new identifier
                SyntaxToken identifier = Syntax.Identifier(String.Format("param{0}", ++order));

                // For named arguments use the associated name
                // Considers:
                // new foo(a: 2); -> foo(int a) { this.a = a; }
                if (argument.NameColon != null)
                {
                    identifier = argument.NameColon.Identifier.Identifier;
                }

                // Determine argument type
                TypeSymbol typeSymbol = model.GetTypeInfo(argument.Expression, cancellationToken).Type as TypeSymbol;
                TypeSyntax typeSyntax = null;

                // Check if the type of parameter specified as template type
                // Considers:
                // new A<int>(1) -> class A<T> { A(>T< param) {} }

                if (typeParametersMap.ContainsKey(typeSymbol))
                {
                    typeSyntax = Syntax.ParseTypeName(typeParametersMap[typeSymbol]);
                }
                else
                {
                    typeSyntax = Syntax.ParseTypeName(typeSymbol.ToDisplayString());
                }

                // Create new parameter
                ParameterSyntax parameter = Syntax.Parameter(identifier)
                                                 .WithType(typeSyntax);

                ctorParameters = ctorParameters.Add(parameter);

                // Create new field to store parameter value
                VariableDeclaratorSyntax variableDeclarator = Syntax.VariableDeclarator(identifier);

                VariableDeclarationSyntax variableDeclaration = Syntax.VariableDeclaration(typeSyntax)
                                                                     .WithVariables(Syntax.SeparatedList<VariableDeclaratorSyntax>(variableDeclarator));

                FieldDeclarationSyntax fieldDeclaration = Syntax.FieldDeclaration(variableDeclaration)
                                                               .WithModifiers(Syntax.TokenList(Syntax.Token(SyntaxKind.PrivateKeyword)))
                                                               .WithTrailingTrivia(Syntax.EndOfLine(""));

                classMembers = classMembers.Add(fieldDeclaration);

                // Create ctor initializing statement:
                // Considers:
                // this.a = a;
                ExpressionStatementSyntax statement = Syntax.ExpressionStatement(
                                                           Syntax.BinaryExpression(SyntaxKind.AssignExpression,
                                                                                   Syntax.MemberAccessExpression(SyntaxKind.MemberAccessExpression,
                                                                                           Syntax.ThisExpression(),
                                                                                           Syntax.IdentifierName(identifier)),
                                                                                   Syntax.IdentifierName(identifier)
                                                                                ));

                ctorStatements = ctorStatements.Add(statement);
            }

            ConstructorDeclarationSyntax ctorDeclaration = Syntax.ConstructorDeclaration(this.className)
                                                                .WithModifiers(Syntax.TokenList(Syntax.Token(SyntaxKind.PublicKeyword)))
                                                                .WithBody(Syntax.Block(ctorStatements))
                                                                .WithParameterList(Syntax.ParameterList(ctorParameters));

            classMembers = classMembers.Add(ctorDeclaration);

            ClassDeclarationSyntax classDeclaration = Syntax.ClassDeclaration(this.className)
                                                           .WithMembers(classMembers)
                                                           .WithTrailingTrivia(Syntax.ElasticCarriageReturnLineFeed)
                                                           .WithAdditionalAnnotations(CodeAnnotations.Formatting);

            if (typeParameters.Count > 0)
            {
                classDeclaration = classDeclaration.WithTypeParameterList(Syntax.TypeParameterList(typeParameters));
            }

            CompilationUnitSyntax compilationUnit = this.expression.FirstAncestorOrSelf<CompilationUnitSyntax>();
            CompilationUnitSyntax newCompilationUnit = compilationUnit.AddMembers(classDeclaration);

            SyntaxNode newRoot = root.ReplaceNode(compilationUnit, newCompilationUnit);

            return new CodeActionEdit(document.UpdateSyntaxRoot(newRoot));
        }

        public ImageSource Icon
        {
            get { return null; }
        }
    }
}
