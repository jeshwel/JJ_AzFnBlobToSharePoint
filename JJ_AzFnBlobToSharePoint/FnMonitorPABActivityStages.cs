using JJ_AzFnBlobToSharePoint.Core;
using JJ_AzFnBlobToSharePoint.Core.Contracts;
using JJ_AzFnBlobToSharePoint.Core.DataAccess;
using JJ_AzFnBlobToSharePoint.Core.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.SharePoint.Client;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CN_FnMonitorDEPActivityStages
{
    public class FnMonitorDEPActivityStages
    {
        private readonly ILogger logger;
        public FnMonitorDEPActivityStages(ILoggerFactory loggerFactory)
        {
            logger = loggerFactory.CreateLogger<FnMonitorDEPActivityStages>();
        }

        [Function("MonitorDEPActivityStages")]
        public async Task Run([TimerTrigger("0 */4 * * * *", RunOnStartup = false)] MyInfo myTimer)
        {
            //runs every day at 10:00:00 & 2pm
            //"0 0 10,14 * * *"
            logger.LogInformation($"Timer trigger function executed at: {DateTime.Now}");
            //TODO - Add a dependency injection framework, instead of below code.
            IDEPActivityRepository depActivityRepository = new DEPActivityRepository();
            IQueueManager queueManager = new QueueManager(logger);
            INotificationManager notification = new NotificationManager(logger);
            IDEPActivityStageManager depActivityStageManager = new DEPActivityStageManager(logger, depActivityRepository, queueManager, notification);
            //Note: Below function checks if an Activity Stage is completed and sets flow to Next Stage.
            try
            {
                await depActivityStageManager.UpdateActivityStages();
            }
            finally
            {
                //Note: Do not call CheckWorkflowHasErrorInAnyStagesAndSendNotification before UpdateActivityStages method.
                depActivityStageManager.CheckWorkflowHasErrorInAnyStagesAndSendNotification();
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




