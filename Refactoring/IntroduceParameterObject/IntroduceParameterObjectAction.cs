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
    class IntroduceParameterObjectAction : ICodeAction
    {
        IDocument document;
        IEnumerable<ParameterSyntax> parameters;

        public IntroduceParameterObjectAction(IDocument document, IEnumerable<ParameterSyntax> parameters)
        {
            this.document = document;
            this.parameters = parameters;
        }

        public string Description
        {
            get
            {
                return String.Format("Introduce parameter object");
            }
        }

        public CodeActionEdit GetEdit(CancellationToken cancellationToken)
        {
            SyntaxNode root = (SyntaxNode)this.document.GetSyntaxRoot(cancellationToken);
            ISemanticModel model = this.document.GetSemanticModel(cancellationToken);

            // Mechanics:
            // (1) Create new type declaration
            // (2) Remove parameters from method declaration and insert ParameterObject instance in place
            // (3) Remove arguments from method invocations and insert ParameterObject creation in place
            // (4) Replace references to parameters with references to properties from ParameterObjectInstance within method body

            const string className = "ParameterObject";
            const string parameterObjectName = "parameterObject";

            // Fetch BaseMethodDeclaration node and its symbol
            BaseMethodDeclarationSyntax methodDeclaration = this.parameters.First().FirstAncestorOrSelf<BaseMethodDeclarationSyntax>();
            ISymbol methodSymbol = model.GetDeclaredSymbol(methodDeclaration);

            // Create declaration of new aggregate class

            SeparatedSyntaxList<ParameterSyntax> ctorParameters = Syntax.SeparatedList<ParameterSyntax>();
            SyntaxList<StatementSyntax> ctorStatements = Syntax.List<StatementSyntax>();
            SyntaxList<MemberDeclarationSyntax> classMembers = Syntax.List<MemberDeclarationSyntax>();
            
            foreach (ParameterSyntax parameter in this.parameters)
            {
                // Get parameter identifier
                SyntaxToken identifier = parameter.Identifier;

                // Get parameter type
                TypeSyntax parameterType = parameter.Type;

                // Create new parameter
                ParameterSyntax ctorParameter = Syntax.Parameter(identifier)
                                                 .WithType(parameterType);

                ctorParameters = ctorParameters.Add(ctorParameter);

                // Create new property to store argument value
                PropertyDeclarationSyntax propertyDeclaration = Syntax.PropertyDeclaration(parameterType, identifier)
                                                                    .WithModifiers(Syntax.TokenList(Syntax.Token(SyntaxKind.PublicKeyword)))
                                                                    .WithTrailingTrivia(Syntax.CarriageReturnLineFeed)
                                                                    .WithAccessorList(
                                                                       Syntax.AccessorList(Syntax.List<AccessorDeclarationSyntax>(
                                                                           Syntax.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                                                                                   .WithSemicolonToken(Syntax.Token(SyntaxKind.SemicolonToken)),
                                                                           Syntax.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                                                                                   .WithSemicolonToken(Syntax.Token(SyntaxKind.SemicolonToken))
                                                                       )).WithTrailingTrivia(Syntax.CarriageReturnLineFeed)
                                                                    );

                classMembers = classMembers.Add(propertyDeclaration);

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

            ConstructorDeclarationSyntax ctorDeclaration = Syntax.ConstructorDeclaration(className)
                                                                .WithModifiers(Syntax.TokenList(Syntax.Token(SyntaxKind.PublicKeyword)))
                                                                .WithBody(Syntax.Block(ctorStatements))
                                                                .WithParameterList(Syntax.ParameterList(ctorParameters));

            classMembers = classMembers.Add(ctorDeclaration);

            ClassDeclarationSyntax classDeclaration = Syntax.ClassDeclaration(className)
                                                           .WithMembers(classMembers)
                                                           .WithLeadingTrivia(Syntax.ElasticCarriageReturnLineFeed)
                                                           .WithTrailingTrivia(Syntax.CarriageReturnLineFeed)
                                                           .WithAdditionalAnnotations(CodeAnnotations.Formatting);


            IntroduceParameterObjectRewriter visitor = new IntroduceParameterObjectRewriter(model, cancellationToken, this.parameters, methodSymbol, parameterObjectName);
            SyntaxNode newRoot = visitor.Visit(root);

            // Add new class declaration to compilation unit
            CompilationUnitSyntax compilationUnit = newRoot.FirstAncestorOrSelf<CompilationUnitSyntax>();
            CompilationUnitSyntax newCompilationUnit = compilationUnit.AddMembers(classDeclaration);
            newRoot = newRoot.ReplaceNode(compilationUnit, newCompilationUnit);

            return new CodeActionEdit(this.document.UpdateSyntaxRoot(newRoot));
        }

        public ImageSource Icon
        {
            get { return null; }
        }

        class IntroduceParameterObjectRewriter : SyntaxRewriter
        {
            ISemanticModel model;
            CancellationToken cancellationToken;
            IEnumerable<ParameterSyntax> parameters;
            ISymbol methodSymbol;
            ParameterListSyntax parameterList;
            string parameterObjectName;

            IList<ParameterSymbol> symbols = new List<ParameterSymbol>();

            public IntroduceParameterObjectRewriter(ISemanticModel model, CancellationToken cancellationToken, IEnumerable<ParameterSyntax> parameters, ISymbol methodSymbol, string parameterObjectName)
            {
                this.model = model;
                this.cancellationToken = cancellationToken;
                this.parameters = parameters;
                this.parameterObjectName = parameterObjectName;
                this.methodSymbol = methodSymbol;

                this.parameterList = this.parameters.First().FirstAncestorOrSelf<ParameterListSyntax>();

                // Get symbols for identification purposes
                foreach (ParameterSyntax parameter in this.parameters)
                {
                    ParameterSymbol symbol = (ParameterSymbol)this.model.GetDeclaredSymbol(parameter, this.cancellationToken);
                    symbols.Add(symbol);
                }
            }

            bool IsCompressedArgument(ArgumentSyntax argument, int order)
            {
                int min = this.parameters.Min(n => this.parameterList.Parameters.IndexOf(n));
                int max = this.parameters.Max(n => this.parameterList.Parameters.IndexOf(n));

                if (argument.NameColon == null)
                {
                    return (min <= order && order <= max);
                }
                else
                {
                    ISymbol symbol = this.model.GetSymbolInfo(argument.NameColon.Identifier, this.cancellationToken).Symbol;
                    return this.symbols.Contains(symbol);
                }
            }

            public override SyntaxNode VisitParameterList(ParameterListSyntax node)
            {
                ParameterListSyntax processedNode = (ParameterListSyntax)base.VisitParameterList(node);

                if (node.Equals(this.parameterList))
                {
                    // Get the lowest index on ParameterList of compressed Parameters
                    // Considers:
                    // void foo(int a, >int b, int c<, int d) -> void foo(int a, ParameterObject o, int d)

                    int order = this.parameters.Min(n => this.parameterList.Parameters.IndexOf(n));

                    // Create new parameter declaration
                    // TODO: hard-coded type name!
                    ParameterSyntax newParameter = Syntax.Parameter(Syntax.Identifier(this.parameterObjectName))
                                                         .WithType(Syntax.ParseTypeName("ParameterObject"))
                                                         .WithAdditionalAnnotations(CodeAnnotations.Formatting);

                    // Insert new parameter's declaration to the list
                    ParameterListSyntax newParameterList = processedNode.WithParameters(processedNode.Parameters.Insert(order, newParameter));

                    return newParameterList;
                }

                return processedNode;
            }

            public override SyntaxNode VisitParameter(ParameterSyntax node)
            {
                if (this.parameters.Contains(node))
                {
                    // Remove parameter
                    return null;
                }
                return base.VisitParameter(node);
            }

            public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
            {
                // Check does invocation refer to updated method
                ISymbol referencedMethod = this.model.GetSymbolInfo(node.Expression, this.cancellationToken).Symbol;

                if (referencedMethod != null && referencedMethod.Equals(this.methodSymbol) && !node.HasDiagnostics)
                {
                    ArgumentListSyntax newArgumentList = CreateNewArgumentList(node.ArgumentList);

                    InvocationExpressionSyntax newInvocationExpression = node.WithArgumentList(newArgumentList);

                    return newInvocationExpression;
                }

                return base.VisitInvocationExpression(node);
            }

            public override SyntaxNode VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
            {
                // Check does invocation refer to updated method
                ISymbol referencedMethod = this.model.GetSymbolInfo(node, this.cancellationToken).Symbol;

                if (referencedMethod != null && referencedMethod.Equals(this.methodSymbol) && !node.HasDiagnostics)
                {
                    ArgumentListSyntax newArgumentList = CreateNewArgumentList(node.ArgumentList);

                    ObjectCreationExpressionSyntax newObjectCreationExpression = node.WithArgumentList(newArgumentList);

                    return newObjectCreationExpression;
                }
                
                return base.VisitObjectCreationExpression(node);
            }
            
            public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
            {
                // Get related symbol
                CommonSymbolInfo symbolInfo = this.model.GetSymbolInfo(node, this.cancellationToken);
                ISymbol referencedSymbol = symbolInfo.Symbol;

                // Do not consider identifiers within NameColonSyntax!
                if (referencedSymbol != null && !(node.Parent is NameColonSyntax))
                {
                    // Verify does the name refer to any of compressed parameters
                    if (this.symbols.Contains(referencedSymbol))
                    {
                        // Replace parameter reference with reference via ParameterObject
                        // Considers:
                        // int a = b; -> int a = parameterObject.b;
                        MemberAccessExpressionSyntax accessExpression = Syntax.MemberAccessExpression(
                                                                             SyntaxKind.MemberAccessExpression,
                                                                             Syntax.IdentifierName(this.parameterObjectName),
                                                                             Syntax.IdentifierName(node.Identifier.WithLeadingTrivia())
                                                                           )
                                                                           .WithLeadingTrivia(node.GetLeadingTrivia())
                                                                           .WithTrailingTrivia(node.GetTrailingTrivia());

                        return accessExpression;
                    }
                }

                // Perform default behavior
                return base.VisitIdentifierName(node);
            }

            private ArgumentListSyntax CreateNewArgumentList(ArgumentListSyntax argumentList)
            {
                // Create ObjectCreationExpression for ParameterObject
                // Considers:
                // foo(a, >b, c,< d) -> foo(a, new ParameterObject(b, c), d)

                // Split arguments into two lists: one for ObjectCreationExpression and another for InvocationExpression
                SeparatedSyntaxList<ArgumentSyntax> ctorArguments = Syntax.SeparatedList<ArgumentSyntax>();
                SeparatedSyntaxList<ArgumentSyntax> invocationArguments = Syntax.SeparatedList<ArgumentSyntax>();

                // Note: Pre-order visiting argument, as it may also contain method call
                // Considers:
                // foo(a, b, foo(1, 2, 3, 4), d) -> foo(a, new ParameterObject(b, foo(1, new ParameterObject(2, 3), 4)), d) 

                for (int i = 0; i < argumentList.Arguments.Count; ++i)
                {
                    ArgumentSyntax argument = argumentList.Arguments[i];

                    if (IsCompressedArgument(argument, i))
                    {
                        ArgumentSyntax processedArgument = (ArgumentSyntax)base.VisitArgument(argument);
                        ctorArguments = ctorArguments.Add(processedArgument);
                    }
                }

                // TODO: Hard-coded type name!
                ObjectCreationExpressionSyntax creationExpression = Syntax.ObjectCreationExpression(Syntax.ParseTypeName("ParameterObject"))
                                                                          .WithArgumentList(Syntax.ArgumentList(ctorArguments));

                bool isParameterObjectAdded = false;

                for (int i = 0; i < argumentList.Arguments.Count; ++i)
                {
                    ArgumentSyntax argument = argumentList.Arguments[i];

                    if (!IsCompressedArgument(argument, i))
                    {
                        ArgumentSyntax processedArgument = (ArgumentSyntax)base.VisitArgument(argument);
                        invocationArguments = invocationArguments.Add(processedArgument);
                    }
                    else if (!isParameterObjectAdded)
                    {
                        // The first occurrence of compressed argument should be replaced with invocation expression
                        ArgumentSyntax processedArgument = (ArgumentSyntax)base.VisitArgument(argument);

                        ArgumentSyntax objectCreationArgument = processedArgument.WithExpression(creationExpression)
                                                                                 .WithAdditionalAnnotations(CodeAnnotations.Formatting);

                        if (argument.NameColon != null)
                        {
                            objectCreationArgument = objectCreationArgument.WithNameColon(Syntax.NameColon(Syntax.IdentifierName(this.parameterObjectName)));
                        }

                        invocationArguments = invocationArguments.Add(objectCreationArgument);

                        isParameterObjectAdded = true;
                    }
                }

                ArgumentListSyntax newArgumentList = Syntax.ArgumentList(invocationArguments);

                return newArgumentList;
            }
        }
    }
}
