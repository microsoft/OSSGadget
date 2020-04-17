using Microsoft.DevSkim;

namespace Microsoft.CST.OpenSource.ML
{
    public class CodeSnippet
    {
        public string Code { get; set; }
        public bool ImplementsCrypto { get; set; }
        public string CodeLanguage { get; set; }

        public CodeSnippet(string Code, bool ImplementsCrypto, string CodeLanguage)
        {
            this.Code = Code;
            this.ImplementsCrypto = ImplementsCrypto;
            this.CodeLanguage = CodeLanguage;
        }
    }
}
