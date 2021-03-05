using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.Tests
{
    [TestClass]
    public class FindSquatsTest
    {
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
