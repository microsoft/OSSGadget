using Microsoft.CST.OpenSource.FindSquats;
using Microsoft.CST.OpenSource.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.Tests
{
    [TestClass]
    public class FindSquatsTest
    {
        public FindSquatsTest()
        {
            CommonInitialization.Initialize();
        }

        //[DataTestMethod]
        //[DataRow("pkg:nuget/Microsoft.CST.OAT", false)]
        //[DataRow("pkg:npm/microsoft/microsoft-graph-library", false)]
        //[DataRow("pkg:npm/foo", true)]
        //public async Task DetectSquats(string packageUrl, bool expectedToHaveSquats)
        //{
        //    var fst = new FindSquatsTool();
        //    var options = new FindSquatsTool.Options()
        //    {
        //        Quiet = true,
        //        Targets = new string[] { packageUrl }
        //    };
        //    var result = await fst.RunAsync(options);
        //    Assert.IsTrue(expectedToHaveSquats ? result.numSquats > 0 : result.numSquats == 0);
        //}

        //[DataTestMethod]
        //[DataRow("pkg:npm/foo", "foojs")]
        //[DataRow("pkg:nuget/Microsoft.CST.OAT", "microsoft.cst.oat.net")]
        //public async Task GenerateManagerSpecific(string packageUrl, string expectedToFind)
        //{
        //    var gen = new Generative();
        //    var res = gen.Mutate(new PackageURL(packageUrl));
        //    Assert.IsTrue(res.ContainsKey(expectedToFind));
        //}

        //[DataTestMethod]
        //[DataRow("pkg:npm/foo", "unicode homoglyph")]
        //[DataRow("pkg:nuget/Microsoft.CST.OAT", "unicode homoglyph")]
        //public async Task DontGenerateManagerSpecific(string packageUrl, string notExpectedToFind)
        //{
        //    var gen = new Generative();
        //    var res = gen.Mutate(new PackageURL(packageUrl));
        //    Assert.IsFalse(res.Values.Any(x => x.Any(x => x == notExpectedToFind)));
        //}
    }
}
