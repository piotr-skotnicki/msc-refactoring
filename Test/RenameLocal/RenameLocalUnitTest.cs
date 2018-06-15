using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test.RenameLocal
{
    [TestClass]
    public class RenameLocalUnitTest : TestFramework
    {
        [TestMethod]
        public void RenameLocal_Rename()
        {
            const string code = @"
class A
{
    void foo()
    {
        int local = 1;
        int a = local;
        bar(ref local);
        bar(p: ref local);
        local.ToString();
    }

    void bar(ref int p)
    {
        p = 1;
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int newVariableName = 1;
        int a = newVariableName;
        bar(ref newVariableName);
        bar(p: ref newVariableName);
        newVariableName.ToString();
    }

    void bar(ref int p)
    {
        p = 1;
    }
}
";
            TestSingleRefactoring<Refactoring.RenameLocalRefactoring>(code, expected, GetTextSpanWithin("local", "int local", code));
        }

        [TestMethod]
        public void RenameLocal_NameReservedByLocal_Nok()
        {
            const string code = @"
class A
{
    void foo()
    {
        int newVariableName = 1;
        int local = 1;
        int a = local;
    }
}
";
            TestNoRefactoring<Refactoring.RenameLocalRefactoring>(code, GetTextSpanWithin("local", "int local", code));
        }

        [TestMethod]
        public void RenameLocal_NameReservedByParameter_Nok()
        {
            const string code = @"
class A
{
    void foo(int newVariableName)
    {
        int local = 1;
        int a = local;
    }
}
";
            TestNoRefactoring<Refactoring.RenameLocalRefactoring>(code, GetTextSpanWithin("local", "int local", code));
        }

        [TestMethod]
        public void RenameLocal_NameReservedByClass()
        {
            const string code = @"
class A
{
    class newVariableName { }

    void foo()
    {
        int local = 1;
        int a = local;
    }
}
";
            const string expected = @"
class A
{
    class newVariableName { }

    void foo()
    {
        int newVariableName = 1;
        int a = newVariableName;
    }
}
";
            TestSingleRefactoring<Refactoring.RenameLocalRefactoring>(code, expected, GetTextSpanWithin("local", "int local", code));
        }

        [TestMethod]
        public void RenameLocal_NameReservedByField()
        {
            const string code = @"
class A
{
    int newVariableName;

    void foo()
    {
        int local = 1;
        int a = local;
    }
}
";
            const string expected = @"
class A
{
    int newVariableName;

    void foo()
    {
        int newVariableName = 1;
        int a = newVariableName;
    }
}
";
            TestSingleRefactoring<Refactoring.RenameLocalRefactoring>(code, expected, GetTextSpanWithin("local", "int local", code));
        }

        [TestMethod]
        public void RenameLocal_QualifyFieldReference()
        {
            const string code = @"
class A
{
    int newVariableName;

    void foo()
    {
        int local = 1;
        int a = local;
        int b = newVariableName;
    }
}
";
            const string expected = @"
class A
{
    int newVariableName;

    void foo()
    {
        int newVariableName = 1;
        int a = newVariableName;
        int b = this.newVariableName;
    }
}
";
            TestSingleRefactoring<Refactoring.RenameLocalRefactoring>(code, expected, GetTextSpanWithin("local", "int local", code));
        }

        [TestMethod]
        public void RenameLocal_QualifyStaticFieldReference()
        {
            const string code = @"
class A
{
    static int newVariableName;

    void foo()
    {
        int local = 1;
        int a = local;
        int b = newVariableName;
    }
}
";
            const string expected = @"
class A
{
    static int newVariableName;

    void foo()
    {
        int newVariableName = 1;
        int a = newVariableName;
        int b = A.newVariableName;
    }
}
";
            TestSingleRefactoring<Refactoring.RenameLocalRefactoring>(code, expected, GetTextSpanWithin("local", "int local", code));
        }

        [TestMethod]
        public void RenameLocal_QualifyMethodReference()
        {
            const string code = @"
class A
{
    int newVariableName()
    {
        return 123;
    }

    void foo()
    {
        int local = 1;
        int a = local;
        int b = newVariableName();
    }
}
";
            const string expected = @"
class A
{
    int newVariableName()
    {
        return 123;
    }

    void foo()
    {
        int newVariableName = 1;
        int a = newVariableName;
        int b = this.newVariableName();
    }
}
";
            TestSingleRefactoring<Refactoring.RenameLocalRefactoring>(code, expected, GetTextSpanWithin("local", "int local", code));
        }

        [TestMethod]
        public void RenameLocal_DontQualifyNonTopLevelFieldReference()
        {
            const string code = @"
class A
{
    B b = new B();

    void foo()
    {
        int local = 1;
        int x = local;
        b.newVariableName = 123;
        A a = new A();
        a.b.newVariableName = 456;
    }
}

class B
{
    int newVariableName;
}
";
            const string expected = @"
class A
{
    B b = new B();

    void foo()
    {
        int newVariableName = 1;
        int x = newVariableName;
        b.newVariableName = 123;
        A a = new A();
        a.b.newVariableName = 456;
    }
}

class B
{
    int newVariableName;
}
";
            TestSingleRefactoring<Refactoring.RenameLocalRefactoring>(code, expected, GetTextSpanWithin("local", "int local", code));
        }

        [TestMethod]
        public void RenameLocal_AllowSameNameAsType()
        {
            const string code = @"
class A
{
    class newVariableName { }

    void foo()
    {
        int local = 1;
        int x = local;
        newVariableName c;
    }
}
";
            const string expected = @"
class A
{
    class newVariableName { }

    void foo()
    {
        int newVariableName = 1;
        int x = newVariableName;
        newVariableName c;
    }
}
";
            TestSingleRefactoring<Refactoring.RenameLocalRefactoring>(code, expected, GetTextSpanWithin("local", "int local", code));
        }
    }
}
