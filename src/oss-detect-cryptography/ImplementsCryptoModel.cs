using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ML;
using Newtonsoft.Json;
using static Microsoft.ML.DataOperationsCatalog;

namespace Microsoft.CST.OpenSource.ML
{
    public class ImplementsCryptoModel
    {
        private MLContext mlContext;
        private PredictionEngine<CodeSnippet, CodeSnippetPrediction> predEngine;
        private ITransformer trainedModel;
        private DataViewSchema modelSchema;
        private IDataView? trainingData;
        private IDataView? testData;

        // Arbitrary but reproducible.
        private const int SEED = 8675309;

        public ImplementsCryptoModel(IEnumerable<CodeSnippet> snippets)
        {
            // Create MLContext
            mlContext = new MLContext(seed: SEED);

            //Load Data
            IDataView data = mlContext.Data.LoadFromEnumerable(snippets);

            TrainTestData trainTestSplit = mlContext.Data.TrainTestSplit(data, testFraction: 0.2);
            trainingData = trainTestSplit.TrainSet;
            testData = trainTestSplit.TestSet;

            modelSchema = trainingData.Schema;

            // STEP 2: Common data process configuration with pipeline data transformations          
            var dataProcessPipeline = mlContext.Transforms.Text.FeaturizeText(outputColumnName: "Features", inputColumnName: nameof(CodeSnippet.Content));

            // STEP 3: Set the training algorithm, then create and config the modelBuilder                            
            var trainer = mlContext.BinaryClassification.Trainers.SdcaLogisticRegression(labelColumnName: "ImplementsCrypto", featureColumnName: "Features");
            var trainingPipeline = dataProcessPipeline.Append(trainer);

            // STEP 4: Train the model fitting to the DataSet
            trainedModel = trainingPipeline.Fit(trainingData);
            predEngine = mlContext.Model.CreatePredictionEngine<CodeSnippet, CodeSnippetPrediction>(trainedModel);
        }

        public ImplementsCryptoModel(MLContext mlContext, ITransformer trainedModel, DataViewSchema modelSchema)
        {
            this.mlContext = mlContext;
            this.trainedModel = trainedModel;
            this.modelSchema = modelSchema;
            predEngine = mlContext.Model.CreatePredictionEngine<CodeSnippet, CodeSnippetPrediction>(trainedModel);
        }

        public static ImplementsCryptoModel CreateModelFromDirectory(string Path)
        {

            var Snippets = new ConcurrentBag<CodeSnippet>();
            Parallel.ForEach(Directory.EnumerateFiles(Path, "crypto-patterns",
                new EnumerationOptions() { RecurseSubdirectories = true }), (file) =>
                 {
                     var Snippet = CodeSnippet.FromString(File.ReadAllText(file));
                     if (Snippet != null)
                     {
                         Snippets.Add(Snippet);
                     }
                 });
            return new ImplementsCryptoModel(Snippets);
        }

        public static ImplementsCryptoModel? LoadImplementsCryptoModelFromFile(string Path)
        {
            var mlContext = new MLContext(seed: SEED);
            //Define DataViewSchema for data preparation pipeline and trained model
            DataViewSchema modelSchema;

            // Load trained model
            ITransformer trainedModel = mlContext.Model.Load(Path, out modelSchema);

            return new ImplementsCryptoModel(mlContext, trainedModel, modelSchema);
        }

        public CodeSnippetPrediction Predict(CodeSnippet code)
        {
            return predEngine.Predict(code);
        }

        public void SaveModel(string Path)
        {
            mlContext.Model.Save(trainedModel, modelSchema , Path);
        }
    }
}
