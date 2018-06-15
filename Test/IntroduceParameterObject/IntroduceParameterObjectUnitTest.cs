using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test.IntroduceParameterObject
{
    [TestClass]
    public class IntroduceParameterObjectUnitTest : TestFramework
    {
        [TestMethod]
        public void IntroduceParameterObject_AllParameters()
        {
            const string code = @"
class A
{
    int foo(int a, float f)
    {
        int b = a;
        float d = f;
        return 123;
    }

    void bar()
    {
        foo(5, 3.14f);
        foo(5, f: 3.14f);
        foo(f: 3.14f, a: 5);
    }
}
";
            const string expected = @"
class A
{
    int foo(ParameterObject parameterObject)
    {
        int b = parameterObject.a;
        float d = parameterObject.f;
        return 123;
    }

    void bar()
    {
        foo(new ParameterObject(5, 3.14f));
        foo(new ParameterObject(5, f: 3.14f));
        foo(parameterObject: new ParameterObject(f: 3.14f, a: 5));
    }
}

class ParameterObject
{
    public int a { get; set; }
    public float f { get; set; }

    public ParameterObject(int a, float f)
    {
        this.a = a;
        this.f = f;
    }
}
";
            TestSingleRefactoring<Refactoring.IntroduceParameterObjectRefactoring>(code, expected, GetTextSpanFor("int a, float f", code));
        }

        [TestMethod]
        public void IntroduceParameterObject_SelectedParameters()
        {
            const string code = @"
class A
{
    int foo(int i, A a, string s, float f)
    {
        int b = i;
        float d = f;
        a.ToString();
        s = """";
        return 123;
    }

    void bar()
    {
        foo(5, null, ""test"", 3.14f);
        foo(5, null, s: ""test"", f: 3.14f);
        foo(f: 3.14f, a: null, i: 5, s: ""test"");
    }
}
";
            const string expected = @"
class A
{
    int foo(int i, ParameterObject parameterObject, float f)
    {
        int b = i;
        float d = f;
        parameterObject.a.ToString();
        parameterObject.s = """";
        return 123;
    }

    void bar()
    {
        foo(5, new ParameterObject(null, ""test""), 3.14f);
        foo(5, new ParameterObject(null, s: ""test""), f: 3.14f);
        foo(f: 3.14f, parameterObject: new ParameterObject(a: null, s: ""test""), i: 5);
    }
}

class ParameterObject
{
    public A a { get; set; }
    public string s { get; set; }

    public ParameterObject(A a, string s)
    {
        this.a = a;
        this.s = s;
    }
}
";
            TestSingleRefactoring<Refactoring.IntroduceParameterObjectRefactoring>(code, expected, GetTextSpanFor("A a, string s", code));
        }

        [TestMethod]
        public void IntroduceParameterObject_NestedCalls()
        {
            const string code = @"
class A
{
    int foo(int i, A a, string s, float f)
    {
        return i;
    }

    void bar()
    {
        foo(f: 3.14f, a: null, i: foo(1, null, f: 3.14f, s: ""test""), s: ""test"");
    }
}
";
            const string expected = @"
class A
{
    int foo(int i, ParameterObject parameterObject, float f)
    {
        return i;
    }

    void bar()
    {
        foo(f: 3.14f, parameterObject: new ParameterObject(a: null, s: ""test""), i: foo(1, new ParameterObject(null, s: ""test""), f: 3.14f));
    }
}

class ParameterObject
{
    public A a { get; set; }
    public string s { get; set; }

    public ParameterObject(A a, string s)
    {
        this.a = a;
        this.s = s;
    }
}
";
            TestSingleRefactoring<Refactoring.IntroduceParameterObjectRefactoring>(code, expected, GetTextSpanFor("A a, string s", code));
        }

        [TestMethod]
        public void IntroduceParameterObject_ConstructorAndNestedCalls()
        {
            const string code = @"
class A
{
    public A(int i, A a, string s, float f)
    {
        string z = s;
    }

    void bar()
    {
        new A(f: 3.14f, a: new A(1, null, f: 3.14f, s: ""test""), i: 3, s: ""test"");
    }
}
";
            const string expected = @"
class A
{
    public A(int i, ParameterObject parameterObject, float f)
    {
        string z = parameterObject.s;
    }

    void bar()
    {
        new A(f: 3.14f, parameterObject: new ParameterObject(a: new A(1, new ParameterObject(null, s: ""test""), f: 3.14f), s: ""test""), i: 3);
    }
}

class ParameterObject
{
    public A a { get; set; }
    public string s { get; set; }

    public ParameterObject(A a, string s)
    {
        this.a = a;
        this.s = s;
    }
}
";
            TestSingleRefactoring<Refactoring.IntroduceParameterObjectRefactoring>(code, expected, GetTextSpanFor("A a, string s", code));
        }

        [TestMethod]
        public void IntroduceParameterObject_RefParameter_Nok()
        {
            const string code = @"
class A
{
    int foo(int i, A a, ref string s, float f)
    {
        return i;
    }
}
";
            TestNoRefactoring<Refactoring.IntroduceParameterObjectRefactoring>(code, GetTextSpanFor("A a, ref string s", code));
        }

        [TestMethod]
        public void IntroduceParameterObject_OutParameter_Nok()
        {
            const string code = @"
class A
{
    int foo(int i, A a, out string s, float f)
    {
        return i;
    }
}
";
            TestNoRefactoring<Refactoring.IntroduceParameterObjectRefactoring>(code, GetTextSpanFor("A a, out string s", code));
        }

        [TestMethod]
        public void IntroduceParameterObject_Operator_Nok()
        {
            const string code = @"
class A
{
    public static int operator +(A lhs, A rhs)
    {
        return 123;
    }
}
";
            TestNoRefactoring<Refactoring.IntroduceParameterObjectRefactoring>(code, GetTextSpanFor("A lhs, A rhs", code));
        }

        [TestMethod]
        public void IntroduceParameterObject_ConversionOperator_Nok()
        {
            const string code = @"
class A
{
    public static implicit operator int(A lhs)
    {
        return 123;
    }
}
";
            TestNoRefactoring<Refactoring.IntroduceParameterObjectRefactoring>(code, GetTextSpanFor("A lhs", code));
        }

        [TestMethod]
        public void IntroduceParameterObject_AnonymousMethod_Nok()
        {
            const string code = @"
class A
{
    void foo()
    {
        Func<int, int> bar = delegate(int a) { return 1; };
    }
}
";
            TestNoRefactoring<Refactoring.IntroduceParameterObjectRefactoring>(code, GetTextSpanFor("int a", code));
        }

        [TestMethod]
        public void IntroduceParameterObject_DelegateDeclaration_Nok()
        {
            const string code = @"
public delegate void foo(int a, int b);
";
            TestNoRefactoring<Refactoring.IntroduceParameterObjectRefactoring>(code, GetTextSpanFor("int a, int b", code));
        }

        [TestMethod]
        public void IntroduceParameterObject_SimpleLambda_Nok()
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
            TestNoRefactoring<Refactoring.IntroduceParameterObjectRefactoring>(code, GetTextSpanWithin("a", "= a =>", code));
        }

        [TestMethod]
        public void IntroduceParameterObject_ParenthesizedLambda_Nok()
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
            TestNoRefactoring<Refactoring.IntroduceParameterObjectRefactoring>(code, GetTextSpanWithin("a", "(a)", code));
        }

        [TestMethod]
        public void IntroduceParameterObject_ExplicitInterfaceMethod_Nok()
        {
            const string code = @"
interface I
{
    int foo(int a);
}

class A : I
{
    int I.foo(int a)
    {
        return 123;
    }
}
";
            TestNoRefactoring<Refactoring.IntroduceParameterObjectRefactoring>(code, GetTextSpanWithin("int a", "I.foo(int a)", code));
        }
    }
}
