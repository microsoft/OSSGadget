﻿using Microsoft.CST.OpenSource;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;

namespace Microsoft.CST.OpenSource.Tests
{
    [TestClass]
    public class FindSquatsTest
    {
        string decoded = "The quick brown fox jumped over the lazy dog.";

        public FindSquatsTest()
        {
        }

        [DataTestMethod]
        [DataRow("pkg:nuget/Microsoft.CST.OAT", false)]
        [DataRow("pkg:npm/microsoft/microsoft-graph-library", false)]
        [DataRow("pkg:npm/foo", true)]

        public async Task DetectSquats(string packageUrl, bool expectedToHaveSquats)
        {
            var fst = new FindSquatsTool();
            var options = new FindSquatsTool.Options()
            {
                Quiet = true,
                Targets = new string[] { packageUrl }
            };
            var result = await fst.RunAsync(options);
            Assert.IsTrue(expectedToHaveSquats ? result.numSquats > 0 : result.numSquats == 0);
        }
    }
}
