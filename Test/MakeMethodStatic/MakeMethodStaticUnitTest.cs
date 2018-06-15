using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test.MakeMethodStatic
{
    [TestClass]
    public class MakeMethodStaticUnitTest : TestFramework
    {
        [TestMethod]
        public void MakeMethodStatic_NoInstanceRefarences()
        {
            const string code = @"
class A
{
    public int foo(int i, int j)
    {
        return i + j;
    }
}

class B
{
    void bar()
    {
        A a = new A();
        a.foo(1, 2);
        new A().foo(3, 4);
    }
}
";
            const string expected = @"
class A
{
    static public int foo(A self, int i, int j)
    {
        return i + j;
    }
}

class B
{
    void bar()
    {
        A a = new A();
        A.foo(a, 1, 2);
        A.foo(new A(), 3, 4);
    }
}
";
            TestSingleRefactoring<Refactoring.MakeMethodStaticRefactoring>(code, expected, GetTextSpanWithin("foo", "public int foo", code));
        }

        [TestMethod]
        public void MakeMethodStatic_InstanceRefarences()
        {
            const string code = @"
class A
{
    int x;

    public int foo(int i, int j)
    {
        return x + i + j;
    }
}

class B
{
    void bar()
    {
        A a = new A();
        a.foo(1, 2);
        new A().foo(3, 4);
    }
}
";
            const string expected = @"
class A
{
    int x;

    static public int foo(A self, int i, int j)
    {
        return self.x + i + j;
    }
}

class B
{
    void bar()
    {
        A a = new A();
        A.foo(a, 1, 2);
        A.foo(new A(), 3, 4);
    }
}
";
            TestSingleRefactoring<Refactoring.MakeMethodStaticRefactoring>(code, expected, GetTextSpanWithin("foo", "public int foo", code));
        }

        [TestMethod]
        public void MakeMethodStatic_InstanceRefarenceAsMemberAccessExpression()
        {
            const string code = @"
class A
{
    C c;

    public int foo(int i, int j)
    {
        return c.x + i + j;
    }
}

class C
{
    public int x;
}
";
            const string expected = @"
class A
{
    C c;

    static public int foo(A self, int i, int j)
    {
        return self.c.x + i + j;
    }
}

class C
{
    public int x;
}
";
            TestSingleRefactoring<Refactoring.MakeMethodStaticRefactoring>(code, expected, GetTextSpanWithin("foo", "public int foo", code));
        }

        [TestMethod]
        public void MakeMethodStatic_InstanceRefarencesWithThis()
        {
            const string code = @"
class A
{
    int x;

    public int foo(int i, int j)
    {
        return this.x + x + i + j;
    }
}

class B
{
    void bar()
    {
        A a = new A();
        a.foo(1, 2);
        new A().foo(3, 4);
    }
}
";
            const string expected = @"
class A
{
    int x;

    static public int foo(A self, int i, int j)
    {
        return self.x + self.x + i + j;
    }
}

class B
{
    void bar()
    {
        A a = new A();
        A.foo(a, 1, 2);
        A.foo(new A(), 3, 4);
    }
}
";
            TestSingleRefactoring<Refactoring.MakeMethodStaticRefactoring>(code, expected, GetTextSpanWithin("foo", "public int foo", code));
        }

        [TestMethod]
        public void MakeMethodStatic_InstanceRefarencesWithBase_Nok()
        {
            const string code = @"
class A : B
{
    public int foo(int i, int j)
    {
        return base.x + i + j;
    }
}

class B
{
    public int x;
}
";
            TestNoRefactoring<Refactoring.MakeMethodStaticRefactoring>(code, GetTextSpanWithin("foo", "public int foo", code));
        }

        [TestMethod]
        public void MakeMethodStatic_MemberAccessExpression()
        {
            const string code = @"
class A
{
    public int foo(int i, int j)
    {
        return i + j;
    }
}

class B
{
    public A a = new A();
}

class C
{
    void bar()
    {
        B b = new B();
        b.a.foo(1, 2);
    }
}
";
            const string expected = @"
class A
{
    static public int foo(A self, int i, int j)
    {
        return i + j;
    }
}

class B
{
    public A a = new A();
}

class C
{
    void bar()
    {
        B b = new B();
        A.foo(b.a, 1, 2);
    }
}
";
            TestSingleRefactoring<Refactoring.MakeMethodStaticRefactoring>(code, expected, GetTextSpanWithin("foo", "public int foo", code));
        }

        [TestMethod]
        public void MakeMethodStatic_RecursiveInvocationWithThis()
        {
            const string code = @"
class A
{
    public int foo(int i, int j)
    {
        return this.foo(i, j);
    }
}
";
            const string expected = @"
class A
{
    static public int foo(A self, int i, int j)
    {
        return foo(self, i, j);
    }
}
";
            TestSingleRefactoring<Refactoring.MakeMethodStaticRefactoring>(code, expected, GetTextSpanWithin("foo", "public int foo", code));
        }
    }
}
