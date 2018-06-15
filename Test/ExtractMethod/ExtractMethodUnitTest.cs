using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test.ExtractMethod
{
    [TestClass]
    public class ExtractMethodUnitTest : TestFramework
    {
        [TestMethod]
        public void ExtractMethod_BinaryExpression()
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
        int a = NewMethod();
    }

    int NewMethod()
    {
        return 1 + 2;
    }
}
";
            TestSingleRefactoring<Refactoring.ExtractMethodRefactoring>(code, expected, GetTextSpanFor("1 + 2", code));
        }
        
        [TestMethod]
        public void ExtractMethod_BinaryExpressionWithLocals()
        {
            const string code = @"
class A
{
    void foo()
    {
        int a = 1;
        int b = 2;
        int c = a + b;
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int a = 1;
        int b = 2;
        int c = NewMethod(a, b);
    }

    int NewMethod(int a, int b)
    {
        return a + b;
    }
}
";
            TestSingleRefactoring<Refactoring.ExtractMethodRefactoring>(code, expected, GetTextSpanFor("a + b", code));
        }

        [TestMethod]
        public void ExtractMethod_AssignmentExpression()
        {
            const string code = @"
class A
{
    void foo()
    {
        int a;
        int c = (a = 1);
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int a;
        int c = NewMethod(out a);
    }

    int NewMethod(out int a)
    {
        return (a = 1);
    }
}
";
            TestSingleRefactoring<Refactoring.ExtractMethodRefactoring>(code, expected, GetTextSpanFor("(a = 1)", code));
        }

        [TestMethod]
        public void ExtractMethod_AssignmentExpressionWithRead()
        {
            const string code = @"
class A
{
    void foo()
    {
        int a;
        int b = 2;
        int c = b + (a = 1) + (b = 3);
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int a;
        int b = 2;
        int c = NewMethod(out a, ref b);
    }

    int NewMethod(out int a, ref int b)
    {
        return b + (a = 1) + (b = 3);
    }
}
";
            TestSingleRefactoring<Refactoring.ExtractMethodRefactoring>(code, expected, GetTextSpanFor("b + (a = 1) + (b = 3)", code));
        }

        [TestMethod]
        public void ExtractMethod_AlreadyOutRefArguments()
        {
            const string code = @"
class A
{
    void foo()
    {
        int a;
        int b = 2;
        int c = bar(out a, ref b);
    }

    int bar(out int a, ref int b)
    {
        return (a = b);
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int a;
        int b = 2;
        int c = NewMethod(out a, ref b);
    }

    int bar(out int a, ref int b)
    {
        return (a = b);
    }

    int NewMethod(out int a, ref int b)
    {
        return bar(out a, ref b);
    }
}
";
            TestSingleRefactoring<Refactoring.ExtractMethodRefactoring>(code, expected, GetTextSpanFor("bar(out a, ref b)", code));
        }

        [TestMethod]
        public void ExtractMethod_StaticMethod()
        {
            const string code = @"
class A
{
    static void foo()
    {
        int a = 1 + 2;
    }
}
";
            const string expected = @"
class A
{
    static void foo()
    {
        int a = NewMethod();
    }

    static int NewMethod()
    {
        return 1 + 2;
    }
}
";
            TestSingleRefactoring<Refactoring.ExtractMethodRefactoring>(code, expected, GetTextSpanFor("1 + 2", code));
        }

        [TestMethod]
        public void ExtractMethod_ReturnTypeSameAsContainerAndSimplified()
        {
            const string code = @"
namespace N1.N2
{
    class A
    {
        void foo()
        {
            N1.N2.A a = new N1.N2.A();
        }
    }
}
";
            const string expected = @"
namespace N1.N2
{
    class A
    {
        void foo()
        {
            N1.N2.A a = NewMethod();
        }

        A NewMethod()
        {
            return new N1.N2.A();
        }
    }
}
";
            TestSingleRefactoring<Refactoring.ExtractMethodRefactoring>(code, expected, GetTextSpanFor("new N1.N2.A()", code));
        }

        [TestMethod]
        public void ExtractMethod_ArrayIndexingExpression()
        {
            const string code = @"
class A
{
    void foo()
    {
        int[] a = new int[100];
        int b = a[0] + a[1];
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int[] a = new int[100];
        int b = NewMethod(a);
    }

    int NewMethod(int[] a)
    {
        return a[0] + a[1];
    }
}
";
            TestSingleRefactoring<Refactoring.ExtractMethodRefactoring>(code, expected, GetTextSpanFor("a[0] + a[1]", code));
        }

        [TestMethod]
        public void ExtractMethod_ExpressionOfTemplateType()
        {
            const string code = @"
class A
{
    void foo<T>(T a)
    {
        T b = a;
    }
}
";
            const string expected = @"
class A
{
    void foo<T>(T a)
    {
        T b = NewMethod<T>(a);
    }

    T NewMethod<T>(T a)
    {
        return a;
    }
}
";
            TestSingleRefactoring<Refactoring.ExtractMethodRefactoring>(code, expected, GetTextSpanWithin("a", " = a;", code));
        }

        [TestMethod]
        public void ExtractMethod_ExpressionOfTemplateTypeAndConstraints()
        {
            const string code = @"
class A
{
    void foo<T, U>(T a) where T : new()
    {
        T b = a;
    }
}
";
            const string expected = @"
class A
{
    void foo<T, U>(T a) where T : new()
    {
        T b = NewMethod<T, U>(a);
    }

    T NewMethod<T, U>(T a) where T : new()
    {
        return a;
    }
}
";
            TestSingleRefactoring<Refactoring.ExtractMethodRefactoring>(code, expected, GetTextSpanWithin("a", " = a;", code));
        }

        [TestMethod]
        public void ExtractMethod_ExpressionOfClassTemplateType()
        {
            const string code = @"
class A<V>
{
    void foo<T>(V a)
    {
        V b = (V)a;
    }
}
";
            const string expected = @"
class A<V>
{
    void foo<T>(V a)
    {
        V b = NewMethod<T>(a);
    }

    V NewMethod<T>(V a)
    {
        return (V)a;
    }
}
";
            TestSingleRefactoring<Refactoring.ExtractMethodRefactoring>(code, expected, GetTextSpanFor("(V)a", code));
        }

        [TestMethod]
        public void ExtractMethod_VoidIncovationExpression()
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
            const string expected = @"
class A
{
    void foo()
    {
        NewMethod();
    }

    void NewMethod()
    {
        foo();
    }
}
";
            TestSingleRefactoring<Refactoring.ExtractMethodRefactoring>(code, expected, GetTextSpanWithin("foo()", "foo();", code));
        }

        [TestMethod]
        public void ExtractMethod_LeftHandSideExtractable()
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
        NewMethod(b).x = 123;
    }

    A NewMethod(B b)
    {
        return b.a;
    }
}
";
            TestSingleRefactoring<Refactoring.ExtractMethodRefactoring>(code, expected, GetTextSpanFor("b.a", code));
        }

        [TestMethod]
        public void ExtractMethod_LeftHandSideNotExtractable_Nok()
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
            TestNoRefactoring<Refactoring.ExtractMethodRefactoring>(code, GetTextSpanFor("b.a.x", code));
        }

        [TestMethod]
        public void ExtractMethod_MemberAccessNotExtractable_Nok()
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
            TestNoRefactoring<Refactoring.ExtractMethodRefactoring>(code, GetTextSpanWithin("a", "b.a.x", code));
        }

        [TestMethod]
        public void ExtractMethod_MemberAccessNotExtractable2_Nok()
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
            TestNoRefactoring<Refactoring.ExtractMethodRefactoring>(code, GetTextSpanWithin("a.x", "b.a.x", code));
        }

        [TestMethod]
        public void ExtractMethod_MemberAccessExtractable()
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
        int z = NewMethod(b).x;
    }

    A NewMethod(B b)
    {
        return b.a;
    }
}
";
            TestSingleRefactoring<Refactoring.ExtractMethodRefactoring>(code, expected, GetTextSpanWithin("b.a", "b.a.x", code));
        }
    }
}
