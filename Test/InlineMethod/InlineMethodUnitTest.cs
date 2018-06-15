using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test.InlineMethod
{
    [TestClass]
    public class InlineMethodUnitTest : TestFramework
    {
        [TestMethod]
        public void InlineMethod_SimpleCase()
        {
            const string code = @"
class A
{
    void foo()
    {
        int a = bar();
    }

    int bar()
    {
        return 1 + 2;
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int a = 1 + 2;
    }

    int bar()
    {
        return 1 + 2;
    }
}
";
            TestSingleRefactoring<Refactoring.InlineMethodRefactoring>(code, expected, GetTextSpanFor("bar", code));
        }

        [TestMethod]
        public void InlineMethod_MapParameters()
        {
            const string code = @"
class A
{
    void foo()
    {
        int a = bar(1, 2);
    }

    int bar(int a, int b)
    {
        return a + b;
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int a = 1 + 2;
    }

    int bar(int a, int b)
    {
        return a + b;
    }
}
";
            TestSingleRefactoring<Refactoring.InlineMethodRefactoring>(code, expected, GetTextSpanFor("bar", code));
        }

        [TestMethod]
        public void InlineMethod_MapVariables()
        {
            const string code = @"
class A
{
    void foo()
    {
        int b = 1, c = 2;
        int a = bar(b, c);
    }

    int bar(int a, int b)
    {
        return a + b;
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int b = 1, c = 2;
        int a = b + c;
    }

    int bar(int a, int b)
    {
        return a + b;
    }
}
";
            TestSingleRefactoring<Refactoring.InlineMethodRefactoring>(code, expected, GetTextSpanFor("bar", code));
        }

        [TestMethod]
        public void InlineMethod_Casting()
        {
            const string code = @"
class A
{
    void foo()
    {
        double a = 1 / bar();
    }

    double bar()
    {
        return 5;
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        double a = 1 / (double)5;
    }

    double bar()
    {
        return 5;
    }
}
";
            TestSingleRefactoring<Refactoring.InlineMethodRefactoring>(code, expected, GetTextSpanFor("bar", code));
        }

        [TestMethod]
        public void InlineMethod_AddParenthesis()
        {
            const string code = @"
class A
{
    void foo()
    {
        int a = 1 * bar();
    }

    int bar()
    {
        return 2 + 3;
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int a = 1 * (2 + 3);
    }

    int bar()
    {
        return 2 + 3;
    }
}
";
            TestSingleRefactoring<Refactoring.InlineMethodRefactoring>(code, expected, GetTextSpanFor("bar", code));
        }

        [TestMethod]
        public void InlineMethod_OutParameter()
        {
            const string code = @"
class A
{
    void foo()
    {
        int x;
        int a = 1 * bar(out x);
    }

    int bar(out int a)
    {
        return a = 1;
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int x;
        int a = 1 * (x = 1);
    }

    int bar(out int a)
    {
        return a = 1;
    }
}
";
            TestSingleRefactoring<Refactoring.InlineMethodRefactoring>(code, expected, GetTextSpanFor("bar", code));
        }

        [TestMethod]
        public void InlineMethod_RefParameter()
        {
            const string code = @"
class A
{
    void foo()
    {
        int x = 2;
        int a = 1 * bar(ref x);
    }

    int bar(ref int a)
    {
        return a = 1;
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int x = 2;
        int a = 1 * (x = 1);
    }

    int bar(ref int a)
    {
        return a = 1;
    }
}
";
            TestSingleRefactoring<Refactoring.InlineMethodRefactoring>(code, expected, GetTextSpanFor("bar", code));
        }

        [TestMethod]
        public void InlineMethod_Assigned_Nok()
        {
            const string code = @"
class A
{
    void foo()
    {
        int x = 2;
        int a = 1 * bar(x);
    }

    int bar(int a)
    {
        return a = 1;
    }
}
";
            TestNoRefactoring<Refactoring.InlineMethodRefactoring>(code, GetTextSpanFor("bar", code));
        }

        [TestMethod]
        public void InlineMethod_DoubleCasting()
        {
            const string code = @"
class A
{
    void foo()
    {
        double a = bar(1 + 2, 3) / 2;
    }

    double bar(float a, int b)
    {
        return a / (b + 4);
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        double a = (double)((float)(1 + 2) / (3 + 4)) / 2;
    }

    double bar(float a, int b)
    {
        return a / (b + 4);
    }
}
";
            TestSingleRefactoring<Refactoring.InlineMethodRefactoring>(code, expected, GetTextSpanFor("bar", code));
        }

        [TestMethod]
        public void InlineMethod_FieldReference()
        {
            const string code = @"
class A
{
    int x;

    void foo()
    {
        int a = bar();
    }

    int bar()
    {
        return x;
    }
}
";
            const string expected = @"
class A
{
    int x;

    void foo()
    {
        int a = x;
    }

    int bar()
    {
        return x;
    }
}
";
            TestSingleRefactoring<Refactoring.InlineMethodRefactoring>(code, expected, GetTextSpanFor("bar", code));
        }

        [TestMethod]
        public void InlineMethod_DefaultValuesOfParameters()
        {
            const string code = @"
class A
{
    void foo()
    {
        int a = bar(1);
    }

    int bar(int a, int b = 2, int c = 4)
    {
        return a + b * c;
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int a = 1 + 2 * 4;
    }

    int bar(int a, int b = 2, int c = 4)
    {
        return a + b * c;
    }
}
";
            TestSingleRefactoring<Refactoring.InlineMethodRefactoring>(code, expected, GetTextSpanFor("bar", code));
        }

        [TestMethod]
        public void InlineMethod_ArrayIndexing()
        {
            const string code = @"
class A
{
    void foo()
    {
        int[] a = new int[10];
        int b = bar(a);
    }

    int bar(int[] c)
    {
        return c[1] + c[2];
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int[] a = new int[10];
        int b = a[1] + a[2];
    }

    int bar(int[] c)
    {
        return c[1] + c[2];
    }
}
";
            TestSingleRefactoring<Refactoring.InlineMethodRefactoring>(code, expected, GetTextSpanFor("bar", code));
        }

        [TestMethod]
        public void InlineMethod_AddParenthesisDueToElementAccess()
        {
            const string code = @"
using System.Collections.Generic;
class A
{
    void foo()
    {
        int[] a = new int[10];
        int b = bar(a);
    }

    int bar(IList<int> c)
    {
        return c[1];
    }
}
";
            const string expected = @"
using System.Collections.Generic;
class A
{
    void foo()
    {
        int[] a = new int[10];
        int b = ((IList<int>)a)[1];
    }

    int bar(IList<int> c)
    {
        return c[1];
    }
}
";
            TestSingleRefactoring<Refactoring.InlineMethodRefactoring>(code, expected, GetTextSpanFor("bar", code));
        }

        [TestMethod]
        public void InlineMethod_AddParenthesisDueToMemberAccess()
        {
            const string code = @"
using System.Collections.Generic;
class A
{
    void foo()
    {
        int[] a = new int[10];
        int b = bar(a);
    }

    int bar(IList<int> c)
    {
        return c.IndexOf(5);
    }
}
";
            const string expected = @"
using System.Collections.Generic;
class A
{
    void foo()
    {
        int[] a = new int[10];
        int b = ((IList<int>)a).IndexOf(5);
    }

    int bar(IList<int> c)
    {
        return c.IndexOf(5);
    }
}
";
            TestSingleRefactoring<Refactoring.InlineMethodRefactoring>(code, expected, GetTextSpanFor("bar", code));
        }

        [TestMethod]
        public void InlineMethod_MapTypeParameters()
        {
            const string code = @"
class A
{
    void foo()
    {
        int b = bar(1);
    }

    T bar<T>(T c)
    {
        return (T)c;
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int b = (int)1;
    }

    T bar<T>(T c)
    {
        return (T)c;
    }
}
";
            TestSingleRefactoring<Refactoring.InlineMethodRefactoring>(code, expected, GetTextSpanFor("bar", code));
        }

        [TestMethod]
        public void InlineMethod_ExpressionStatementIsNotIndependent_Nok()
        {
            const string code = @"
class A
{
    void foo()
    {
        bar(1);
    }

    int bar(int c)
    {
        return c;
    }
}
";
            TestNoRefactoring<Refactoring.InlineMethodRefactoring>(code, GetTextSpanFor("bar", code));
        }

        [TestMethod]
        public void InlineMethod_ExpressionStatementIsIndependent()
        {
            const string code = @"
class A
{
    void foo()
    {
        bar(1);
    }

    A bar(int c)
    {
        return new A();
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        new A();
    }

    A bar(int c)
    {
        return new A();
    }
}
";
            TestSingleRefactoring<Refactoring.InlineMethodRefactoring>(code, expected, GetTextSpanFor("bar", code));
        }

        [TestMethod]
        public void InlineMethod_VirtualMethod_Nok()
        {
            const string code = @"
class A
{
    void foo()
    {
        int z = bar();
    }

    public virtual int bar()
    {
        return 123;
    }
}
";
            TestNoRefactoring<Refactoring.InlineMethodRefactoring>(code, GetTextSpanFor("bar", code));
        }

        [TestMethod]
        public void InlineMethod_OverridingMethod_Nok()
        {
            const string code = @"
class B
{
    public virtual int bar()
    {
        return 123;
    }
}

class A : B
{
    void foo()
    {
        int z = bar();
    }

    public override int bar()
    {
        return 456;
    }
}
";
            TestNoRefactoring<Refactoring.InlineMethodRefactoring>(code, GetTextSpanWithin("bar", "int z = bar()", code));
        }
    }
}