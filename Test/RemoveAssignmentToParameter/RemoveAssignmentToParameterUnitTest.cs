using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Roslyn.Compilers.Common;
using Roslyn.Compilers.CSharp;

namespace Test.RemoveAssignmentToParameter
{
    [TestClass]
    public class RemoveAssignmentToParameterUnitTest : TestFramework
    {
        [TestMethod]
        public void RemoveAssignmentToParameter_DoNotReplaceBeforeFirstWrite()
        {
            const string code = @"
class A
{
    void foo(int p)
    {
        int a = p + 1;
        p = 2;
        int b = p;
    }
}
";
            const string expected = @"
class A
{
    void foo(int p)
    {
        int tempVariable = p;
        int a = p + 1;
        tempVariable = 2;
        int b = tempVariable;
    }
}
";
            TestSingleIssue<Refactoring.RemoveAssignmentToParameterIssue, ParameterSyntax>(code, expected, GetTextSpanFor("int p", code));
        }

        [TestMethod]
        public void RemoveAssignmentToParameter_ComplexAssignment()
        {
            const string code = @"
class A
{
    void foo(int p)
    {
        int a = p + 1;
        p += 2;
        int b = p;
    }
}
";
            const string expected = @"
class A
{
    void foo(int p)
    {
        int tempVariable = p;
        int a = p + 1;
        tempVariable += 2;
        int b = tempVariable;
    }
}
";
            TestSingleIssue<Refactoring.RemoveAssignmentToParameterIssue, ParameterSyntax>(code, expected, GetTextSpanFor("int p", code));
        }

        [TestMethod]
        public void RemoveAssignmentToParameter_NotReassigned_Nok()
        {
            const string code = @"
class A
{
    void foo(int p)
    {
        int a = p + 1;
        int b = p;
        bar(p);
    }

    void bar(int a)
    {
    }
}
";
            TestNoIssue<Refactoring.RemoveAssignmentToParameterIssue, ParameterSyntax>(code, GetTextSpanFor("int p", code));
        }

        [TestMethod]
        public void RemoveAssignmentToParameter_RaiseIssueForDetectedUsage()
        {
            const string code = @"
class A
{
    void foo(int p)
    {
        int a = 123;
        p = 2;
    }
}
";
            const string expected = @"
class A
{
    void foo(int p)
    {
        int tempVariable = p;
        int a = 123;
        tempVariable = 2;
    }
}
";
            TestSingleIssue<Refactoring.RemoveAssignmentToParameterIssue, BinaryExpressionSyntax>(code, expected, GetTextSpanFor("p = 2", code));
        }

        [TestMethod]
        public void RemoveAssignmentToParameter_NestedBlocksAnalysis()
        {
            const string code = @"
class A
{
    void foo(int p)
    {
        int a = p;
        {
            int b = p;
            p = 2;
            int c = p;
        }
    }
}
";
            const string expected = @"
class A
{
    void foo(int p)
    {
        int tempVariable = p;
        int a = p;
        {
            int b = p;
            tempVariable = 2;
            int c = tempVariable;
        }
    }
}
";
            TestSingleIssue<Refactoring.RemoveAssignmentToParameterIssue, BinaryExpressionSyntax>(code, expected, GetTextSpanFor("p = 2", code));
        }

        [TestMethod]
        public void RemoveAssignmentToParameter_IfBlockAnalysis()
        {
            const string code = @"
class A
{
    int foo(int p)
    {
        int a = p;
        if (p == 1) p = 2;
        else if (p == 2) p = 3;
        else p = 4;
        int b = p;
        return p;
    }
}
";
            const string expected = @"
class A
{
    int foo(int p)
    {
        int tempVariable = p;
        int a = p;
        if (p == 1) tempVariable = 2;
        else if (p == 2) tempVariable = 3;
        else tempVariable = 4;
        int b = tempVariable;
        return tempVariable;
    }
}
";
            TestSingleIssue<Refactoring.RemoveAssignmentToParameterIssue, ParameterSyntax>(code, expected, GetTextSpanFor("int p", code));
        }

        [TestMethod]
        public void RemoveAssignmentToParameter_ForBlockAnalysis()
        {
            const string code = @"
class A
{
    int foo(int p)
    {
        int a = p;
        for (int i = p; i < 10; ++i, ++p)
        {
            int b = p;
        }
        return p;
    }
}
";
            const string expected = @"
class A
{
    int foo(int p)
    {
        int tempVariable = p;
        int a = p;
        for (int i = tempVariable; i < 10; ++i, ++tempVariable)
        {
            int b = tempVariable;
        }
        return tempVariable;
    }
}
";
            TestSingleIssue<Refactoring.RemoveAssignmentToParameterIssue, ParameterSyntax>(code, expected, GetTextSpanFor("int p", code));
        }

        [TestMethod]
        public void RemoveAssignmentToParameter_IfConditionBlockAnalysis()
        {
            const string code = @"
class A
{
    int foo(int p)
    {
        int b = p;
        if (++p == 123)
        {
            int c = p;
        }
        return p;
    }
}
";
            const string expected = @"
class A
{
    int foo(int p)
    {
        int tempVariable = p;
        int b = p;
        if (++tempVariable == 123)
        {
            int c = tempVariable;
        }
        return tempVariable;
    }
}
";
            TestSingleIssue<Refactoring.RemoveAssignmentToParameterIssue, ParameterSyntax>(code, expected, GetTextSpanFor("int p", code));
        }

        [TestMethod]
        public void RemoveAssignmentToParameter_MethodCallExpression()
        {
            const string code = @"
class A
{
    int foo(int p)
    {
        int b = p;
        foo(++p);
        return p;
    }
}
";
            const string expected = @"
class A
{
    int foo(int p)
    {
        int tempVariable = p;
        int b = p;
        foo(++tempVariable);
        return tempVariable;
    }
}
";
            TestSingleIssue<Refactoring.RemoveAssignmentToParameterIssue, ParameterSyntax>(code, expected, GetTextSpanFor("int p", code));
        }
    }
}
