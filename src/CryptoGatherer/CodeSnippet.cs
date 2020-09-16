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
        public const int KnownVersion = 1;
        public int version { get; }
        public string name { get; }
        public string sourceUrl { get; }
        public string packageName { get; }
        public CodeLanguage language { get; }
        public CryptoAlgorithm[] algorithms { get; }
        public bool isFullFile { get; }

        public string content { get; }

        public CodeSnippet(int version, string name, string sourceUrl, string packageName, CodeLanguage language, CryptoAlgorithm[] algorithms, bool fullFile, string content)
        {
            this.version = version;
            this.name = name;
            this.sourceUrl = sourceUrl;
            this.packageName = packageName;
            this.language = language;
            this.algorithms = algorithms;
            this.isFullFile = fullFile;
            this.content = content;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.AppendLine($"version={version}");
            sb.AppendLine(name);
            sb.AppendLine(sourceUrl);
            sb.AppendLine(packageName);
            sb.AppendLine(language.ToString());
            sb.AppendLine(string.Join(",", algorithms));
            sb.AppendLine(isFullFile ? "checked=true" : "checked=false");
            sb.AppendLine("--");
            sb.AppendLine(content);

            return sb.ToString();
        }

        public static CodeSnippet FromString(string serialized)
        {
            
            var lines = serialized.Split(new char[] { '\n' });
            if (!lines[0].Trim().Equals($"version={KnownVersion}"))
            {
                return null;
            }
            try
            {
                var obj = new CodeSnippet(
                    KnownVersion,
                    lines[1].Trim(),
                    lines[2].Trim(),
                    lines[3].Trim(),
                    (CodeLanguage)Enum.Parse(typeof(CodeLanguage),lines[4].Trim()),
                    lines[5].Trim().Split(new[] { ',' }).Where(x => !string.IsNullOrEmpty(x)).Select(x => (CryptoAlgorithm)Enum.Parse(typeof(CryptoAlgorithm), x)).ToArray(),
                    lines[6].Trim().Contains("checked=true"),
                    string.Join("\n", lines.Skip(8)));
                return obj;
            }
            catch(Exception e)
            {
                Console.WriteLine(string.Format("{0} ({1})", e.Message, e.GetType()));
                return null;
            }
        }
    }
}
