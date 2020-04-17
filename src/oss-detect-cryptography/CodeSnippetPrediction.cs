namespace Microsoft.CST.OpenSource.ML
{
    public class CodeSnippetPrediction
    {
        // ColumnName attribute is used to change the column name from
        // its default value, which is the name of the field.
        public bool Prediction { get; set; }

        // No need to specify ColumnName attribute, because the field
        // name "Probability" is the column name we want.
        public float Probability { get; set; }

        public float Score { get; set; }
    }
}
