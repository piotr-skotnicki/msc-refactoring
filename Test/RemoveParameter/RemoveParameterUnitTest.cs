using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Roslyn.Compilers.Common;
using Roslyn.Compilers.CSharp;

namespace Test.RemoveParameter
{
    [TestClass]
    public class RemoveParameterUnitTest : TestFramework
    {
        [TestMethod]
        public void RemoveParameter_IsNotUsed()
        {
            const string code = @"
class A
{
    void foo(int p)
    {
        int a = 123;
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int a = 123;
    }
}
";
            TestSingleIssue<Refactoring.RemoveParameterIssue, ParameterSyntax>(code, expected, GetTextSpanFor("int p", code));
        }

        [TestMethod]
        public void RemoveParameter_NestedInvocations()
        {
            const string code = @"
class A
{
    int foo(int a, int b)
    {
        return a;
    }

    void bar()
    {
        foo(b: foo(a: 1, b: 2), a: foo(foo(b: 2, a: 1), 2));
    }
}
";
            const string expected = @"
class A
{
    int foo(int a)
    {
        return a;
    }

    void bar()
    {
        foo(a: foo(foo(a: 1)));
    }
}
";
            TestSingleIssue<Refactoring.RemoveParameterIssue, ParameterSyntax>(code, expected, GetTextSpanFor("int b", code));
        }

        [TestMethod]
        public void RemoveParameter_Ctor()
        {
            const string code = @"
class A
{
    public A(int a, A b)
    {
        int c = a;
    }

    void bar()
    {
        new A(b: new A(a: 1, b: new A(1, null)), a: 2);
    }
}
";
            const string expected = @"
class A
{
    public A(int a)
    {
        int c = a;
    }

    void bar()
    {
        new A(a: 2);
    }
}
";
            TestSingleIssue<Refactoring.RemoveParameterIssue, ParameterSyntax>(code, expected, GetTextSpanFor("A b", code));
        }

        [TestMethod]
        public void RemoveParameter_IsUsed_Nok()
        {
            const string code = @"
class A
{
    void foo(int p)
    {
        int a = p;
    }
}
";
            TestNoIssue<Refactoring.RemoveParameterIssue, ParameterSyntax>(code, GetTextSpanFor("int p", code));
        }

        [TestMethod]
        public void RemoveParameter_Operator_Nok()
        {
            const string code = @"
class A
{
    public static A operator +(A rhs)
    {
        return new A();
    }
}
";
            TestNoIssue<Refactoring.RemoveParameterIssue, ParameterSyntax>(code, GetTextSpanFor("A rhs", code));
        }

        [TestMethod]
        public void RemoveParameter_ConversionOperator_Nok()
        {
            const string code = @"
class A
{
    public static explicit operator B(A rhs)
    {
        return new B();
    }
}

class B
{
}
";
            TestNoIssue<Refactoring.RemoveParameterIssue, ParameterSyntax>(code, GetTextSpanFor("A rhs", code));
        }

        [TestMethod]
        public void RemoveParameter_OverridenMethod_Nok()
        {
            const string code = @"
class A
{
    public override bool Equals(object obj)
    {
        return false;
    }
}
";
            TestNoIssue<Refactoring.RemoveParameterIssue, ParameterSyntax>(code, GetTextSpanFor("object obj", code));
        }

        [TestMethod]
        public void RemoveParameter_ExplicitInterfaceMethod_Nok()
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
            TestNoIssue<Refactoring.RemoveParameterIssue, ParameterSyntax>(code, GetTextSpanWithin("int a", "I.foo(int a)", code));
        }

        [TestMethod]
        public void RemoveParameter_DelegateDeclaration_Nok()
        {
            const string code = @"
public delegate void foo(int a, int b);
";
            TestNoIssue<Refactoring.RemoveParameterIssue, ParameterSyntax>(code, GetTextSpanFor("int a", code));
        }
    }
}