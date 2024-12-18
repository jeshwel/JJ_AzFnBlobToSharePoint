using JJ_AzFnBlobToSharePoint.Core;
using JJ_AzFnBlobToSharePoint.Core.Contracts;
using JJ_AzFnBlobToSharePoint.Core.DataAccess;
using JJ_AzFnBlobToSharePoint.Core.Exceptions;
using JJ_AzFnBlobToSharePoint.Core.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;

namespace CN_FnPrepItemsForFileProcessorQueue
{
    public class FnPrepItemsForFileProcessorQueue
    {
        private readonly ILogger logger;
        public FnPrepItemsForFileProcessorQueue(ILoggerFactory loggerFactory)
        {
            logger = loggerFactory.CreateLogger<FnPrepItemsForFileProcessorQueue>();
        }

        [Function("Step1-PrepItemsForFileProcessorQueue")]
        public void Run([TimerTrigger("%CN_TriggerSchedule%", RunOnStartup = false)] MyInfo myTimer)
        {
            //runs every day at 10:00:00 & 2pm UTC
            //"0 0 10,14 * * *"
            //runs every day at 10pm EST
            //"0 0 2 * * *" Azure takes in UTC time only, so EST 10pm is 2AM UTC.
            logger.LogInformation($"Timer trigger function executed at: {DateTime.Now}");
            INotificationManager notificationManager = new NotificationManager(logger);
            IQueueManager queueManager = new QueueManager(logger);
            IDEPActivityRepository depActivityRepository = new DEPActivityRepository();
            //TODO - Add a dependency injection framework, instead of below code.
            IDEPActivityStageManager depActivityStageManager = new DEPActivityStageManager(logger, depActivityRepository, queueManager, notificationManager);
            try
            {
                if (depActivityStageManager.CanPrepItemForFileProcessorQueue(4))
                {
                    depActivityStageManager.UpdateActivityStart(DateTime.UtcNow, DateTime.UtcNow.AddSeconds(30), ActivityStage.PrepItemsForProcessorQueue);
                    depActivityStageManager.CreateMonitorLogRecordForNewRun();
                    //Note: Current project framework is .net 4.8 due to CSOM lib usage.
                    //TODO - If the project need to be updated to latest .Net Core, the CSOM lib basic auth must be changed to Azure Entra Auth as latest CSOM lib does not support basic auth.
                    //TODO - Add a dependency injection framework, instead of below code.
                    ISharePointManager sharePointManager = new SharePointManager(logger);
                    IAzureBlobManager azureBlobManager = new AzureBlobManager(logger);
                    IBlobToSharePointManager blobToSharePointManager = new BlobToSharePointManager(logger, sharePointManager, azureBlobManager, queueManager);
                    IPrepItemsForFileProcessorQueue prepItemsForFileProcessorQueue = new PrepItemsForFileProcessorQueue(logger, blobToSharePointManager, queueManager, depActivityStageManager);
                    prepItemsForFileProcessorQueue.Start();
                }
            }
           
            catch (Exception ex)
            {
                var activityErrorLog = ExceptionManager.SetActivityErrorEntity(ex, ActivityStage.PrepItemsForProcessorQueue);
                //Note: Since this is the first stage in the flow we can reset the current ActivityStage here.
                depActivityStageManager.UpdateActivity(DateTime.UtcNow, string.Empty);
                depActivityStageManager.AddErrorsToMonitorLogRecord(activityErrorLog);
                throw ex;
            }
        }
    }

    public class MyInfo
    {
        public MyScheduleStatus ScheduleStatus { get; set; }

        public bool IsPastDue { get; set; }
    }

    public class MyScheduleStatus
    {
        public DateTime Last { get; set; }

        public DateTime Next { get; set; }

        public DateTime LastUpdated { get; set; }
    }
}
