using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CCD2;
using System.IO;

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
        public void DataRoundTrip()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);

            DiskIndexCache.AppDir = tempPath;

            string newRoot = @"c:\windows\";
            var roots = DiskIndexCache.LoadRoots();
            Assert.AreEqual(0, roots.Length, "No roots in the temp directory");
            DiskIndexCache.AddRoot(newRoot);
            roots = DiskIndexCache.LoadRoots();
            Assert.AreEqual(1, roots.Length, "One root present after we added it");
            Assert.AreEqual(newRoot, roots[0], "Contains the root we added");
        }
    }
}
