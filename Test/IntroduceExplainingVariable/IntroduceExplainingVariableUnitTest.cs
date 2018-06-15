using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test.IntroduceExplainingVariable
{
    [TestClass]
    public class IntroduceExplainingVariableUnitTest : TestFramework
    {
        [TestMethod]
        public void IntroduceExplainingVariable_OneLevel()
        {
            const string code = @"
class A
{
    void foo()
    {
        int a = 1, b = 2, c = 3;
        if (a == 1 && b == 2 && 3 == c) { }
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int a = 1, b = 2, c = 3;
        bool isVar1 = a == 1;
        bool isVar2 = b == 2;
        bool isVar3 = 3 == c;
        if (isVar1 && isVar2 && isVar3) { }
    }
}
";
            TestSingleRefactoring<Refactoring.IntroduceExplainingVariableRefactoring>(code, expected, GetTextSpanFor("a == 1", code));
        }

        [TestMethod]
        public void IntroduceExplainingVariable_MultipleLevels()
        {
            const string code = @"
class A
{
    void foo()
    {
        int a = 1, b = 2, c = 3, d = 4;
        if ((a < 1 && b != 2) || (3 == c || d > 2)) { }
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int a = 1, b = 2, c = 3, d = 4;
        bool isVar1 = a < 1;
        bool isVar2 = b != 2;
        bool isVar3 = 3 == c;
        bool isVar4 = d > 2;
        if ((isVar1 && isVar2) || (isVar3 || isVar4)) { }
    }
}
";
            TestSingleRefactoring<Refactoring.IntroduceExplainingVariableRefactoring>(code, expected, GetTextSpanFor("a < 1", code));
        }

        [TestMethod]
        public void IntroduceExplainingVariable_NoParentBlock()
        {
            const string code = @"
class A
{
    void foo()
    {
        int a = 1, b = 2;
        for (;;)
            if (a == 1 && b == 2) { }
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int a = 1, b = 2;
        for (;;)
        {
            bool isVar1 = a == 1;
            bool isVar2 = b == 2;
            if (isVar1 && isVar2) { }
        }
    }
}
";
            TestSingleRefactoring<Refactoring.IntroduceExplainingVariableRefactoring>(code, expected, GetTextSpanFor("a == 1", code));
        }

        [TestMethod]
        public void IntroduceExplainingVariable_NoParentBlockIf()
        {
            const string code = @"
class A
{
    void foo()
    {
        int a = 1, b = 2;
        if (true)
            if (a == 1 && b == 2) { }
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int a = 1, b = 2;
        if (true)
        {
            bool isVar1 = a == 1;
            bool isVar2 = b == 2;
            if (isVar1 && isVar2) { }
        }
    }
}
";
            TestSingleRefactoring<Refactoring.IntroduceExplainingVariableRefactoring>(code, expected, GetTextSpanFor("a == 1", code));
        }

        [TestMethod]
        public void IntroduceExplainingVariable_DoWhile_Nok()
        {
            const string code = @"
class A
{
    void foo()
    {
        int a = 1, b = 2;
        do
        {
        }
        while (a == 1 && b == 2);
    }
}
";
            TestNoRefactoring<Refactoring.IntroduceExplainingVariableRefactoring>(code, GetTextSpanFor("a == 1", code));
        }

        [TestMethod]
        public void IntroduceExplainingVariable_ElseIf()
        {
            const string code = @"
class A
{
    void foo()
    {
        int a = 1, b = 2;
        if (true)
        {
        }
        else if (a == 1 && b == 2)
        {
        }
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int a = 1, b = 2;
        if (true)
        {
        }
        else
        {
            bool isVar1 = a == 1;
            bool isVar2 = b == 2;
            if (isVar1 && isVar2)
            {
            }
        }
    }
}
";
            TestSingleRefactoring<Refactoring.IntroduceExplainingVariableRefactoring>(code, expected, GetTextSpanFor("a == 1", code));
        }

        [TestMethod]
        public void IntroduceExplainingVariable_MultiplyParenthesized()
        {
            const string code = @"
class A
{
    void foo()
    {
        int a = 1;
        if ((((a == 1))))
        {
        }
    }
}
";
            const string expected = @"
class A
{
    void foo()
    {
        int a = 1;
        bool isVar1 = a == 1;
        if (((isVar1)))
        {
        }
    }
}
";
            TestSingleRefactoring<Refactoring.IntroduceExplainingVariableRefactoring>(code, expected, GetTextSpanFor("(((a == 1)))", code));
        }
    }
}
