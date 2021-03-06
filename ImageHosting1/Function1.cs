using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Security.Claims;
using System.Linq;
using System.Collections.Generic;

namespace AzFunctions
{
    public static class UploadBlobHttpTriggerFunc
    {
        private static readonly string CONTAINER_NAME = "images";
        private static readonly object base_url = "/api/image/";

        [FunctionName("UploadBlobHttpTriggerFunc")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "image/{image_id?}")] HttpRequest req,
             string image_id,
             ClaimsPrincipal principal,
            ILogger log, ExecutionContext context)
        {
            try
            {
                if (req.Method == "GET")
                {
                    if (image_id is null)
                    {
                        string htmlFilePath = Path.Combine(context.FunctionAppDirectory, "1.html");
                        var content1 = File.ReadAllText(htmlFilePath);

                        var cr1 = new ContentResult()
                        {
                            Content = content1,
                            ContentType = "text/html",
                        };

                        return cr1;
                    }
                    else if (image_id == "all")
                    {
                        // query image ids, generate html with <img src=[im_id]> for each
                        List<string> result =(await GetImageIdsAsync(log, context)).Select(x => base_url + x.Split("/").Last()).Select(x => $"<img src='{x}'>").ToList();


                        var cr2 = new ContentResult()
                        {
                            Content = $"<html><head><style>body{{background-color: #3c424b; color: white;}}</style></head><body><center>{string.Join("<br><br>", result)}</center></body></html>",
                            ContentType = "text/html",
                        };

                        return cr2;
                    }
                    else
                    {
                        byte[] image_bytes2 = await LoadImageAsync(image_id, log, context);

                        var fr = new FileContentResult(image_bytes2, "image/jpeg"); // TODO: preserve mime type

                        return fr;
                    }
                }

                string new_image_id = await SaveImage(req, log, context);

                string content = $"{base_url}{new_image_id}";
                var cr = new ContentResult()
                {
                    Content = content,
                    ContentType = "text/plain",
                };

                return cr;

            }
            catch (Exception e)
            {
                log.LogError(e.Message);
                return new OkObjectResult(e.Message);
            }

        }

        private static async Task<string> SaveImage(HttpRequest req, ILogger log, ExecutionContext context)
        {
            log.LogInformation($"C# Http trigger function executed at: {DateTime.Now}");
            CreateContainerIfNotExists(log, context);

            CloudStorageAccount storageAccount = GetCloudStorageAccount(log, context);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(CONTAINER_NAME);

            string new_image_id = CreateImageId();
            CloudBlockBlob blob = container.GetBlockBlobReference(new_image_id);

            var f = req.Form.Files[0];
            // TODO: validate file size
            // TODO: validate mime type
            byte[] img_bytes1 = await GetByteArrayFromImageAsync(f);

            await blob.UploadFromByteArrayAsync(img_bytes1, 0, img_bytes1.Length);
            await blob.SetPropertiesAsync();

            log.LogInformation($"Bolb {new_image_id} is uploaded to container {container.Name}");
            return new_image_id;
        }

        private static async Task<IEnumerable<string>> GetImageIdsAsync(ILogger log, ExecutionContext context)
        {
            CloudStorageAccount storageAccount1 = GetCloudStorageAccount(log, context);
            CloudBlobClient blobClient1 = storageAccount1.CreateCloudBlobClient();
            CloudBlobContainer container1 = blobClient1.GetContainerReference(CONTAINER_NAME);

            BlobContinuationToken continuationToken = null;
            List<IListBlobItem> results = new List<IListBlobItem>();
            do
            {
                var response = await container1.ListBlobsSegmentedAsync(continuationToken);
                continuationToken = response.ContinuationToken;
                results.AddRange(response.Results);
            }
            while (continuationToken != null);

            return results.Select(x => x.Uri.ToString());
        }

        private static async Task<byte[]> LoadImageAsync(string image_id, ILogger log, ExecutionContext context)
        {
            CloudStorageAccount storageAccount1 = GetCloudStorageAccount(log, context);
            CloudBlobClient blobClient1 = storageAccount1.CreateCloudBlobClient();
            CloudBlobContainer container1 = blobClient1.GetContainerReference(CONTAINER_NAME);
            CloudBlockBlob blob1 = container1.GetBlockBlobReference(image_id);
            await blob1.FetchAttributesAsync();
            long blob_size = blob1.Properties.Length;
            byte[] image_bytes2 = new byte[blob_size];
            await blob1.DownloadToByteArrayAsync(image_bytes2, 0);
            return image_bytes2;
        }

        private static string CreateImageId()
        {
            Random rnd = new Random();
            string result = "";
            // char[] az = Enumerable.Range('a', 'z' - 'a' + 1).Select(i => (Char)i).ToArray();
            // https://stackoverflow.com/questions/314466/generating-an-array-of-letters-in-the-alphabet
            string values =  "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
            for (int i = 0; i < 7; i++)
            {
                result += values[rnd.Next(0, 62)];
            }
            return result;
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
            string[] containers = new string[] { CONTAINER_NAME };
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
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(config["CloudStorageAccount"]); // TODO: integrate KV
            return storageAccount;
        }
    }
}