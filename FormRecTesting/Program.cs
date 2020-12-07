using Azure;
using Azure.AI.FormRecognizer;
using Azure.AI.FormRecognizer.Models;
using Azure.AI.FormRecognizer.Training;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FormRecTesting
{
    class Program
    {
        public enum ModelType
        {
            NAVIGATION,
            DEFINITION,
        };
        private static readonly string endpointNavigation = "https://navigationrecognizer.cognitiveservices.azure.com/";
        private static readonly string apiKeyNavigation = "ca58f0fb10f7482a8c0041307c2954fa";
        private static readonly string endpointDefinition = "https://definitionrecognizer.cognitiveservices.azure.com/";
        private static readonly string apiKeyDefinition = "7ef18a522cc74f36a5ed7c45a0ac4a2f";

        //private static readonly string mongoConnStr = "mongodb://localhost:27017/?readPreference=primary&appname=MongoDB%20Compass%20Community&ssl=false";
        private static readonly string mongoConnStr = "mongodb://localhost:27017/?readPreference=primary&appname=MongoDB%20Compass%20Community&ssl=false";
        private static readonly string dbName = "AISmartLinkingDB";
        private static readonly string collectionForModels = "AISmartLinkingDBModelCollection";
        private static readonly string collectionForRecongizedForms = "AISmartLinkingDBRecongizedFormCollection";

        private static readonly string navigationID = "4d372cd5-c4eb-48b9-a362-b3c585420499";
        private static readonly string definitionID = "d84ca5b0-9203-44f3-ade6-7a3e5aaf24e6";

        public async static Task processFile(string pdfToAnalyzePath)
        {
            Program p = new Program();
            await p.useModelAndRecognizeGivenPDFForm(pdfToAnalyzePath);//.Wait();
        }

        public async Task useModelAndRecognizeGivenPDFForm(string pdfToAnalyzePath)
        {
            var navTrainingClient = AuthenticateTrainingClientForNavigationModel();
            var navModel = navTrainingClient.GetCustomModel(navigationID);
            writeModelToDB(navModel, ModelType.NAVIGATION);

            var defTrainingClient = AuthenticateTrainingClientForDefinitionModel();
            var defModel = defTrainingClient.GetCustomModel(definitionID);
            writeModelToDB(defModel, ModelType.DEFINITION);

            var navigationRecognizerClient = AuthenticateFormNavigationClient();
            await  analyzePdfForm(
                navigationRecognizerClient, navigationID, pdfToAnalyzePath, ModelType.NAVIGATION);

            var definitionRecognizerClient = AuthenticateFormDefinitionClient();
            await  analyzePdfForm(
                definitionRecognizerClient, definitionID, pdfToAnalyzePath, ModelType.DEFINITION);
        }
        // authenticate form recognizer client Navigation
        static private FormRecognizerClient AuthenticateFormNavigationClient()
        {
            var navigationCredential = new AzureKeyCredential(apiKeyNavigation);
            var navigationRecognizerClient = new FormRecognizerClient(new Uri(endpointNavigation), navigationCredential);
            return navigationRecognizerClient;
        }
        // authenticate form recognizer client Definition
        static private FormRecognizerClient AuthenticateFormDefinitionClient()
        {
            var definitionCredential = new AzureKeyCredential(apiKeyDefinition);
            var definitionRecognizerClient = new FormRecognizerClient(new Uri(endpointDefinition), definitionCredential);
            return definitionRecognizerClient;
        }
        //Authenticate form training client Navigation
        static private FormTrainingClient AuthenticateTrainingClientForNavigationModel()
        {
            var credential = new AzureKeyCredential(apiKeyNavigation);
            var trainingClient = new FormTrainingClient(new Uri(endpointNavigation), credential);
            return trainingClient;
        }
        //Authenticate form training client Definition
        static private FormTrainingClient AuthenticateTrainingClientForDefinitionModel()
        {
            var credential = new AzureKeyCredential(apiKeyDefinition);
            var trainingClient = new FormTrainingClient(new Uri(endpointDefinition), credential);
            return trainingClient;
        }
        public async Task analyzePdfForm(
            FormRecognizerClient recognizerClient,
            string modelId,
            string pdfToAnalyzeLocalPath,
            ModelType processingModelType)
        {
            using (FileStream pdfInputstream = new FileStream(pdfToAnalyzeLocalPath, FileMode.Open))
            {
                RecognizedFormCollection recognizedForms = await recognizerClient
                .StartRecognizeCustomForms(modelId, pdfInputstream)
                .WaitForCompletionAsync();
                switch(processingModelType)
                {
                    case ModelType.NAVIGATION:
                        pdfToAnalyzeLocalPath = pdfToAnalyzeLocalPath + "_Navig";
                        break;
                    case ModelType.DEFINITION:
                        pdfToAnalyzeLocalPath = pdfToAnalyzeLocalPath + "_Defin";
                        break;
                }
                writeRecognizedFormCollectionToDB(pdfToAnalyzeLocalPath, recognizedForms);
            }
        }
        public class RecognizedFormCollectionForPDFDocument
        {
            public string documentPath { get; set; }
            public RecognizedFormCollection recognizedFormCollection { get; set; }
        }
        
        public bool writeRecognizedFormCollectionToDB(
            string documentPath,
            RecognizedFormCollection recognizedForms)
        {
            RecognizedFormCollectionForPDFDocument currDocumentAndItsRecognizedForms = new RecognizedFormCollectionForPDFDocument();
            currDocumentAndItsRecognizedForms.documentPath = documentPath;
            currDocumentAndItsRecognizedForms.recognizedFormCollection = recognizedForms;
            string jsonString = JsonSerializer.Serialize(currDocumentAndItsRecognizedForms);
            var regex = new Regex(@"(Arti[cl][lc]e[ ]+[0-9]+)\.([0-9]+)");
            jsonString = regex.Replace(jsonString, "$1dot$2");

            var usRegex = new Regex(@"U\.S\.");
            jsonString = usRegex.Replace(jsonString, "$1dot$2");

            MongoClient dbClient = new MongoClient(mongoConnStr);
            var aiSmartLinkingDB = dbClient.GetDatabase(dbName);
            if (!(aiSmartLinkingDB is null))
            {
                var aiSmartLinkingDBCollection = aiSmartLinkingDB.GetCollection<BsonDocument>(
                    collectionForRecongizedForms);
                if (!(aiSmartLinkingDBCollection is null))
                {
                    BsonDocument modelBsonDocument = BsonDocument.Parse(jsonString);
                    aiSmartLinkingDBCollection.InsertOne(modelBsonDocument);
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
            return true;
        }


        public bool writeModelToDB(CustomFormModel model, ModelType modelType)
        {
            string jsonString = JsonSerializer.Serialize(model);
            var regex = new Regex(@"(Arti[cl][lc]e[ ]+[0-9]+)\.([0-9]+)");
            jsonString = regex.Replace(jsonString, "$1dot$2");
            var usRegex = new Regex(@"U\.S\.");
            jsonString = usRegex.Replace(jsonString, "$1dot$2");

            Console.WriteLine(jsonString);
            MongoClient dbClient = new MongoClient(mongoConnStr);
            var aiSmartLinkingDB = dbClient.GetDatabase(dbName);
            if (!(aiSmartLinkingDB is null))
            {
                var aiSmartLinkingDBCollection = aiSmartLinkingDB.GetCollection<BsonDocument>(
                    collectionForModels);
                var modelId = "";
                if (modelType == ModelType.NAVIGATION)
                    modelId = navigationID;
                if (modelType == ModelType.DEFINITION)
                    modelId = definitionID;
                var filter = Builders<BsonDocument>.Filter.Eq("ModelId", modelId);
                var modelDoc = aiSmartLinkingDBCollection.Find(filter).FirstOrDefault();
                if (!(aiSmartLinkingDBCollection is null) && modelDoc == null)
                {
                    BsonDocument modelBsonDocument = BsonDocument.Parse(jsonString);
                    aiSmartLinkingDBCollection.InsertOne(modelBsonDocument);
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
            return true;
        }


    }
}