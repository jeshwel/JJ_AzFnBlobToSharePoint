using Azure.Storage.Blobs;
using JJ_AzFnBlobToSharePoint.Core.Contracts;
using JJ_AzFnBlobToSharePoint.Core.DataAccess;
using JJ_AzFnBlobToSharePoint.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace JJ_AzFnBlobToSharePoint.Core
{
    public class AzureBlobManager : IAzureBlobManager
    {
        private readonly DEPActivityRepository depActivityRepository;
        private readonly ILogger logger;

        public AzureBlobManager(ILogger Logger)
        {
            logger = Logger;
            depActivityRepository = new DEPActivityRepository();
        }

        public async Task<string> DownloadBlob(BlobContainerClient container, string BlobName, string TempDirectoryPath)
        {
            try
            {
                // Get a reference to a blob
                BlobClient blob = container.GetBlobClient(BlobName);
                var fileName = Path.GetFileName(BlobName);
                string tempFileName = string.Format("{0}_{1}", Guid.NewGuid().ToString("N"), fileName);
                var blobTempFilePath = Path.Combine(TempDirectoryPath, tempFileName);
                //Add secs based on blob size //else email wil get triggered
                depActivityRepository.UpdateActivity(DateTime.UtcNow.AddSeconds(30), ActivityStage.SharePointCopy);
                // Download file to the given path from Azure storage.
                await blob.DownloadToAsync(blobTempFilePath);
                logger.LogInformation($"Blob download to temp {blobTempFilePath} completed.");
                return blobTempFilePath;
            }
            catch (Exception ex)
            {
                throw new Exception($"{BlobName} blob download failed " + ex.Message);
            }

        }

    }
}
