using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Roslyn.Compilers.Common;
using Roslyn.Compilers.CSharp;

namespace Test.MakeConst
{
    [TestClass]
    public class MakeConstUnitTest : TestFramework
    {
        [TestMethod]
        public void MakeConst_Simple()
        {
            const string code = @"
class A
{
    void foo()
    {
        int VAR = 1;
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        const int VAR = 1;
    }
}
";
            TestSingleIssue<Refactoring.MakeConstIssue, LocalDeclarationStatementSyntax>(code, expected, GetTextSpanFor("VAR", code));
        }

        [TestMethod]
        public void MakeConst_AlreadyConst_Nok()
        {
            const string code = @"
class A
{
    void foo()
    {
        const int VAR = 1;
    }
}
";
            TestNoIssue<Refactoring.MakeConstIssue, LocalDeclarationStatementSyntax>(code, GetTextSpanFor("VAR", code));
        }

        [TestMethod]
        public void MakeConst_VarKeyword_Nok()
        {
            const string code = @"
class A
{
    void foo()
    {
        var VAR = 1;
    }
}
";
            TestNoIssue<Refactoring.MakeConstIssue, LocalDeclarationStatementSyntax>(code, GetTextSpanFor("VAR", code));
        }

        [TestMethod]
        public void MakeConst_Reassigned_Nok()
        {
            const string code = @"
class A
{
    void foo()
    {
        int VAR = 1;
        VAR = 2; 
    }
}
";
            TestNoIssue<Refactoring.MakeConstIssue, LocalDeclarationStatementSyntax>(code, GetTextSpanFor("VAR", code));
        }

        [TestMethod]
        public void MakeConst_PassedAsRef_Nok()
        {
            const string code = @"
class A
{
    void foo()
    {
        int VAR = 1;
        bar(ref VAR); 
    }
    
    void bar(ref int i)
    {
        i = 2;
    }
}
";
            TestNoIssue<Refactoring.MakeConstIssue, LocalDeclarationStatementSyntax>(code, GetTextSpanFor("VAR", code));
        }

        [TestMethod]
        public void MakeConst_String()
        {
            const string code = @"
class A
{
    void foo()
    {
        string VAR = ""test"";
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        const string VAR = ""test"";
    }
}
";
            TestSingleIssue<Refactoring.MakeConstIssue, LocalDeclarationStatementSyntax>(code, expected, GetTextSpanFor("VAR", code));
        }

        [TestMethod]
        public void MakeConst_StringObject_Nok()
        {
            const string code = @"
class A
{
    void foo()
    {
        object VAR = ""test"";
    }
}
";
            TestNoIssue<Refactoring.MakeConstIssue, LocalDeclarationStatementSyntax>(code, GetTextSpanFor("VAR", code));
        }
    }
}
