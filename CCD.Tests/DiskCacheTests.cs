using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CCD2;
using System.IO;

namespace CCD.Tests
{
    [TestClass]
    public class DiskCacheTests
    {
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
