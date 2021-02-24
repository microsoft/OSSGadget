using Microsoft.CodeAnalysis.Sarif;
using System;

namespace Microsoft.CST.OpenSource.Shared
{
    /// <summary>
    ///     Factory to build the outputBuilder based on the format specified
    /// </summary>
    public class OutputBuilderFactory
    {
        /// <summary>
        /// The output formats you can pass
        /// </summary>
        public enum OutputFormat
        {
            sarifv1,
            sarifv2,
            text // no sarif, just text
        };

        /// <summary>
        /// Create a StringOutputBuilder
        /// </summary>
        /// <returns></returns>
        public static IOutputBuilder CreateDefaultOutputBuilder()
        {
            return new StringOutputBuilder();
        }

        /// <summary>
        /// Create an output builder based on the given format
        /// </summary>
        /// <param name="format"></param>
        /// <returns></returns>
        public static IOutputBuilder CreateOutputBuilder(string format)
        {
            OutputFormat currentOutputFormat = GetOutputFormat(format);
            return CreateOutputBuilder(currentOutputFormat);
        }

        /// <summary>
        /// Create an output builder based on the given format
        /// </summary>
        /// <param name="format"></param>
        /// <returns></returns>
        public static IOutputBuilder CreateOutputBuilder(OutputFormat format)
        {
            switch (format)
            {
                case OutputFormat.text:
                default:
                    return new StringOutputBuilder();

                case OutputFormat.sarifv1:
                case OutputFormat.sarifv2:
                    SarifVersion version = format == OutputFormat.sarifv1 ? SarifVersion.OneZeroZero : SarifVersion.Current;
                    return new SarifOutputBuilder(version);
            }
        }

        /// <summary>
        /// Convert a string to an OutputFormat
        /// </summary>
        /// <param name="format"></param>
        /// <returns></returns>
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