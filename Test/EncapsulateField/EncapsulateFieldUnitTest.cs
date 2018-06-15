using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test.EncapsulateField
{
    [TestClass]
    public class EncapsulateFieldUnitTest : TestFramework
    {
        [TestMethod]
        public void EncapsulateField_PublicField()
        {
            const string code = @"
class A
{
    public int field = 1;
}
";
            const string expected = @"
class A
{
    private int field = 1;

    public int GetField
    {
        get
        {
            return field;
        }

        set
        {
            field = value;
        }
    }
}
";
            TestSingleRefactoring<Refactoring.EncapsulateFieldRefactoring>(code, expected, GetTextSpanFor("field", code));
        }

        [TestMethod]
        public void EncapsulateField_InternalProtectedField()
        {
            const string code = @"
class A
{
    internal protected int field = 1;
}
";
            const string expected = @"
class A
{
    private int field = 1;

    public int GetField
    {
        get
        {
            return field;
        }

        set
        {
            field = value;
        }
    }
}
";
            TestSingleRefactoring<Refactoring.EncapsulateFieldRefactoring>(code, expected, GetTextSpanFor("field", code));
        }

        [TestMethod]
        public void EncapsulateField_AlreadyHasGetterProperty_Nok()
        {
            const string code = @"
class A
{
    int field = 1;

    public int Field
    {
        get { return this.field; }
    }
}
";
            TestNoRefactoring<Refactoring.EncapsulateFieldRefactoring>(code, GetTextSpanFor("field", code));
        }

        [TestMethod]
        public void EncapsulateField_AlreadyHasGetterMethod_Nok()
        {
            const string code = @"
class A
{
    int field = 1;

    public int GetField()
    {
        return this.field;
    }
}
";
            TestNoRefactoring<Refactoring.EncapsulateFieldRefactoring>(code, GetTextSpanFor("field", code));
        }

        [TestMethod]
        public void EncapsulateField_ReplaceAllReferences()
        {
            const string code = @"
class A
{
    public int field = 1;
}

class B
{
    void foo()
    {
        A a = new A();
        a.field = 1;
        int b = a.field;
    }
}
";
            const string expected = @"
class A
{
    private int field = 1;

    public int GetField
    {
        get
        {
            return field;
        }

        set
        {
            field = value;
        }
    }
}

class B
{
    void foo()
    {
        A a = new A();
        a.GetField = 1;
        int b = a.GetField;
    }
}
";
            TestSingleRefactoring<Refactoring.EncapsulateFieldRefactoring>(code, expected, GetTextSpanFor("field", code));
        }
    }
}
