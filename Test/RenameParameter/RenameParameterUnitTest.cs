using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test.RenameParameter
{
    [TestClass]
    public class RenameParameterUnitTest : TestFramework
    {
        [TestMethod]
        public void RenameParameter_NestedInvocation()
        {
            const string code = @"
class A
{
    int foo(int a, int b)
    {
        int c = b;
        return c + b;
    }

    void bar()
    {
        foo(b: foo(a: foo(1, 2), b: foo(b: 5, a: 1)), a: foo(5, 2));
    }
}
";
            const string expected = @"
class A
{
    int foo(int a, int newParameterName)
    {
        int c = newParameterName;
        return c + newParameterName;
    }

    void bar()
    {
        foo(newParameterName: foo(a: foo(1, 2), newParameterName: foo(newParameterName: 5, a: 1)), a: foo(5, 2));
    }
}
";
            TestSingleRefactoring<Refactoring.RenameParameterRefactoring>(code, expected, GetTextSpanFor("int b", code));
        }

        [TestMethod]
        public void RenameParameter_Ctor()
        {
            const string code = @"
class A
{
    public A(A a, A b)
    {
        A c = b;
    }

    void bar()
    {
        new A(b: new A(a: new A(null, null), b: new A(b: null, a: null)), a: new A(null, null));
    }
}
";
            const string expected = @"
class A
{
    public A(A a, A newParameterName)
    {
        A c = newParameterName;
    }

    void bar()
    {
        new A(newParameterName: new A(a: new A(null, null), newParameterName: new A(newParameterName: null, a: null)), a: new A(null, null));
    }
}
";
            TestSingleRefactoring<Refactoring.RenameParameterRefactoring>(code, expected, GetTextSpanFor("A b", code));
        }

        [TestMethod]
        public void RenameParameter_Operator()
        {
            const string code = @"
class A
{
    int x;
    public static int operator +(A a, A b)
    {
        return a.x + b.x;
    }
}
";
            const string expected = @"
class A
{
    int x;
    public static int operator +(A a, A newParameterName)
    {
        return a.x + newParameterName.x;
    }
}
";
            TestSingleRefactoring<Refactoring.RenameParameterRefactoring>(code, expected, GetTextSpanFor("A b", code));
        }

        [TestMethod]
        public void RenameParameter_SimpleLambda()
        {
            const string code = @"
class A
{
    void foo()
    {
        System.Func<int, int> bar = a => a + 1;
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        System.Func<int, int> bar = newParameterName => newParameterName + 1;
    }
}
";
            TestSingleRefactoring<Refactoring.RenameParameterRefactoring>(code, expected, GetTextSpanWithin("a", "= a =>", code));
        }

        [TestMethod]
        public void RenameParameter_ParenthesizedLambda()
        {
            const string code = @"
class A
{
    void foo()
    {
        System.Func<int, int> bar = (a) => a + 1;
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        System.Func<int, int> bar = (newParameterName) => newParameterName + 1;
    }
}
";
            TestSingleRefactoring<Refactoring.RenameParameterRefactoring>(code, expected, GetTextSpanWithin("a", "(a)", code));
        }

        [TestMethod]
        public void RenameParameter_AnonymousMethod()
        {
            const string code = @"
class A
{
    void foo()
    {
        System.Func<int, int> bar = delegate(int a) { return a + 1; };
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        System.Func<int, int> bar = delegate(int newParameterName) { return newParameterName + 1; };
    }
}
";
            TestSingleRefactoring<Refactoring.RenameParameterRefactoring>(code, expected, GetTextSpanFor("int a", code));
        }

        [TestMethod]
        public void RenameParameter_DelegateDeclaration_Nok()
        {
            const string code = @"
public delegate void foo(int a);
";
            TestNoRefactoring<Refactoring.RenameParameterRefactoring>(code, GetTextSpanFor("int a", code));
        }

        [TestMethod]
        public void RenameParameter_NameReservedByLocal()
        {
            const string code = @"
class A
{
    int foo(int a, int b)
    {
        int newParameterName = a;
        return 123;
    }
}
";
            TestNoRefactoring<Refactoring.RenameParameterRefactoring>(code, GetTextSpanFor("int b", code));
        }

        [TestMethod]
        public void RenameParameter_NameReservedByParameter()
        {
            const string code = @"
class A
{
    int foo(int newParameterName, int b)
    {
        int c = b;
        return 123;
    }
}
";
            TestNoRefactoring<Refactoring.RenameParameterRefactoring>(code, GetTextSpanFor("int b", code));
        }
    }
}