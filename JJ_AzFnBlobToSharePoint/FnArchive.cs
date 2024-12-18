using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using JJ_AzFnBlobToSharePoint.Core;
using JJ_AzFnBlobToSharePoint.Core.Contracts;
using JJ_AzFnBlobToSharePoint.Core.DataAccess;
using JJ_AzFnBlobToSharePoint.Core.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace JJ_AzFnBlobToSharePoint
{
    public class FnArchive
    {
        private readonly ILogger logger;

        public FnArchive(ILoggerFactory loggerFactory)
        {
            logger = loggerFactory.CreateLogger<FnArchive>();
        }

        [Function("Step3-Archive")]
        public async Task Run([QueueTrigger("dep-archive-main", Connection = "CN_DEPFileProcessorQueueStorageConnection")] string QMessage)
        {
            IDEPActivityRepository depActivityRepository = new DEPActivityRepository();
            INotificationManager notificationManager = new NotificationManager(logger);
            IQueueManager queueManager = new QueueManager(logger);
            //TODO - Add a dependency injection framework, instead of below code.
            IDEPActivityStageManager depActivityStageManager = new DEPActivityStageManager(logger, depActivityRepository, queueManager, notificationManager);
            try
            {
                logger.LogInformation($"Archive queue input : {QMessage}");
                //Note: JsonNode Property names are case sensitive.
                //QMessage = @"[{""IsBlob"":true,""EntityFullName"":""rt-101/case-sub-10/subFile10.txt"",""ContainerName"":""dep-root""}]";
                var qEntityList = JsonSerializer.Deserialize<List<BlobQEntity>>(QMessage);
                IBlobArchiveManager archiveManager = new BlobArchiveManager(logger);
                await archiveManager.ArchiveEntity(qEntityList);
                if (archiveManager.ErrorBlobQEntities.Count > 0)
                {
                    //Will retry as per logic defined in ArchiveEntity method.
                    //Re-add the message to queue so that processing order is also changed.
                    queueManager.AddMessageToQueue(archiveManager.ErrorBlobQEntities, Environment.GetEnvironmentVariable("CN_DEPFileProcessorQueueStorageConnection"), Environment.GetEnvironmentVariable("CN_DEPArchiveMainQueue"));
                }
                if(archiveManager.HasErrors)
                    depActivityStageManager.UpdateActivity(DateTime.UtcNow, ActivityStage.ProcessError);
            }
            catch (Exception ex) //Any non-handled errors inside ArchiveEntity method also comes here.
            {
                depActivityStageManager.UpdateActivity(DateTime.UtcNow, ActivityStage.ProcessError);
                //notificationManager.PushMessageToEmailQ(Environment.GetEnvironmentVariable("CN_FailureEmailAddress"), NotificationType.Error, null, null, ex.Message, "Archive");
                logger.LogError(ex.Message);
                throw ex;
            }
        }


    }
}
