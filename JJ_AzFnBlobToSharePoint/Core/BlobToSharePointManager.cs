using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using JJ_AzFnBlobToSharePoint.Core.Contracts;
using JJ_AzFnBlobToSharePoint.Core.DataAccess;
using JJ_AzFnBlobToSharePoint.Core.Exceptions;
using JJ_AzFnBlobToSharePoint.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace JJ_AzFnBlobToSharePoint.Core
{
    public class BlobToSharePointManager : IBlobToSharePointManager
    {
        private ILogger logger;
        private ISharePointManager sharePointManager;
        private IAzureBlobManager azureBlobManager;
        private IQueueManager queueManager;
        private IDEPActivityStageManager activityStageManager;
        private string sourceStorageConn;
        private string tempDirectoryPath;
        private string archiveFileOnlyWaitQueue;
        private string archiveFolderOnlyWaitQueue;
        private string fileProcessorQueueStorageConnection;
        private BlobContainerClient container;
        const string PathSeparator = "/";
        public string ContainerName { get; private set; }
        DEPActivityRepository depActivityRepository;

        public BlobToSharePointManager(ILogger Logger, ISharePointManager SharePointManager, IAzureBlobManager AzureBlobManager, IQueueManager QueueManager)
        {
            Init(Logger, SharePointManager, AzureBlobManager, QueueManager, null);
        }
        public BlobToSharePointManager(ILogger Logger, ISharePointManager SharePointManager, IAzureBlobManager AzureBlobManager, IQueueManager QueueManager, string ContainerName)
        {
            Init(Logger, SharePointManager, AzureBlobManager, QueueManager, ContainerName);
        }

        public void InitContainerInstance(string ContainerName)
        {
            BlobServiceClient client = new BlobServiceClient(sourceStorageConn);
            container = client.GetBlobContainerClient(ContainerName);
            this.ContainerName = ContainerName;
        }

        public async Task CopyBlobsToSharePoint(List<BlobQEntity> BlobQMessage)
        {
            //Important!! DO NOT CHANGE this OrderByDescending code.
            BlobQMessage = BlobQMessage.OrderByDescending(item => !item.IsBlob)
                                        .ThenBy(item => item.EntityFullName)
                                        .ToList();
            List<BlobQEntity> archiveFilesOnlyQEntity = new List<BlobQEntity>();
            List<BlobQEntity> archiveFoldersQEntity = new List<BlobQEntity>();
            CleanUpDownloadTempDirectory();
            foreach (var blobEntity in BlobQMessage)
            {
                try
                {
                    //Important!! Note: As per the current design & logic (from creation of message in queue (FnPrepItemsForFileProcessorQueue) + processing queue message) directories will be created first,
                    //so when we process a blob entity we need NOT check again the directory exists or not.
                    var folderPath = blobEntity.EntityFullName;
                    if (blobEntity.IsBlob)
                    {
                        folderPath = Path.GetDirectoryName(blobEntity.EntityFullName);
                        //First download blob then upload to SharePoint
                        var blobTempPath = await azureBlobManager.DownloadBlob(container, blobEntity.EntityFullName, tempDirectoryPath);
                        Microsoft.SharePoint.Client.File result = sharePointManager.UploadBlobFile(folderPath, blobTempPath);
                        if (result != null)
                        {
                            //Mark "Processed" file in temp to be picked up by cleanup function.
                            var newFileName = $"Processed_{Path.GetFileName(blobTempPath)}";
                            var processedFileFullPath = Path.Combine(Path.GetDirectoryName(blobTempPath), newFileName);
                            File.Move(blobTempPath, processedFileFullPath);
                            archiveFilesOnlyQEntity.Add(blobEntity);
                        }
                    }
                    else
                    {
                        //Check if folder exists in SP
                        sharePointManager.CreateFolderIfNotExists(folderPath);
                        archiveFoldersQEntity.Add(blobEntity);
                    }

                }
                catch (Exception ex)
                {
                    //Here we add errors to monitor record in table and continue processing the next blobEntity item. The AzFunc FnMonitorDEPActivityStages
                    //will notify the client of the errors through email. (The error suppression logic is used here, since all blobs in list might not error out.)
                    ActivityErrorLog activityErrorLog = new ActivityErrorLog { Container = blobEntity.ContainerName, FileName = blobEntity.EntityFullName, ErrorMessage = ex.Message, ActivityStage = ActivityStage.SharePointCopy };
                    activityStageManager.AddErrorsToMonitorLogRecord(activityErrorLog);
                    logger.LogError(ex.Message);
                }
            }

            if (archiveFilesOnlyQEntity.Count > 0)
            {
                activityStageManager.AddFilesCopiedToMonitorLogRecord(archiveFilesOnlyQEntity);
                queueManager.AddMessageToQueue(archiveFilesOnlyQEntity, fileProcessorQueueStorageConnection, archiveFileOnlyWaitQueue);
            }

            if (archiveFoldersQEntity.Count > 0)
                queueManager.AddMessageToQueue(archiveFoldersQEntity, fileProcessorQueueStorageConnection, archiveFolderOnlyWaitQueue);

        }

        public Pageable<BlobHierarchyItem> GetOneLevelHierarchyBlobs(string prefix)
        {
            if (!string.IsNullOrEmpty(prefix) && !prefix.EndsWith(PathSeparator))
                prefix = prefix + PathSeparator;
            return container.GetBlobsByHierarchy(prefix: prefix, delimiter: PathSeparator);
        }


        public async Task<string> DownloadBlob(string BlobName)
        {
            try
            {
                // Get a reference to a blob
                BlobClient blob = container.GetBlobClient(BlobName);
                var fileName = Path.GetFileName(BlobName);
                string tempFileName = string.Format("{0}_{1}", Guid.NewGuid().ToString("N"), fileName);
                var blobTempFilePath = Path.Combine(tempDirectoryPath, tempFileName);
                //Add secs based on blob size else email might get triggered
                activityStageManager.UpdateActivity(DateTime.UtcNow.AddSeconds(30), ActivityStage.SharePointCopy);
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

        public void CleanUpDownloadTempDirectory(bool AllFiles = false)
        {
            var timeout = TimeSpan.FromMinutes(2);
            try
            {
                var cleanUpRegEx = AllFiles ? "*.*" : "Processed_*.*";
                var dir = new DirectoryInfo(tempDirectoryPath);
                var files = dir.GetFiles(cleanUpRegEx)
                    .Where(file => DateTime.UtcNow - file.LastWriteTimeUtc > timeout);
                logger.LogInformation($"Number of files to be deleted from DownloadTempDirectory: {files.Count()}");
                foreach (FileInfo file in files)
                {
                    try
                    {
                        file.Delete();
                    }
                    //Suppress any file cleanup errors but Log them.
                    catch (Exception ex) { logger.LogError(ex.Message); }
                }
            }
            //Suppress any file cleanup errors but Log them.
            catch (Exception ex) { logger.LogError(ex.Message); }
        }

        private void Init(ILogger Logger, ISharePointManager SharePointManager, IAzureBlobManager AzureBlobManager, IQueueManager QueueManager, string ContainerName)
        {
            logger = Logger;
            sourceStorageConn = Environment.GetEnvironmentVariable("CN_SourceStorageConnString");
            fileProcessorQueueStorageConnection = Environment.GetEnvironmentVariable("CN_DEPFileProcessorQueueStorageConnection");
            archiveFileOnlyWaitQueue = Environment.GetEnvironmentVariable("CN_DEPArchiveFileOnlyWaitQueue");
            archiveFolderOnlyWaitQueue = Environment.GetEnvironmentVariable("CN_DEPArchiveFolderOnlyWaitQueue");
            var local_root = Environment.GetEnvironmentVariable("AzureWebJobsScriptRoot");
            var azure_root = $"{Environment.GetEnvironmentVariable("HOME")}\\site\\wwwroot";
            var actual_root = local_root ?? azure_root;
            tempDirectoryPath = Path.Combine(actual_root, "DownloadTemp");
            logger.LogInformation($"TempDirectoryPath {tempDirectoryPath}");
            CreateDownloadTempDirectory();
            depActivityRepository = new DEPActivityRepository();
            INotificationManager notificationManager = new NotificationManager(Logger);
            activityStageManager = new DEPActivityStageManager(Logger, depActivityRepository, QueueManager, notificationManager);
            sharePointManager = SharePointManager;
            azureBlobManager = AzureBlobManager;
            queueManager = QueueManager;

            if (!string.IsNullOrWhiteSpace(ContainerName))
                InitContainerInstance(ContainerName);

        }

        private void CreateDownloadTempDirectory()
        {
            try
            {
                if (!Directory.Exists(tempDirectoryPath))
                    Directory.CreateDirectory(tempDirectoryPath);
            }
            catch (Exception ex)
            {
                throw new Exception("Create Temp Directory Failed" + ex.Message);
            }
        }
    }
}
