using JJ_AzFnBlobToSharePoint.Core.Contracts;
using JJ_AzFnBlobToSharePoint.Core.Exceptions;
using JJ_AzFnBlobToSharePoint.Core.Extensions;
using JJ_AzFnBlobToSharePoint.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace JJ_AzFnBlobToSharePoint.Core
{
    public class PrepItemsForFileProcessorQueue : IPrepItemsForFileProcessorQueue
    {
        private readonly ILogger logger;
        private readonly IQueueManager queueManager;
        private readonly IBlobToSharePointManager blobToSPManager;
        private readonly IDEPActivityStageManager depActivityStageManager;
        private string fileProcessorQueueStorageConnection;
        private string fileProcessorWaitQueueName;
        private bool anyContainerHasData;
        Queue<string> subfolderLocalMemQueue = new Queue<string>();
        public PrepItemsForFileProcessorQueue(ILogger Logger, IBlobToSharePointManager BlobToSPManager, IQueueManager QueueManager, IDEPActivityStageManager DEPActivityStageManager)
        {
            logger = Logger;
            queueManager = QueueManager;
            blobToSPManager = BlobToSPManager;
            fileProcessorQueueStorageConnection = Environment.GetEnvironmentVariable("CN_DEPFileProcessorQueueStorageConnection");
            fileProcessorWaitQueueName = Environment.GetEnvironmentVariable("CN_DEPFileProcessorWaitQueue");
            this.depActivityStageManager = DEPActivityStageManager;
        }

        public void Start()
        {
            try
            {
                //var siteURL = "https://mcgov.sharepoint.com/sites/RecordsManagement_DEV";
                var containers = Environment.GetEnvironmentVariable("CN_SourceContainers").Split('|');
                if (containers.Length > 0)
                {
                    anyContainerHasData = false;
                    logger.LogInformation($"CleanUpDownloadTempDirectory started.");
                    blobToSPManager.CleanUpDownloadTempDirectory(true);
                    logger.LogInformation($"CleanUpDownloadTempDirectory completed.");
                    foreach (var container in containers)
                    {
                        try
                        {
                            blobToSPManager.InitContainerInstance(container.Trim());
                            depActivityStageManager.UpdateActivity(DateTime.UtcNow.AddSeconds(30), ActivityStage.PrepItemsForProcessorQueue);
                            IterateContainer(blobToSPManager, string.Empty);
                        }
                        catch (Exception ex)
                        {
                            throw new ProcessException(ex.Message, container, null);
                        }
                    }

                    if (!anyContainerHasData)
                    {
                        //If none of the containers has data, reset the current ActivityStage.
                        depActivityStageManager.UpdateActivity(DateExtensions.CustomDateMinValue, string.Empty);
                        logger.LogInformation("[IterateContainer] There is no data in any of the containers.");
                    }
                }
                else
                    throw new Exception("[APP_ERROR]: Could not find container list configuration.");
            }
            catch (Exception ex)
            {
                //Note: Since this is the first stage in the flow we can clear the wait queue here.
                queueManager.ClearQueueMessages(fileProcessorQueueStorageConnection, fileProcessorWaitQueueName);
                throw ex;
            }
        }

        private void IterateContainer(IBlobToSharePointManager blobToSPManager, string prefix)
        {
            logger.LogInformation($"[IterateContainer] ContainerName: {blobToSPManager.ContainerName}, FolderPrefix: {prefix}");
            var blobsList = blobToSPManager.GetOneLevelHierarchyBlobs(prefix).ToList();
            logger.LogInformation($"GetOneLevelHierarchyBlobs completed: {JsonSerializer.Serialize(blobsList)}");
            var blobMsgList = new List<BlobQEntity>();
            foreach (var blob in blobsList)
            {
                var entityFullName = string.Empty;
                long? contentLength = null;
                if (blob.IsBlob)
                {
                    entityFullName = blob.Blob.Name;
                    contentLength = blob.Blob.Properties.ContentLength;
                }
                else
                {
                    entityFullName = blob.Prefix.TrimEnd('/');
                    subfolderLocalMemQueue.Enqueue(entityFullName);
                }
                var qMessage = new BlobQEntity { IsBlob = blob.IsBlob, EntityFullName = entityFullName, ContainerName = blobToSPManager.ContainerName, ContentLength = contentLength, ContentLengthDisplayText = contentLength?.GetFileSizeDisplayText() };
                blobMsgList.Add(qMessage);
            }

            //DO NOT CHANGE this OrderByDescending code.
            blobMsgList = blobMsgList.OrderByDescending(item => !item.IsBlob)
            .ThenBy(item => item.EntityFullName)
                                    .ToList();

            if (blobMsgList.Count > 0)
            {
                anyContainerHasData = true;
                queueManager.AddMessageToQueue(blobMsgList, fileProcessorQueueStorageConnection, fileProcessorWaitQueueName);
                depActivityStageManager.UpdateActivity(DateTime.UtcNow.AddSeconds(30), ActivityStage.PrepItemsForProcessorQueue);
            }
            if (subfolderLocalMemQueue.Count > 0)
            {
                var newPrefix = subfolderLocalMemQueue.Dequeue();
                IterateContainer(blobToSPManager, newPrefix);
            }

        }
    }
}
