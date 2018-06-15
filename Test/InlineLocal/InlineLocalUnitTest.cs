using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test.InlineLocal
{
    [TestClass]
    public class InlineLocalUnitTest : TestFramework
    {
        [TestMethod]
        public void InlineLocal_Casting()
        {
            const string code = @"
class A
{
    void foo()
    {
        float f = 5;
        float a = 1 / f;
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        float a = 1 / (float)5;
    }
}
";
            TestSingleRefactoring<Refactoring.InlineLocalRefactoring>(code, expected, GetTextSpanFor("f = 5", code));
        }

        [TestMethod]
        public void InlineLocal_ConditionalExpression()
        {
            const string code = @"
class A
{
    void foo()
    {
        int a = c == true ? 1 : 4;
        int b = a + 3;
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int b = (c == true ? 1 : 4) + 3;
    }
}
";
            TestSingleRefactoring<Refactoring.InlineLocalRefactoring>(code, expected, GetTextSpanFor("a =", code));
        }

        [TestMethod]
        public void InlineLocal_AddParenthesis()
        {
            const string code = @"
class A
{
    void foo()
    {
        int a = 1 + 2;
        int b = a * 3;
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int b = (1 + 2) * 3;
    }
}
";
            TestSingleRefactoring<Refactoring.InlineLocalRefactoring>(code, expected, GetTextSpanFor("a =", code));
        }

        [TestMethod]
        public void InlineLocal_MultipleDeclaration()
        {
            const string code = @"
class A
{
    void foo()
    {
        int a = 1 + 2, b = 4;
        int c = a;
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int b = 4;
        int c = 1 + 2;
    }
}
";
            TestSingleRefactoring<Refactoring.InlineLocalRefactoring>(code, expected, GetTextSpanFor("a =", code));
        }

        [TestMethod]
        public void InlineLocal_Invocation()
        {
            const string code = @"
class A
{
    void foo()
    {
        int a = 1 + 2;
        a.ToString();
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        (1 + 2).ToString();
    }
}
";
            TestSingleRefactoring<Refactoring.InlineLocalRefactoring>(code, expected, GetTextSpanFor("a =", code));
        }

        [TestMethod]
        public void InlineLocal_NoInitializer_Nok()
        {
            const string code = @"
class A
{
    void foo()
    {
        int local;
        local = 1;
    }
}
";
            TestNoRefactoring<Refactoring.InlineLocalRefactoring>(code, GetTextSpanFor("local", code));
        }

        [TestMethod]
        public void InlineLocal_AddParenthesisDueToElementAccess()
        {
            const string code = @"
using System.Collections.Generic;
class A
{
    void foo()
    {
        int[] a = new int[100];
        IList<int> list = a;
        int b = list[0];
        list.GetEnumerator();
    }
}
";
            const string expected = @"
using System.Collections.Generic;
class A
{
    void foo()
    {
        int[] a = new int[100];
        int b = ((IList<int>)a)[0];
        ((IList<int>)a).GetEnumerator();
    }
}
";
            TestSingleRefactoring<Refactoring.InlineLocalRefactoring>(code, expected, GetTextSpanFor("list =", code));
        }
    }
}