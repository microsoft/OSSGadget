// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.CST.OpenSource.Reproducibility;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.Tests
{
    using OssGadget.Options;

    [TestClass]
    public class ReproducibleTest
    {
        public ReproducibleTest()
        {
        }

        [DataTestMethod]
        [DataRow("pkg:npm/left-pad@1.3.0", true)]
        [DataRow("pkg:npm/non-existent1267461827467@12421", false)]
        public async Task CheckReproducibility(string packageUrl, bool? expectedToBeReproducible)
        {
            string? outputFilename = Guid.NewGuid().ToString() + ".json";
            var options = new ReproducibleToolOptions()
            {
                AllStrategies = true, OutputFile = outputFilename, Targets = new[] { packageUrl }
            };
            await new ReproducibleTool().RunAsync(options);

            bool outputFileExists = File.Exists(outputFilename);

            if (expectedToBeReproducible != null)
            {
                Assert.IsTrue(outputFileExists, "Output file does not exist.");
                string? result = File.ReadAllText(outputFilename);

                List<ReproducibleToolResult>? jsonResults = JsonSerializer.Deserialize<List<ReproducibleToolResult>>(result);
                Assert.IsNotNull(jsonResults, "Output file was not parseable.");

                Assert.AreEqual(expectedToBeReproducible == true, jsonResults.First().IsReproducible);
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


        [DataTestMethod]
        [DataRow("/foo/bar/quux.c", "quux.c", "quux.c")]
        [DataRow("/foo/bar/quux.c", "baz.c", null)]
        [DataRow("/foo/bar/quux.c", "baz/quux.c,bar/quux.c", "bar/quux.c")]
        public async Task CheckGetClosestMatch(string filename, string targets, string expectedTarget)
        {
            IEnumerable<string>? results = OssReproducibleHelpers.GetClosestFileMatch(filename, targets.Split(','));
            Assert.IsNotNull(results);

            if (expectedTarget == null)
            {
                Assert.IsFalse(results.Any());
            }
            else
            {
                Assert.IsTrue(results.Any());
                Assert.AreEqual(expectedTarget, results.First());
            }
        }
    }

}
