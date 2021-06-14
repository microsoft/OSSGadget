using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.CST.OpenSource;
using System.Threading.Tasks;
using System.IO;
using System;
using System.Text.Json;

namespace Microsoft.CST.OpenSource.Tests
{
    [TestClass]
    public class ReproducibleTest
    {
        public ReproducibleTest()
        {
        }

        [DataTestMethod]
        [DataRow("pkg:npm/left-pad@1.3.0", true)]
        [DataRow("pkg:npm/non-existent1267461827467@12421", null)]
        [DataRow("pkg:error/error", null)]
        public async Task CheckReproducibility(string packageUrl, bool? expectedToBeReproducible)
        {
            var outputFilename = Guid.NewGuid().ToString() + ".json";
            ReproducibleTool.Main(new[] { "-a", "-o", outputFilename, packageUrl }).Wait();
            
            bool outputFileExists = File.Exists(outputFilename);

            if (expectedToBeReproducible != null)
            {
                Assert.IsTrue(outputFileExists, "Output file does not exist.");
                var result = File.ReadAllText(outputFilename);

                var jsonResult = JsonSerializer.Deserialize<ReproducibleToolResult>(result);
                Assert.IsNotNull(jsonResult, "Output file was not parseable.");

                Assert.AreEqual(expectedToBeReproducible == true, jsonResult.IsReproducible);
            }
            else
            {
                if (outputFileExists)
                {
                    File.Delete(outputFilename);
                }
                Assert.IsTrue(!outputFileExists, "File was produced but should have been an error.");
            }

            // Cleanup
            if (File.Exists(outputFilename))
            {
                File.Delete(outputFilename);
            }
        }
    }
}
