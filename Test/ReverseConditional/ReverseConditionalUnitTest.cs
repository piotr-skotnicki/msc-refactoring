using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test.ReverseConditional
{
    [TestClass]
    public class ReverseConditionalUnitTest : TestFramework
    {
        [TestMethod]
        public void ReverseConditional_EqualsEquals()
        {
            const string code = @"
class A
{
    void foo()
    {
        int a = 1, b = 2, c = 3;
        if (a == b) c = 4;
        else c = 5;
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int a = 1, b = 2, c = 3;
        if (a != b) c = 5;
        else c = 4;
    }
}
";
            TestSingleRefactoring<Refactoring.ReverseConditionalRefactoring>(code, expected, GetTextSpanFor("a == b", code));
        }

        [TestMethod]
        public void ReverseConditional_NotEquals()
        {
            const string code = @"
class A
{
    void foo()
    {
        int a = 1, b = 2, c = 3;
        if (a != b) c = 4;
        else c = 5;
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int a = 1, b = 2, c = 3;
        if (a == b) c = 5;
        else c = 4;
    }
}
";
            TestSingleRefactoring<Refactoring.ReverseConditionalRefactoring>(code, expected, GetTextSpanFor("a != b", code));
        }

        [TestMethod]
        public void ReverseConditional_GreaterThan()
        {
            const string code = @"
class A
{
    void foo()
    {
        int a = 1, b = 2, c = 3;
        if (a > b) c = 4;
        else c = 5;
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int a = 1, b = 2, c = 3;
        if (a <= b) c = 5;
        else c = 4;
    }
}
";
            TestSingleRefactoring<Refactoring.ReverseConditionalRefactoring>(code, expected, GetTextSpanFor("a > b", code));
        }

        [TestMethod]
        public void ReverseConditional_GreaterThanEquals()
        {
            const string code = @"
class A
{
    void foo()
    {
        int a = 1, b = 2, c = 3;
        if (a >= b) c = 4;
        else c = 5;
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int a = 1, b = 2, c = 3;
        if (a < b) c = 5;
        else c = 4;
    }
}
";
            TestSingleRefactoring<Refactoring.ReverseConditionalRefactoring>(code, expected, GetTextSpanFor("a >= b", code));
        }

        [TestMethod]
        public void ReverseConditional_LessThan()
        {
            const string code = @"
class A
{
    void foo()
    {
        int a = 1, b = 2, c = 3;
        if (a < b) c = 4;
        else c = 5;
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int a = 1, b = 2, c = 3;
        if (a >= b) c = 5;
        else c = 4;
    }
}
";
            TestSingleRefactoring<Refactoring.ReverseConditionalRefactoring>(code, expected, GetTextSpanFor("a < b", code));
        }

        [TestMethod]
        public void ReverseConditional_LessThanEquals()
        {
            const string code = @"
class A
{
    void foo()
    {
        int a = 1, b = 2, c = 3;
        if (a <= b) c = 4;
        else c = 5;
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int a = 1, b = 2, c = 3;
        if (a > b) c = 5;
        else c = 4;
    }
}
";
            TestSingleRefactoring<Refactoring.ReverseConditionalRefactoring>(code, expected, GetTextSpanFor("a <= b", code));
        }

        [TestMethod]
        public void ReverseConditional_AlreadyNegated()
        {
            const string code = @"
class A
{
    void foo()
    {
        int a = 1, b = 2, c = 3;
        if (!(a == b)) c = 4;
        else c = 5;
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int a = 1, b = 2, c = 3;
        if (a == b) c = 5;
        else c = 4;
    }
}
";
            TestSingleRefactoring<Refactoring.ReverseConditionalRefactoring>(code, expected, GetTextSpanFor("a == b", code));
        }

        [TestMethod]
        public void ReverseConditional_DeMorganAndToOr()
        {
            const string code = @"
class A
{
    void foo()
    {
        int a = 1, b = 2, c = 3, d = 4;
        if (a == b && c > 5) d = 6;
        else d = 7;
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int a = 1, b = 2, c = 3, d = 4;
        if (a != b || c <= 5) d = 7;
        else d = 6;
    }
}
";
            TestSingleRefactoring<Refactoring.ReverseConditionalRefactoring>(code, expected, GetTextSpanFor("a == b && c > 5", code));
        }

        [TestMethod]
        public void ReverseConditional_DeMorganOrToAnd()
        {
            const string code = @"
class A
{
    void foo()
    {
        int a = 1, b = 2, c = 3, d = 4;
        if (a == b || c > 5) d = 6;
        else d = 7;
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int a = 1, b = 2, c = 3, d = 4;
        if (a != b && c <= 5) d = 7;
        else d = 6;
    }
}
";
            TestSingleRefactoring<Refactoring.ReverseConditionalRefactoring>(code, expected, GetTextSpanFor("a == b || c > 5", code));
        }

        [TestMethod]
        public void ReverseConditional_DeMorganOrToAndWithUnaryExpressions()
        {
            const string code = @"
class A
{
    void foo()
    {
        bool a = false, b = true, c;
        if (a || b) c = true;
        else c = false;
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        bool a = false, b = true, c;
        if (!a && !b) c = false;
        else c = true;
    }
}
";
            TestSingleRefactoring<Refactoring.ReverseConditionalRefactoring>(code, expected, GetTextSpanFor("a || b", code));
        }

        [TestMethod]
        public void ReverseConditional_Swap()
        {
            const string code = @"
class A
{
    void foo()
    {
        int a = 1, b = 2, c = 3;
        if (a == b) c = 4;
        else if (a == c) b = 5;
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int a = 1, b = 2, c = 3;
        if (a == c) b = 5;
        else if (a == b) c = 4;
    }
}
";
            TestSingleRefactoring<Refactoring.ReverseConditionalRefactoring>(code, expected, GetTextSpanFor("a == b", code));
        }

        [TestMethod]
        public void ReverseConditional_FalseLiteralExpression()
        {
            const string code = @"
class A
{
    void foo()
    {
        int c = 1;
        if (false) c = 2;
        else c = 3;
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int c = 1;
        if (true) c = 3;
        else c = 2;
    }
}
";
            TestSingleRefactoring<Refactoring.ReverseConditionalRefactoring>(code, expected, GetTextSpanFor("false", code));
        }

        [TestMethod]
        public void ReverseConditional_TrueLiteralExpression()
        {
            const string code = @"
class A
{
    void foo()
    {
        int c = 1;
        if (true) c = 2;
        else c = 3;
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int c = 1;
        if (false) c = 3;
        else c = 2;
    }
}
";
            TestSingleRefactoring<Refactoring.ReverseConditionalRefactoring>(code, expected, GetTextSpanFor("true", code));
        }

        [TestMethod]
        public void ReverseConditional_MultipleElseIfs_Nok()
        {
            const string code = @"
class A
{
    void foo()
    {
        int a = 1, b = 2, c = 3;
        if (a == b) c = 4;
        else if (a == c) b = 5;
        else if (c == b) a = 6;
    }
}
";
            TestNoRefactoring<Refactoring.ReverseConditionalRefactoring>(code, GetTextSpanFor("a == b", code));
        }
    }
}
