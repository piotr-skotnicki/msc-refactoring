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
    class RemoveParameterAction : ICodeAction
    {
        IDocument document;
        ParameterSyntax parameterDeclaration;

        public RemoveParameterAction(IDocument document, ParameterSyntax parameterDeclaration)
        {
            this.document = document;
            this.parameterDeclaration = parameterDeclaration;
        }

        public string Description
        {
            get
            {
                return String.Format("Remove parameter `{0}'", this.parameterDeclaration.Identifier.ValueText);
            }
        }

        public CodeActionEdit GetEdit(CancellationToken cancellationToken)
        {
            SyntaxNode root = (SyntaxNode)this.document.GetSyntaxRoot(cancellationToken);
            ISemanticModel model = this.document.GetSemanticModel(cancellationToken);

            ParameterSymbol parameterSymbol = (ParameterSymbol)model.GetDeclaredSymbol(this.parameterDeclaration);

            // Can be either constructor or regular method
            BaseMethodDeclarationSyntax baseMethodDeclaration = this.parameterDeclaration.FirstAncestorOrSelf<BaseMethodDeclarationSyntax>();
            ISymbol baseMethodSymbol = model.GetDeclaredSymbol(baseMethodDeclaration);

            // Get the ordering number of removed parameter
            int order = baseMethodDeclaration.ParameterList.Parameters.IndexOf(this.parameterDeclaration);

            // Run visitor
            RemoveParameterRewriter visitor = new RemoveParameterRewriter(model, baseMethodSymbol, parameterSymbol, order, cancellationToken);
            SyntaxNode newRoot = visitor.Visit(root);

            return new CodeActionEdit(document.UpdateSyntaxRoot(newRoot));
        }

        public ImageSource Icon
        {
            get { return null; }
        }

        class RemoveParameterRewriter : SyntaxRewriter
        {
            ISemanticModel model;
            ISymbol methodSymbol;
            ISymbol parameterSymbol;
            readonly int order;
            CancellationToken cancellationToken;

            public RemoveParameterRewriter(ISemanticModel model, ISymbol methodSymbol, ISymbol parameterSymbol, int order, CancellationToken cancellationToken)
            {
                this.model = model;
                this.methodSymbol = methodSymbol;
                this.parameterSymbol = parameterSymbol;
                this.order = order;
                this.cancellationToken = cancellationToken;
            }

            public override SyntaxNode VisitParameter(ParameterSyntax node)
            {
                // Considers:
                // foo(int a, int b){} -> foo(int b){}

                // If this is the parameter we are looking for, simply remove it
                ParameterSymbol visitedParameter = this.model.GetDeclaredSymbol(node) as ParameterSymbol;
                if (visitedParameter != null && visitedParameter.Equals(this.parameterSymbol))
                {
                    // Performs removal
                    return null;
                }

                return base.VisitParameter(node);
            }

            public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
            {
                // Note: According to specification, argument is either at its position (as declared), or it is a named argument
                // That is, named argument specification can appear only after all fixed arguments have been specified

                // Considers:
                // foo(a: 1, b: 2) -> foo(b: 2)
                // foo(b: 2, a: 1) -> foo(b: 2)
                // foo(1, b: 2) -> foo(b: 2)
                // foo(1, 2) -> foo(2)
                // foo(a: 1, b: foo(a: 1, b: 2))

                // Check does invocation refer to updated method
                ISymbol referencedMethod = this.model.GetSymbolInfo(node.Expression, this.cancellationToken).Symbol;

                if (referencedMethod != null && referencedMethod.Equals(this.methodSymbol))
                {
                    SeparatedSyntaxList<ArgumentSyntax> newArgumentSeparatedList = Syntax.SeparatedList<ArgumentSyntax>();

                    // Check is the removed parameter referenced by name within method invocation, e.g. foo(1, 2, >a:2<)
                    // Note: Must be a direct child! Not nested descendant like foo(a: foo(>a:<1)) !

                    NameColonSyntax namedArgument = null;
                    foreach (ArgumentSyntax argument in node.ArgumentList.Arguments)
                    {
                        namedArgument = argument.ChildNodes()
                                            .OfType<NameColonSyntax>()
                                            .FirstOrDefault(
                                                (n) =>
                                                {
                                                    ISymbol nameSymbol = this.model.GetSymbolInfo(n.Identifier).Symbol;
                                                    return nameSymbol != null && nameSymbol.Equals(this.parameterSymbol);
                                                });
                        if (namedArgument != null)
                        {
                            break;
                        }
                    }
                     
                    int argumentOrder = 0;
                    foreach (ArgumentSyntax argument in node.ArgumentList.Arguments)
                    {   
                        if (namedArgument != null && argument.ChildNodes().Contains(namedArgument))
                        {
                            // Remove the named argument. That is, do not add to new argument list
                        }
                        else if (namedArgument == null && argumentOrder == this.order)
                        {
                            // Remove the named argument. That is, do not add to new argument list
                        }
                        else
                        {
                            // Some other argument. Process it recursively.
                            // Considers:
                            // foo(a: 1, b: foo(a: 1, b: 2))
                            ArgumentSyntax processedArgument = (ArgumentSyntax)base.VisitArgument(argument);
                            
                            if (processedArgument != null)
                            {
                                newArgumentSeparatedList = newArgumentSeparatedList.Add(processedArgument);
                            }
                        }

                        ++argumentOrder;
                    }

                    InvocationExpressionSyntax newInvocation = node.WithArgumentList(Syntax.ArgumentList(newArgumentSeparatedList))
                                                                   .WithAdditionalAnnotations(CodeAnnotations.Formatting);
                    return newInvocation;
                }

                return base.VisitInvocationExpression(node);
            }

            public override SyntaxNode VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
            {
                // Considers:
                // new A(a: 1, b: 2) -> new A(b: 2)
                // new A(b: 2, a: 1) -> new A(b: 2)
                // new A(1, b: 2) -> new A(b: 2)
                // new A(1, 2) -> new A(2)
                // new A(a: 1, b: new A(a: 1, b: null));

                // Check does invocation refer to updated method
                ISymbol referencedMethod = this.model.GetSymbolInfo(node, this.cancellationToken).Symbol;

                if (referencedMethod != null && referencedMethod.Equals(this.methodSymbol))
                {
                    SeparatedSyntaxList<ArgumentSyntax> newArgumentSeparatedList = Syntax.SeparatedList<ArgumentSyntax>();

                    // Check is the removed parameter referenced by name within method object creation, e.g. new A(1, 2, >a:2<)
                    // Note: Must be a direct child! Not nested descendant like new A(a: new A(>a:<null)) !

                    NameColonSyntax namedArgument = null;
                    foreach (ArgumentSyntax argument in node.ArgumentList.Arguments)
                    {
                        namedArgument = argument.ChildNodes()
                                            .OfType<NameColonSyntax>()
                                            .FirstOrDefault(
                                                (n) =>
                                                {
                                                    ISymbol nameSymbol = this.model.GetSymbolInfo(n.Identifier).Symbol;
                                                    return nameSymbol != null && nameSymbol.Equals(this.parameterSymbol);
                                                });
                        if (namedArgument != null)
                        {
                            break;
                        }
                    }

                    int argumentOrder = 0;
                    foreach (ArgumentSyntax argument in node.ArgumentList.Arguments)
                    {
                        if (namedArgument != null && argument.ChildNodes().Contains(namedArgument))
                        {
                            // Remove the named argument. That is, do not add to new argument list
                        }
                        else if (namedArgument == null && argumentOrder == this.order)
                        {
                            // Remove the named argument. That is, do not add to new argument list
                        }
                        else
                        {
                            // Some other argument. Process it recursively.
                            // Considers:
                            // new A(a: 1, b: new A(a: 1, b: null))
                            ArgumentSyntax processedArgument = (ArgumentSyntax)base.VisitArgument(argument);

                            if (processedArgument != null)
                            {
                                newArgumentSeparatedList = newArgumentSeparatedList.Add(processedArgument);
                            }
                        }

                        ++argumentOrder;
                    }

                    ObjectCreationExpressionSyntax newCreation = node.WithArgumentList(Syntax.ArgumentList(newArgumentSeparatedList))
                                                                     .WithAdditionalAnnotations(CodeAnnotations.Formatting);
                    return newCreation;
                }

                return base.VisitObjectCreationExpression(node);
            }
        }
    }
}
