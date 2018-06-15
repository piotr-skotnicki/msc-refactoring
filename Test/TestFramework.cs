using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Roslyn.Compilers;
using Roslyn.Compilers.Common;
using Roslyn.Compilers.CSharp;
using Roslyn.Services;
using Roslyn.Services.Editor;

namespace Test
{
    public class TestFramework
    {
        protected SolutionId solutionId;

        protected ProjectId projectId;

        protected DocumentId documentId;

        protected ISolution solution;

        protected IDocument document;

        protected CancellationToken cancellationToken;

        public TextSpan GetTextSpanFor(String snippet, String code)
        {
            return new TextSpan(code.IndexOf(snippet), snippet.Length);
        }

        public TextSpan GetTextSpanWithin(String snippet, String within, String code)
        {
            return new TextSpan(code.IndexOf(snippet, code.IndexOf(within), within.Length), snippet.Length);
        }

        public void CreateWorkspace(String code)
        {
            solutionId = SolutionId.CreateNewId("TestSolution");

            solution = Solution.Create(solutionId)
                                       .AddCSharpProject("TestProject", "TestProject.dll", out projectId)
                                       .AddMetadataReference(projectId, MetadataReference.Create("mscorlib"))
                                       .AddDocument(projectId, "TestDocument", code, out documentId);

            document = solution.GetDocument(documentId);

            cancellationToken = default(CancellationToken);
        }

        public String ExecuteAction(ICodeAction codeAction)
        {
            CodeActionEdit codeActionEdit = codeAction.GetEdit(cancellationToken);

            IDocument newDocument = codeActionEdit.UpdatedSolution.GetDocument(documentId);
            
            // By default, code formatting is not applied (Visual Studio does that, not the action itself).
            // It must be explicitly forced to format all annotated nodes.
            DocumentTransformation transformation = newDocument.Format(CodeAnnotations.Formatting, cancellationToken: cancellationToken);

            newDocument = transformation.GetUpdatedDocument(cancellationToken);

            CommonSyntaxNode root = newDocument.GetSyntaxRoot();
            //Assert.IsFalse(root.DescendantNodesAndSelf().Any(n => n.HasDiagnostics));

            String newCode = newDocument.GetText().ToString();

            return newCode;
        }

        public void TestSingleRefactoring<T>(String code, String expectedCode, TextSpan textSpan)
            where T : ICodeRefactoringProvider, new()
        {
            CreateWorkspace(code);

            ICodeRefactoringProvider codeRefactoringProvider = new T();

            CodeRefactoring refactoring = codeRefactoringProvider.GetRefactoring(document, textSpan, cancellationToken);

            Assert.AreEqual(1, refactoring.Actions.Count());

            ICodeAction codeAction = refactoring.Actions.First();

            Assert.AreEqual(expectedCode, ExecuteAction(codeAction));
        }

        public void TestNoRefactoring<T>(String code, TextSpan textSpan)
            where T : ICodeRefactoringProvider, new()
        {
            CreateWorkspace(code);

            ICodeRefactoringProvider codeRefactoringProvider = new T();

            CodeRefactoring refactoring = codeRefactoringProvider.GetRefactoring(document, textSpan, cancellationToken);

            Assert.IsNull(refactoring);
        }

        public void TestSingleIssue<T, Node>(String code, String expectedCode, TextSpan textSpan)
            where T : ICodeIssueProvider, new()
            where Node : CommonSyntaxNode
        {
            CreateWorkspace(code);

            CommonSyntaxNode node = document.GetSyntaxRoot().FindToken(textSpan.Start).Parent.FirstAncestorOrSelf<Node>();

            ICodeIssueProvider codeIssueProvider = new T();

            IEnumerable<CodeIssue> codeIssues = codeIssueProvider.GetIssues(document, node, cancellationToken);

            Assert.AreEqual(1, codeIssues.Count());

            CodeIssue codeIssue = codeIssues.First();
            
            Assert.AreEqual(1, codeIssue.Actions.Count());

            ICodeAction codeAction = codeIssue.Actions.First();

            Assert.AreEqual(expectedCode, ExecuteAction(codeAction));
        }

        public void TestNoIssue<T, Node>(String code, TextSpan textSpan)
            where T : ICodeIssueProvider, new()
            where Node : CommonSyntaxNode
        {
            CreateWorkspace(code);

            CommonSyntaxNode node = document.GetSyntaxRoot().FindToken(textSpan.Start).Parent.FirstAncestorOrSelf<Node>();

            ICodeIssueProvider codeIssueProvider = new T();

            IEnumerable<CodeIssue> codeIssues = codeIssueProvider.GetIssues(document, node, cancellationToken);

            Assert.AreEqual(0, codeIssues.Count());
        }
    }
}
