using Microsoft.CodeAnalysis.Sarif;
using System;

namespace Microsoft.CST.OpenSource.Shared
{
    /// <summary>
    ///     Factory to build the outputBuilder based on the format specified
    /// </summary>
    public class OutputBuilderFactory
    {
        public enum OutputFormat
        {
            sarifv1,
            sarifv2,
            text, // no sarif, just text
            none
        };

        public static IOutputBuilder CreateDefaultOutputBuilder()
        {
            return new StringOutputBuilder();
        }

        public static IOutputBuilder CreateOutputBuilder(string format)
        {
            OutputFormat currentOutputFormat = GetOutputFormat(format);
            return CreateOutputBuilder(currentOutputFormat);
        }

        public static IOutputBuilder CreateOutputBuilder(OutputFormat format)
        {
            switch (format)
            {
                case OutputFormat.none:
                    return new NoneOutputBuilder();

                case OutputFormat.sarifv1:
                case OutputFormat.sarifv2:
                    SarifVersion version = format == OutputFormat.sarifv1 ? SarifVersion.OneZeroZero : SarifVersion.Current;
                    return new SarifOutputBuilder(version);

                case OutputFormat.text:
                default:
                    return new StringOutputBuilder();
            }
        }

        public static OutputFormat GetOutputFormat(string format)
        {
            OutputFormat currentOutputFormat = OutputFormat.text;
            if (!Enum.TryParse<OutputFormat>(format, true, out currentOutputFormat))
            {
                throw new ArgumentOutOfRangeException("Invalid output format");
            }
            return currentOutputFormat;
        }

        private OutputBuilderFactory()
        {
        }
    }
}