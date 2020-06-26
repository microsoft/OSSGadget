using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoGatherer
{
    public class CodeSnippet
    {
        public int version { get; }
        public string name { get; }
        public string sourceUrl { get; }
        public string packageName { get; }
        public CodeLanguage language { get; }
        public CryptoAlgorithm[] algorithms { get; }
        public bool fullFile { get; }

        public CodeSnippet(int version, string name, string sourceUrl, string packageName, CodeLanguage language, CryptoAlgorithm[] algorithms, bool fullFile)
        {
            this.version = version;
            this.name = name;
            this.sourceUrl = sourceUrl;
            this.packageName = packageName;
            this.language = language;
            this.algorithms = algorithms;
            this.fullFile = fullFile;
        }
    }
}
