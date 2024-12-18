using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using JJ_AzFnBlobToSharePoint.Core.Contracts;
using JJ_AzFnBlobToSharePoint.Core.DataAccess;
using JJ_AzFnBlobToSharePoint.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JJ_AzFnBlobToSharePoint.Core
{
    public class BlobArchiveManager : IBlobArchiveManager
    {
        private readonly ILogger logger;
        private IDEPActivityStageManager activityStageManager;
        public List<BlobQEntity> ErrorBlobQEntities { get; private set; }
        public bool HasErrors { get; private set; }
        public BlobArchiveManager(ILogger Logger)
        {
            logger = Logger;
        }

        public async Task ArchiveEntity(List<BlobQEntity> qEntityList)
        {
            /**********************************************************************************
             * Note: When Archiving a Blob like "parent-1/subFolder-1/subFile10.txt", the parent & subfolder is created in the
             * Archive container but when deleting from source it will delete only the blob and "parent-1/subFolder-1" folders 
             * stays in source, so we have to delete them later.
             **********************************************************************************/
            //Note: JsonNode Property names are case sensitive.
            //var QMessage = @"[{""IsBlob"":true,""EntityFullName"":""rt-101/case-sub-10/subFile10.txt"",""ContainerName"":""dep-root""}]";
            //var qEntityList = JsonSerializer.Deserialize<List<BlobQEntity>>(QMessage);
            //As per the current design, 1 queue message, will only have folder/blobs belonging to 1 container.
            var sourceConnString = Environment.GetEnvironmentVariable("CN_SourceStorageConnString");
            var ArchiveContainerName = Environment.GetEnvironmentVariable("CN_DEPArchiveContainerName");
            DEPActivityRepository depActivityRepository = new DEPActivityRepository();
            IQueueManager queueManager = new QueueManager(logger);
            INotificationManager notificationManager = new NotificationManager(logger);
            activityStageManager = new DEPActivityStageManager(logger, depActivityRepository, queueManager, notificationManager);
            ErrorBlobQEntities = new List<BlobQEntity>();
            foreach (var blobEntity in qEntityList)
            {

                try
                {
                    if (blobEntity.IsBlob)
                    {
                        string sourceContainerName = blobEntity.ContainerName;
                        string blobName = blobEntity.EntityFullName;
                        var sourceBlobClient = new BlobClient(sourceConnString, sourceContainerName, blobName);

                        // Generate SAS Token for reading the SOURCE Blob with a 2 hour expiration
                        var sourceBlobSasToken = sourceBlobClient.GenerateSasUri(BlobSasPermissions.Read, DateTime.UtcNow.AddHours(2));

                        // Create DESTINATION Blob Client
                        var destblobName = $"{sourceContainerName}/{blobName}";
                        var destBlobClient = new BlobClient(sourceConnString, ArchiveContainerName, destblobName);

                        // Initiate Blob Copy from SOURCE to DESTINATION
                        await destBlobClient.StartCopyFromUriAsync(sourceBlobSasToken);

                        var destProps = destBlobClient.GetProperties().Value;
                        while (destProps.BlobCopyStatus == CopyStatus.Pending)
                        {
                            // Log copy operation status
                            logger.LogInformation($"Copy operation status: {destProps.CopyProgress}");
                            // pause for 30 seconds before checking again
                            await Task.Delay(TimeSpan.FromSeconds(15));
                            // Check copy properties again for updated status
                            destProps = destBlobClient.GetProperties().Value;
                        }

                        // Throw exception if a failure occurred
                        if (destProps.BlobCopyStatus == CopyStatus.Failed)
                            throw new Exception($"Copy operation failed: {destProps.CopyStatusDescription}");

                        // check that copy operation was successful
                        if (destProps.BlobCopyStatus == CopyStatus.Success)
                            // Delete the SOURCE blob
                            sourceBlobClient.DeleteIfExists();
                    }
                    else //Parent folder or Subfolder entity
                    {
                        try
                        {
                            //NonEmptyFolderDelete_ShouldThrowException
                            //Status: 409 (This operation is not permitted on a non-empty directory.)
                            //ErrorCode: DirectoryIsNotEmpty
                            //Delete folders or subfolders in source e.g. parent-1/subFolder-1, at this point the blobs in these folders would be already archived.
                            string sourceContainerName = blobEntity.ContainerName;
                            string blobName = blobEntity.EntityFullName;
                            var sourceBlobClient = new BlobClient(sourceConnString, sourceContainerName, blobName);
                            sourceBlobClient.DeleteIfExists();
                        }
                        catch (Azure.RequestFailedException ex)
                        {
                            if (ex.ErrorCode.Equals("DirectoryIsNotEmpty"))
                            {
                                //Retry 3 times, in some cases operator might have removed those files from source due to 
                                //previous run error.
                                if (blobEntity.MessageRetryCount <= 3)
                                {
                                    blobEntity.MessageRetryCount++;
                                    ErrorBlobQEntities.Add(blobEntity);
                                }
                            }
                            else
                                throw ex;
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!HasErrors) //Check needed since this is inside a for loop iteration.
                        HasErrors = true;

                    var stage = blobEntity.IsBlob ? ActivityStage.ArchiveFilesOnly : ActivityStage.ArchiveSourceFolders;
                    //Here we add errors to monitor record in table and continue processing the next blobEntity item. The AzFunc FnMonitorDEPActivityStages
                    //will notify the client of the errors through email. (The error suppression logic is used here, since all blobs in list might not error out.)
                    ActivityErrorLog activityErrorLog = new ActivityErrorLog { Container = blobEntity.ContainerName, FileName = blobEntity.EntityFullName, ErrorMessage = ex.Message, ActivityStage = stage };
                    activityStageManager.AddErrorsToMonitorLogRecord(activityErrorLog);
                    logger.LogError($"ActivityStage: {stage}, Error: {ex.Message}");
                }
            }
        }
    }
}
