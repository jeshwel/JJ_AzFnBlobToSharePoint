using JJ_AzFnBlobToSharePoint.Core;
using JJ_AzFnBlobToSharePoint.Core.Contracts;
using JJ_AzFnBlobToSharePoint.Core.DataAccess;
using JJ_AzFnBlobToSharePoint.Core.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace JJ_AzFnBlobToSharePoint
{
    public class FnCopyFilesToSharePoint
    {
        private readonly ILogger logger;

        public FnCopyFilesToSharePoint(ILoggerFactory loggerFactory)
        {
            logger = loggerFactory.CreateLogger<FnCopyFilesToSharePoint>();
        }

        [Function("Step2-CopyFilesToSharePoint")]
        public async Task Run([QueueTrigger("dep-fileprocessor-main", Connection = "CN_DEPFileProcessorQueueStorageConnection")] string QMessage)
        {
            logger.LogInformation($"Queue input message : {QMessage}");
            //Note: JsonNode Property names are case sensitive.
            var qEntityList = JsonSerializer.Deserialize<List<BlobQEntity>>(QMessage);
            INotificationManager notificationManager = new NotificationManager(logger);
            IDEPActivityRepository depActivityRepository = new DEPActivityRepository();
            ISharePointManager sharePointManager = new SharePointManager(logger);
            IAzureBlobManager azureBlobManager = new AzureBlobManager(logger);
            IQueueManager queueManager = new QueueManager(logger);
            //TODO - Add a dependency injection framework, instead of below code.
            IDEPActivityStageManager depActivityStageManager = new DEPActivityStageManager(logger, depActivityRepository, queueManager, notificationManager); //As per the current design 1 queue message will have only folder/blobs belonging to 1 container.
            BlobToSharePointManager blobToSPManager = new BlobToSharePointManager(logger, sharePointManager, azureBlobManager, queueManager, qEntityList.First().ContainerName);
            var timer = new Stopwatch();
            timer.Start();
            try
            {
                await blobToSPManager.CopyBlobsToSharePoint(qEntityList);
                timer.Stop();
                TimeSpan timeTaken = timer.Elapsed;
                string res = "QMessage blob(s) copy process total time: " + timeTaken.ToString(@"m\:ss\.fff");
                logger.LogInformation($"{res}");
            }
            catch (Exception ex)
            {
                //Note: During the CopyBlobsToSharePoint operation there are chances that some files may have copied successfully and some which errored out,
                //so we will continue to next stage Archive in order to Archive the successfully copied files from source.
                depActivityStageManager.UpdateActivity(DateTime.UtcNow, ActivityStage.SharePointCopy);
                var activityErrorLog = ExceptionManager.SetActivityErrorEntity(ex, ActivityStage.SharePointCopy);
                depActivityStageManager.AddErrorsToMonitorLogRecord(activityErrorLog);
                //notificationManager.PushMessageToEmailQ(Environment.GetEnvironmentVariable("CN_FailureEmailAddress"),NotificationType.Error,null,null,ex.Message,ActivityStage.SharePointCopy);
                logger.LogError(ex.Message);
                throw ex;
            }
        }

       
    }
}
