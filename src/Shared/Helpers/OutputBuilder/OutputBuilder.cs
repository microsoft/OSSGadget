using Microsoft.CodeAnalysis.Sarif;
using System;

namespace Microsoft.CST.OpenSource.Shared
{
    /// <summary>
    /// Builds the output text based on the format specified
    /// </summary>
    public class OutputBuilder : IOutputBuilder
    {
        public enum OutputFormat
        {
            sarifv1,
            sarifv2,
            text // no sarif, just text
        };

        public OutputFormat currentOutputFormat { get; private set; } = OutputFormat.text; // default = text
        IOutputBuilder currentBuilder;

        public OutputBuilder(string format)
        {
            OutputFormat currentFormat = OutputFormat.text;
            if (!Enum.TryParse<OutputFormat>(format, true, out currentFormat))
            {
                throw new ArgumentOutOfRangeException("Invalid output format");
            }

            this.currentOutputFormat = currentFormat;

            switch (this.currentOutputFormat)
            {
                case OutputFormat.text:
                default:
                    this.currentBuilder = new StringOutputBuilder();
                    break;

                case OutputFormat.sarifv1:
                case OutputFormat.sarifv2:
                    SarifVersion version = this.currentOutputFormat == OutputFormat.sarifv1 ? SarifVersion.OneZeroZero : SarifVersion.Current;
                    this.currentBuilder = new SarifOutputBuilder(version);
                    break;
            }
        }

        /// <summary>
        /// Prints to the currently selected output
        /// </summary>
        public void PrintOutput()
        {
            this.currentBuilder?.PrintOutput();
        }

        public string? GetOutput()
        {
            return this.currentBuilder?.GetOutput();
        }
        public void AppendOutput(object? output)
        {
            this.currentBuilder?.AppendOutput(output ?? new object());
        }
    }
}
