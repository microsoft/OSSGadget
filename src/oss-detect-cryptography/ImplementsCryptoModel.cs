using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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

        private const int SEED = 9238546;

        public ImplementsCryptoModel(IEnumerable<CodeSnippet> snippets)
        {
            // Create MLContext
            mlContext = new MLContext(seed: SEED);

            //Load Data
            IDataView data = mlContext.Data.LoadFromEnumerable<CodeSnippet>(snippets);

            TrainTestData trainTestSplit = mlContext.Data.TrainTestSplit(data, testFraction: 0.2);
            trainingData = trainTestSplit.TrainSet;
            testData = trainTestSplit.TestSet;

            modelSchema = trainingData.Schema;

            // STEP 2: Common data process configuration with pipeline data transformations          
            var dataProcessPipeline = mlContext.Transforms.Text.FeaturizeText(outputColumnName: "Features", inputColumnName: nameof(CodeSnippet.Code));

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

        public static ImplementsCryptoModel? CreateImplementsCryptoModelFromJson(string Path)
        {
            using StreamReader file = System.IO.File.OpenText(Path);
            var snippets = JsonConvert.DeserializeObject<List<CodeSnippet>>(file.ReadToEnd());
            if (snippets != null)
            {
                return new ImplementsCryptoModel(snippets);
            }
            return null;
        }

        public static ImplementsCryptoModel? LoadImplementsCryptoModelFromPath(string Path)
        {
            var mlContext = new MLContext(seed: SEED);
            //Define DataViewSchema for data preparation pipeline and trained model
            DataViewSchema modelSchema;

            // Load trained model
            ITransformer trainedModel = mlContext.Model.Load("model.zip", out modelSchema);

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
