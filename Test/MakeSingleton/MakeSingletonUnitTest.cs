using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test.MakeSingleton
{
    [TestClass]
    public class MakeSingletonUnitTest : TestFramework
    {
        [TestMethod]
        public void MakeSingleton_NoConstructor()
        {
            const string code = @"
class A
{
}

class B
{
    void foo()
    {
        A a = new A();
        new A().ToString();
    }
}
";
            const string expected = @"
class A
{
    static public A Instance()
    {
        if (instance == null)
        {
            instance = new A();
        }
        return instance;
    }

    static private A instance;
}

class B
{
    void foo()
    {
        A a = A.Instance();
        A.Instance().ToString();
    }
}
";
            TestSingleRefactoring<Refactoring.MakeSingletonRefactoring>(code, expected, GetTextSpanWithin("A", "class A", code));
        }

        [TestMethod]
        public void MakeSingleton_NoParametersInConstructor()
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
        A a = new A();
        new A().ToString();
    }
}
";
            const string expected = @"
class A
{
    private A()
    {
    }

    static public A Instance()
    {
        if (instance == null)
        {
            instance = new A();
        }
        return instance;
    }

    static private A instance;
}

class B
{
    void foo()
    {
        A a = A.Instance();
        A.Instance().ToString();
    }
}
";
            TestSingleRefactoring<Refactoring.MakeSingletonRefactoring>(code, expected, GetTextSpanWithin("A", "class A", code));
        }

        [TestMethod]
        public void MakeSingleton_ParametrizedConstructor_Nok()
        {
            const string code = @"
class A
{
    public A(int a, int b)
    {
    }
}
";
            TestNoRefactoring<Refactoring.MakeSingletonRefactoring>(code, GetTextSpanWithin("A", "class A", code));
        }
    }
}
