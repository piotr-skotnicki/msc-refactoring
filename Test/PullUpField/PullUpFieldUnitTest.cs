using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test.PullUpField
{
    [TestClass]
    public class PullUpFieldUnitTest : TestFramework
    {
        [TestMethod]
        public void PullUpField_SingleDocument()
        {
            const string code = @"
class A
{
}

class B : A
{
    int field;
}
";
            const string expected = @"
class A
{
    int field;
}

class B : A
{
}
";
            TestSingleRefactoring<Refactoring.PullUpFieldRefactoring>(code, expected, GetTextSpanFor("field", code));
        }
    }
}
