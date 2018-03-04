using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;


using Delta;
namespace TestDelta
{
    [TestClass]
    public class TestCompressDelDirs
    {
        [TestMethod]
        public void TestSimple1()
        {
            var dirs = new List<string>() { "a", "b", "c", "a\\x" };
            dirs.Sort();
            List<string> compressed = DeltaSortedFileLists.CompressToBaseDirs(dirs).ToList();
            Assert.AreEqual(3, compressed.Count);
        }
        [TestMethod]
        public void TestCompressToOne()
        {
            var dirs = new List<string>() { "a", "a\\b", "a\\c", "a\\x" };
            dirs.Sort();
            List<string> compressed = DeltaSortedFileLists.CompressToBaseDirs(dirs).ToList();
            Assert.AreEqual(1, compressed.Count);
        }
        [TestMethod]
        public void TestCompressToTwo()
        {
            var dirs = new List<string>() { "x", "a", "a\\b", "a\\c", "a\\x", "x\\x" };
            dirs.Sort();
            List<string> compressed = DeltaSortedFileLists.CompressToBaseDirs(dirs).ToList();
            Assert.AreEqual(2, compressed.Count);
        }
    }
}
