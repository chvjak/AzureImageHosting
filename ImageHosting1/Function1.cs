using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;

namespace AzFunctions
{
    public static class UploadBlobHttpTriggerFunc
    {
        [FunctionName("UploadBlobHttpTriggerFunc")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log, ExecutionContext context)
        {
            try { 
            if (req.Method == "GET")
            {
                var content = "<html><body><form method='POST' target='http://localhost:7071/api/UploadBlobHttpTriggerFunc'><input type='file'/><input type='submit'/></form></body></html>";
                var cr =  new ContentResult()
                {
                    Content = content,
                    ContentType = "text/html",
                };

                return cr;
            }

            log.LogInformation($"C# Http trigger function executed at: {DateTime.Now}");
            CreateContainerIfNotExists(log, context);

            CloudStorageAccount storageAccount = GetCloudStorageAccount(log, context);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("dummy-messages");

            string randomStr = Guid.NewGuid().ToString();
            CloudBlockBlob blob = container.GetBlockBlobReference(randomStr);

            byte[] buffer = new byte[req.Body.Length];
            int index = 0, count = 0;

            //blob.Properties.ContentType = req.ContentType;
            //await req.Body.ReadAsync(buffer, index, count);

            string body = await StreamToStringAsync(req);

            await blob.UploadFromByteArrayAsync(buffer, index, count);
            await blob.SetPropertiesAsync();

            log.LogInformation($"Bolb {randomStr} is uploaded to container {container.Name}");

            return new OkObjectResult("UploadBlobHttpTrigger function executed successfully!!"); // return url

        }
            catch (Exception e)
            {
                log.LogError(e.Message);
                return new OkObjectResult(e.Message); // return url
            }

}


        private static async Task<string> StreamToStringAsync(HttpRequest request)
        {
            using (var sr = new StreamReader(request.Body))
            {
                return await sr.ReadToEndAsync();
            }
        }

        private static void CreateContainerIfNotExists(ILogger logger, ExecutionContext executionContext)
        {
            CloudStorageAccount storageAccount = GetCloudStorageAccount(logger, executionContext);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            string[] containers = new string[] { "dummy-messages" };
            foreach (var item in containers)
            {
                CloudBlobContainer blobContainer = blobClient.GetContainerReference(item);
                blobContainer.CreateIfNotExistsAsync();
            }
        }

        private static CloudStorageAccount GetCloudStorageAccount(ILogger logger, ExecutionContext executionContext)
        {
            var config = new ConfigurationBuilder()
                            .SetBasePath(executionContext.FunctionAppDirectory)
                            .AddJsonFile("local.settings.json", true, true)
                            .AddEnvironmentVariables().Build();
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(config["CloudStorageAccount"]);
            return storageAccount;
        }
        private static void LoadStreamWithJson(Stream ms, object obj)
        {
            StreamWriter writer = new StreamWriter(ms);
            writer.Write(obj);
            writer.Flush();
            ms.Position = 0;
        }
    }
}