using Microsoft.CST.OpenSource;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Markup;
using Microsoft.CST.OpenSource.Shared;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.Tests
{
    [TestClass]
    public class SharedTests
    {
        public SharedTests()
        {
        }

        [DataTestMethod]
        [DataRow("1.2.3")]
        [DataRow("v1.2.3")]
        [DataRow("v123.456.abc.789")]
        [DataRow(".123")]
        [DataRow("5")]
        [DataRow("1.2.3-release1")]
        public async Task VersionParseSucceeds(string versionString)
        {
            var result = VersionComparer.Parse(versionString);
            Assert.AreEqual(string.Join("", result), versionString);
        }
    }
}
