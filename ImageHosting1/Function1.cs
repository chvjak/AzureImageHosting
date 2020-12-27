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
using System.Net.Http;

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
                    switch(req.QueryString.Value)
                    {
                        case "": 
                            {
                                var content = "<html><body><form method='POST' target='http://localhost:7071/api/UploadBlobHttpTriggerFunc' enctype='multipart/form-data'><input type='text' name='t1' id='t1' value='123'/><input type='file' name='f1' id='f1'/><input type='submit'/></form></body></html>";
                                var cr = new ContentResult()
                                {
                                    Content = content,
                                    ContentType = "text/html",
                                };

                                return cr;
                            }
                        case "?image": 
                            {
                                // list all images?
                                break; 
                            }
                        case "?image=8c54c2e1-a160-4dff-9f7f-d9d4fd959c6d": 
                            {
                                CloudStorageAccount storageAccount1 = GetCloudStorageAccount(log, context);
                                CloudBlobClient blobClient1 = storageAccount1.CreateCloudBlobClient();
                                CloudBlobContainer container1 = blobClient1.GetContainerReference("dummy-messages");

                                string randomStr1 = "8c54c2e1-a160-4dff-9f7f-d9d4fd959c6d";
                                CloudBlockBlob blob1 = container1.GetBlockBlobReference(randomStr1);
                                await blob1.FetchAttributesAsync();
                                long blob_size = blob1.Properties.Length;
                                byte[] image_bytes2 = new byte[blob_size];
                                await blob1.DownloadToByteArrayAsync(image_bytes2, 0);

                                var fr = new FileContentResult(image_bytes2, "image/jpeg");

                                return fr;
                            }

                    }
            }

            log.LogInformation($"C# Http trigger function executed at: {DateTime.Now}");
            CreateContainerIfNotExists(log, context);

            CloudStorageAccount storageAccount = GetCloudStorageAccount(log, context);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("dummy-messages");

            string randomStr = Guid.NewGuid().ToString();
            CloudBlockBlob blob = container.GetBlockBlobReference(randomStr);

            var f = req.Form.Files[0];
            byte[] img_bytes1 = await GetByteArrayFromImageAsync(f);

            await blob.UploadFromByteArrayAsync(img_bytes1, 0, img_bytes1.Length);
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


        private static async Task<byte[]> GetByteArrayFromImageAsync(IFormFile file)
        {
            using (var target = new MemoryStream())
            {
                await file.CopyToAsync(target);
                return target.ToArray();
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