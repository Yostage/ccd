using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CCD.Tests
{
    [TestClass]
    public class QueryTests
    {
        /*
         * test cases:
         * query miss returns a miss
         * exact match: a/b/c matches "a c"
         * substring matches: aa/bb/cc matches "b"
         * exact match beats substring match
         * out of order match : a/b/c not match c a
         * matches in your root are preferred to matches out of your root
         */
        [TestMethod]
        public void TestMethod1()
        {
        }
    }
}
