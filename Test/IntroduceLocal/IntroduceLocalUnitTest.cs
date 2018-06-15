using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test.IntroduceLocal
{
    [TestClass]
    public class IntroduceLocalUnitTest : TestFramework
    {
        [TestMethod]
        public void IntroduceLocal_SimpleCase()
        {
            const string code = @"
class A
{
    void foo()
    {
        int a = 1 + 2;
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int newVariable = 1 + 2;
        int a = newVariable;
    }
}
";
            TestSingleRefactoring<Refactoring.IntroduceLocalRefactoring>(code, expected, GetTextSpanFor("1 + 2", code));
        }

        [TestMethod]
        public void IntroduceLocal_NestedBlocks()
        {
            const string code = @"
class A
{
    void foo()
    {
        {
            int a = 1 + 2 * 3;
        }
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        {
            int newVariable = 1 + 2 * 3;
            int a = newVariable;
        }
    }
}
";
            TestSingleRefactoring<Refactoring.IntroduceLocalRefactoring>(code, expected, GetTextSpanFor("1 + 2 * 3", code));
        }

        [TestMethod]
        public void IntroduceLocal_NoBlockIf()
        {
            const string code = @"
class A
{
    void foo()
    {
        int a = 1;
        if (a == 1)
            a = 1 + 2;
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int a = 1;
        if (a == 1)
        {
            int newVariable = 1 + 2;
            a = newVariable;
        }
    }
}
";
            TestSingleRefactoring<Refactoring.IntroduceLocalRefactoring>(code, expected, GetTextSpanFor("1 + 2", code));
        }

        [TestMethod]
        public void IntroduceLocal_NoBlockFor()
        {
            const string code = @"
class A
{
    void foo()
    {
        int a = 1;
        for (int i = 0; i < 10; ++i)
            a = i * 10;
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int a = 1;
        for (int i = 0; i < 10; ++i)
        {
            int newVariable = i * 10;
            a = newVariable;
        }
    }
}
";
            TestSingleRefactoring<Refactoring.IntroduceLocalRefactoring>(code, expected, GetTextSpanFor("i * 10", code));
        }

        [TestMethod]
        public void IntroduceLocal_InsertBeforeFor()
        {
            const string code = @"
class A
{
    void foo()
    {
        if (true)
            for (int i = 0; i < 10 * 20; ++i) { }
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        if (true)
        {
            int newVariable = 10 * 20;
            for (int i = 0; i < newVariable; ++i) { }
        }
    }
}
";
            TestSingleRefactoring<Refactoring.IntroduceLocalRefactoring>(code, expected, GetTextSpanFor("10 * 20", code));
        }

        [TestMethod]
        public void IntroduceLocal_IncovationExpression()
        {
            const string code = @"
class A
{
    int foo()
    {
        foo();
    }
}
";
            const string expected = @"
class A
{
    int foo()
    {
        int newVariable = foo();
    }
}
";
            TestSingleRefactoring<Refactoring.IntroduceLocalRefactoring>(code, expected, GetTextSpanWithin("foo()", "foo();", code));
        }

        [TestMethod]
        public void IntroduceLocal_IncrementExpression()
        {
            const string code = @"
class A
{
    void foo()
    {
        int i = 0;
        ++i;
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int i = 0;
        int newVariable = ++i;
    }
}
";
            TestSingleRefactoring<Refactoring.IntroduceLocalRefactoring>(code, expected, GetTextSpanFor("++i", code));
        }

        [TestMethod]
        public void IntroduceLocal_VoidIncovationExpression_Nok()
        {
            const string code = @"
class A
{
    void foo()
    {
        foo();
    }
}
";
            TestNoRefactoring<Refactoring.IntroduceLocalRefactoring>(code, GetTextSpanWithin("foo()", "foo();", code));
        }

        [TestMethod]
        public void IntroduceLocal_LeftHandSideExtractable()
        {
            const string code = @"
class A
{
    public int x;
}

class B
{
    A a;
    void foo()
    {
        B b = new B();
        b.a.x = 123;
    }
}
";
            const string expected = @"
class A
{
    public int x;
}

class B
{
    A a;
    void foo()
    {
        B b = new B();
        A newVariable = b.a;
        newVariable.x = 123;
    }
}
";
            TestSingleRefactoring<Refactoring.IntroduceLocalRefactoring>(code, expected, GetTextSpanFor("b.a", code));
        }

        [TestMethod]
        public void IntroduceLocal_LeftHandSideNotExtractable_Nok()
        {
            const string code = @"
class A
{
    public int x;
}

class B
{
    A a;
    void foo()
    {
        B b = new B();
        b.a.x = 123;
    }
}
";
            TestNoRefactoring<Refactoring.IntroduceLocalRefactoring>(code, GetTextSpanFor("b.a.x", code));
        }

        [TestMethod]
        public void IntroduceLocal_MemberAccessNotExtractable_Nok()
        {
            const string code = @"
class A
{
    public int x;
}

class B
{
    A a;
    void foo()
    {
        B b = new B();
        int z = b.a.x;
    }
}
";
            TestNoRefactoring<Refactoring.IntroduceLocalRefactoring>(code, GetTextSpanWithin("a", "b.a.x", code));
        }

        [TestMethod]
        public void IntroduceLocal_MemberAccessNotExtractable2_Nok()
        {
            const string code = @"
class A
{
    public int x;
}

class B
{
    A a;
    void foo()
    {
        B b = new B();
        int z = b.a.x;
    }
}
";
            TestNoRefactoring<Refactoring.IntroduceLocalRefactoring>(code, GetTextSpanWithin("a.x", "b.a.x", code));
        }

        [TestMethod]
        public void IntroduceLocal_MemberAccessExtractable()
        {
            const string code = @"
class A
{
    public int x;
}

class B
{
    A a;
    void foo()
    {
        B b = new B();
        int z = b.a.x;
    }
}
";
            const string expected = @"
class A
{
    public int x;
}

class B
{
    A a;
    void foo()
    {
        B b = new B();
        A newVariable = b.a;
        int z = newVariable.x;
    }
}
";
            TestSingleRefactoring<Refactoring.IntroduceLocalRefactoring>(code, expected, GetTextSpanWithin("b.a", "b.a.x", code));
        }
    }
}
