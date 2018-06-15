using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test.ReplaceConstructorWithFactoryMethod
{
    [TestClass]
    public class ReplaceConstructorWithFactoryMethodUnitTest : TestFramework
    {
        [TestMethod]
        public void ReplaceConstructorWithFactoryMethod_SimpleCase()
        {
            const string code = @"
class A
{
    public A()
    {
    }
}

class B
{
    void foo()
    {
        new A();
    }
}
";
            const string expected = @"
class A
{
    private A()
    {
    }

    public static A CreateA()
    {
        return new A();
    }
}

class B
{
    void foo()
    {
        A.CreateA();
    }
}
";
            TestSingleRefactoring<Refactoring.ReplaceConstructorWithFactoryMethodRefactoring>(code, expected, GetTextSpanWithin("A", "public A()", code));
        }

        [TestMethod]
        public void ReplaceConstructorWithFactoryMethod_ConstructorWithParameters()
        {
            const string code = @"
class A
{
    public A(ref int a, out float f, string s)
    {
    }
}

class B
{
    void foo()
    {
        int i = 1;
        float f;
        new A(ref i, out f, ""test"");
    }
}
";
            const string expected = @"
class A
{
    private A(ref int a, out float f, string s)
    {
    }

    public static A CreateA(ref int a, out float f, string s)
    {
        return new A(ref a, out f, s);
    }
}

class B
{
    void foo()
    {
        int i = 1;
        float f;
        A.CreateA(ref i, out f, ""test"");
    }
}
";
            TestSingleRefactoring<Refactoring.ReplaceConstructorWithFactoryMethodRefactoring>(code, expected, GetTextSpanWithin("A", "public A", code));
        }

        [TestMethod]
        public void ReplaceConstructorWithFactoryMethod_StaticConstructor_Nok()
        {
            const string code = @"
class A
{
    static A()
    {
    }
}
";
            TestNoRefactoring<Refactoring.ReplaceConstructorWithFactoryMethodRefactoring>(code, GetTextSpanWithin("A", "static A()", code));
        }
    }
}
