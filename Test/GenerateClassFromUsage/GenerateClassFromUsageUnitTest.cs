using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Roslyn.Compilers.Common;
using Roslyn.Compilers.CSharp;

namespace Test.GenerateClassFromUsage
{
    [TestClass]
    public class GenerateClassFromUsageUnitTest : TestFramework
    {
        [TestMethod]
        public void GenerateClassFromUsage_NotGeneric()
        {
            const string code = @"
class A
{
    void foo()
    {
        new MyClass(1, ""test"", 3.14f, new A());
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        new MyClass(1, ""test"", 3.14f, new A());
    }
}

class MyClass
{
    private int param1;
    private string param2;
    private float param3;
    private A param4;

    public MyClass(int param1, string param2, float param3, A param4)
    {
        // TODO: Complete member initialization;
        this.param1 = param1;
        this.param2 = param2;
        this.param3 = param3;
        this.param4 = param4;
    }
}
";
            TestSingleIssue<Refactoring.GenerateClassFromUsageIssue, ObjectCreationExpressionSyntax>(code, expected, GetTextSpanFor("new MyClass", code));
        }

        [TestMethod]
        public void GenerateClassFromUsage_Generic()
        {
            const string code = @"
class A
{
    void foo()
    {
        new MyClass<int, A>(1, ""test"", 3.14f, new A());
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        new MyClass<int, A>(1, ""test"", 3.14f, new A());
    }
}

class MyClass<T, U>
{
    private T param1;
    private string param2;
    private float param3;
    private U param4;

    public MyClass(T param1, string param2, float param3, U param4)
    {
        // TODO: Complete member initialization;
        this.param1 = param1;
        this.param2 = param2;
        this.param3 = param3;
        this.param4 = param4;
    }
}
";
            TestSingleIssue<Refactoring.GenerateClassFromUsageIssue, ObjectCreationExpressionSyntax>(code, expected, GetTextSpanFor("new MyClass", code));
        }

        [TestMethod]
        public void GenerateClassFromUsage_GenericMultipleTypeParameters()
        {
            const string code = @"
class A
{
    void foo()
    {
        new MyClass<int, A, string, float, double>(1, ""test"", 3.14f, new A(), 'n');
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        new MyClass<int, A, string, float, double>(1, ""test"", 3.14f, new A(), 'n');
    }
}

class MyClass<T, U, V, TT, UU>
{
    private T param1;
    private V param2;
    private TT param3;
    private U param4;
    private char param5;

    public MyClass(T param1, V param2, TT param3, U param4, char param5)
    {
        // TODO: Complete member initialization;
        this.param1 = param1;
        this.param2 = param2;
        this.param3 = param3;
        this.param4 = param4;
        this.param5 = param5;
    }
}
";
            TestSingleIssue<Refactoring.GenerateClassFromUsageIssue, ObjectCreationExpressionSyntax>(code, expected, GetTextSpanFor("new MyClass", code));
        }

        [TestMethod]
        public void GenerateClassFromUsage_ClassAlreadyExist_Nok()
        {
            const string code = @"
class A
{
    void foo()
    {
        new MyClass();
    }
}

class MyClass
{
}
";
            TestNoIssue<Refactoring.GenerateClassFromUsageIssue, ObjectCreationExpressionSyntax>(code, GetTextSpanFor("new MyClass", code));
        }
    }
}
