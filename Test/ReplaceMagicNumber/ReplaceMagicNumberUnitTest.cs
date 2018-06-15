using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test.ReplaceMagicNumber
{
    [TestClass]
    public class ReplaceMagicNumberUnitTest : TestFramework
    {
        [TestMethod]
        public void ReplaceMagicNumber_ReplaceAllOccurences()
        {
            const string code = @"
class A
{
    void foo()
    {
        int a = 123;
        int b = 123 + a;
        foo(123);
        123.ToString();
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        const int MAGIC_NUMBER = 123;
        int a = MAGIC_NUMBER;
        int b = MAGIC_NUMBER + a;
        foo(MAGIC_NUMBER);
        MAGIC_NUMBER.ToString();
    }
}
";
            TestSingleRefactoring<Refactoring.ReplaceMagicNumberRefactoring>(code, expected, GetTextSpanFor("123", code));
        }

        [TestMethod]
        public void ReplaceMagicNumber_HandleSuffixesAndDots()
        {
            const string code = @"
class A
{
    void foo()
    {
        double a = 123d;
        double b = 123.0 + a;
        foo(123.0d);
        123.0.ToString();
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        const double MAGIC_NUMBER = 123d;
        double a = MAGIC_NUMBER;
        double b = MAGIC_NUMBER + a;
        foo(MAGIC_NUMBER);
        MAGIC_NUMBER.ToString();
    }
}
";
            TestSingleRefactoring<Refactoring.ReplaceMagicNumberRefactoring>(code, expected, GetTextSpanFor("123d", code));
        }

        [TestMethod]
        public void ReplaceMagicNumber_HandleLeadingZeroes()
        {
            const string code = @"
class A
{
    void foo()
    {
        int a = 0123 + 00123;
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        const int MAGIC_NUMBER = 0123;
        int a = MAGIC_NUMBER + MAGIC_NUMBER;
    }
}
";
            TestSingleRefactoring<Refactoring.ReplaceMagicNumberRefactoring>(code, expected, GetTextSpanFor("0123", code));
        }
    }
}
