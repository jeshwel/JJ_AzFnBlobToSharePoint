using Azure.Storage.Queues.Models;
using JJ_AzFnBlobToSharePoint.Core.Contracts;
using JJ_AzFnBlobToSharePoint.Core.DataAccess;
using JJ_AzFnBlobToSharePoint.Core.Extensions;
using JJ_AzFnBlobToSharePoint.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace JJ_AzFnBlobToSharePoint.Core
{
    public class DEPActivityStageManager : IDEPActivityStageManager
    {
        private readonly ILogger logger;
        private readonly IDEPActivityRepository depActivityRepository;
        private readonly IQueueManager queueManager;
        private readonly INotificationManager notification;
        private readonly string fileProcessorQueueStorageConnection;
        private readonly string archiveMainQueue;
        private readonly string fileProcessorMainQueue;
        private string monitorRowKey;

        public DEPActivityStageManager(ILogger Logger, IDEPActivityRepository DEPActivityRepository, IQueueManager QueueManager, INotificationManager Notification)
        {
            logger = Logger;
            depActivityRepository = DEPActivityRepository;
            queueManager = QueueManager;
            notification = Notification;
            fileProcessorQueueStorageConnection = Environment.GetEnvironmentVariable("CN_DEPFileProcessorQueueStorageConnection");
            archiveMainQueue = Environment.GetEnvironmentVariable("CN_DEPArchiveMainQueue");
            fileProcessorMainQueue = Environment.GetEnvironmentVariable("CN_DEPFileProcessorMainQueue");
            monitorRowKey = DateTime.UtcNow.ToString("yyyyMMdd");
        }

        public bool CanPrepItemForFileProcessorQueue(int minutesAfterLastActivity = 3)
        {
            var depActivity = GetCompletedActivityStage(minutesAfterLastActivity);
            if (string.IsNullOrWhiteSpace(depActivity.ActivityStage))
                return true;
            else
                //Note: When changing this check BE CAUTIOUS! as PrepItem process should not run if there are errors in any other Activity Stages.
                return (depActivity.ActivityStage == ActivityStage.ArchiveSourceFolders && depActivity.StageCompleted);
        }

        public DEPActivity GetCompletedActivityStage(int minutesAfterLastActivity = 3)
        {
            var depActivity = depActivityRepository.GetCurrentActivityStage();
            depActivity.StageCompleted = false;
            //Note: There is a default functionlity to get ApproximateMessagesCount in AzureQueues and check if process is completed but it had issues intermittently,
            //hence added this custom logic GetCompletedActivityStage().

            var mainQueueHasAnyPendingMessages = CheckMainQueueHasAnyPendingMessages(depActivity?.ActivityStage);

            //If difference in last activity datetime versus current datetime is greater than minutesAfterLastActivity(i.e 3mins or more),
            //we assume activity of current stage has been completed.
            if (depActivity.LastActivityDateUtc != null)
            {
                var lastActivity = ((DateTime)depActivity.LastActivityDateUtc).AddMinutes(minutesAfterLastActivity);
                if (lastActivity < DateTime.UtcNow && !mainQueueHasAnyPendingMessages)
                    depActivity.StageCompleted = true;
            }
            return depActivity;
        }

        public async Task<bool> UpdateActivityStages()
        {
            var successEmailAddress = Environment.GetEnvironmentVariable("CN_SuccessEmailAddress");
            //Note: There is a default functionlity to check QueueCount in Azure but it had issues intermittently, hence added this custom logic GetCompletedActivityStage().
            var depActivity = GetCompletedActivityStage();
            var stageUpdated = false; //TODO is this var needed??
            if (depActivity.StageCompleted)
            {
                logger.LogInformation($"[UpdateActivityStages] StageCompleted: {depActivity?.ActivityStage}");
                switch (depActivity?.ActivityStage)
                {
                    case ActivityStage.PrepItemsForProcessorQueue:
                        int msgCount = await queueManager.MovetoMainQueue(Environment.GetEnvironmentVariable("CN_DEPFileProcessorWaitQueue"), Environment.GetEnvironmentVariable("CN_DEPFileProcessorMainQueue"));
                        if (msgCount > 0)
                        {
                            UpdateActivity(DateTime.UtcNow.AddSeconds(30), ActivityStage.SharePointCopy);
                            logger.LogInformation($"[UpdateActivityStages] Moved {msgCount} QItem(s) from FileProcessorWaitQueue to FileProcessorMainQueue, NewStage: {ActivityStage.SharePointCopy}");
                            stageUpdated = true;
                        }
                        break;
                    case ActivityStage.SharePointCopy:
                        msgCount = await queueManager.MovetoMainQueue(Environment.GetEnvironmentVariable("CN_DEPArchiveFileOnlyWaitQueue"), Environment.GetEnvironmentVariable("CN_DEPArchiveMainQueue"));
                        if (msgCount > 0)
                        {
                            //notification.PushMessageToEmailQ(successEmailAddress, NotificationType.SharePointCopy, activityStartDateEST, lastActivityDateEST, null, null);
                            UpdateActivity((DateTime)depActivity.LastActivityDateUtc, ActivityStage.ArchiveFilesOnly, depActivity.LastActivityDateUtc);
                            logger.LogInformation($"[UpdateActivityStages] Moved {msgCount} QItem(s) from ArchiveFileOnlyWaitQueue to ArchiveMainQueue, NewStage: {ActivityStage.ArchiveFilesOnly}");
                            stageUpdated = true;
                        }
                        break;
                    case ActivityStage.ArchiveFilesOnly:
                        msgCount = await queueManager.MovetoMainQueue(Environment.GetEnvironmentVariable("CN_DEPArchiveFolderOnlyWaitQueue"), Environment.GetEnvironmentVariable("CN_DEPArchiveMainQueue"), true);
                        if (msgCount > 0)
                        {
                            UpdateActivity(DateTime.UtcNow.AddSeconds(30), ActivityStage.ArchiveSourceFolders);
                            logger.LogInformation($"[UpdateActivityStages] Moved {msgCount} QItem(s) from ArchiveFolderOnlyWaitQueue to ArchiveMainQueue, NewStage: {ActivityStage.ArchiveSourceFolders}");
                            stageUpdated = true;
                        }
                        break;
                    case ActivityStage.ArchiveSourceFolders:
                        if (CheckWorkflowHasErrorInAnyStages(out List<ActivityErrorLog> Errors, out _))
                        {
                            logger.LogInformation($"[UpdateActivityStages] Workflow process completed with error(s), Errors: {JsonSerializer.Serialize(Errors)}");
                            UpdateActivity(DateTime.UtcNow, ActivityStage.ProcessError);
                        }
                        else
                        {
                            var activityStartDateEST = depActivity.ActivityStartDateUtc?.ConvertUtcToEST().ToString();
                            var lastActivityDateEST = depActivity.LastActivityDateUtc?.ConvertUtcToEST().ToString();
                            var depMonitor = depActivityRepository.GetMonitorLog(monitorRowKey);
                            var filesCopied = depMonitor?.GetFilesCopiedInLatestRun();
                            //Send email indicating process completed.
                            notification.SendEmailNotification(successEmailAddress, NotificationType.Archived, filesCopied, null, activityStartDateEST, lastActivityDateEST);
                            UpdateActivity(DateExtensions.CustomDateMinValue, string.Empty);
                            logger.LogInformation("[UpdateActivityStages] Workflow process completed successfully.");
                        }
                        stageUpdated = true;
                        break;
                }
            }
            return stageUpdated;
        }

        public bool UpdateActivityStart(DateTime ActivityStartDateUtc, DateTime LastActivityDateUtc, string ActivityStage)
        {
            return depActivityRepository.UpdateActivityStart(ActivityStartDateUtc, LastActivityDateUtc, ActivityStage);
        }

        public bool UpdateActivity(DateTime LastActivityDateUtc, string ActivityStage, DateTime? SharePointCopyCompletedDateUtc = null)
        {
            return depActivityRepository.UpdateActivity(LastActivityDateUtc, ActivityStage, SharePointCopyCompletedDateUtc);
        }

        public bool CreateMonitorLogRecordForNewRun()
        {
            var depMonitor = depActivityRepository.GetMonitorLog(monitorRowKey);
            //If monitor record is null create new record with runId as 1
            if (depMonitor == null)
            {
                logger.LogInformation($"MonitorRowKey: {monitorRowKey}, LatestRunId: 1");
                return depActivityRepository.AddMonitorLogRecord(monitorRowKey, 1);
            }
            else
            {
                depMonitor.LatestRunId++;
                logger.LogInformation($"MonitorRowKey: {monitorRowKey}, LatestRunId: {depMonitor.LatestRunId}");
                return depActivityRepository.UpdateMonitorLogRecord(monitorRowKey, depMonitor.LatestRunId);
            }
        }

        public bool AddErrorsToMonitorLogRecord(ActivityErrorLog NewError)
        {
            var depMonitor = depActivityRepository.GetMonitorLog(monitorRowKey) ?? throw new ApplicationException($"Could not find monitor log record for run date {monitorRowKey}.");
            NewError.RunId = depMonitor.LatestRunId;
            var errors = depMonitor.GetActivityErrorsEntity();
            errors.Add(NewError);
            depMonitor.SetActivityErrorsEntityAsString(errors);
            return depActivityRepository.UpdateMonitorLogRecord(monitorRowKey, NewError.RunId, Errors: depMonitor.Errors);
        }

        public bool AddFilesCopiedToMonitorLogRecord(List<BlobQEntity> NewlyCopiedFiles)
        {
            var depMonitor = depActivityRepository.GetMonitorLog(monitorRowKey) ?? throw new ApplicationException($"Could not find monitor log record for run date {monitorRowKey}.");
            NewlyCopiedFiles.ForEach(c => c.RunId = depMonitor.LatestRunId);
            var filesCopied = depMonitor.GetFilesCopiedInLatestRun();
            filesCopied.AddRange(NewlyCopiedFiles);
            depMonitor.SetFilesCopiedEntityAsString(filesCopied);
            return depActivityRepository.UpdateMonitorLogRecord(monitorRowKey, null, FilesCopied: depMonitor.FilesCopied);
        }

        public bool CheckWorkflowHasErrorInAnyStages(out List<ActivityErrorLog> Errors, out DateTime? ErrorNotifiedDateUtc)
        {
            Errors = new List<ActivityErrorLog>();
            ErrorNotifiedDateUtc = null;
            var depMonitor = depActivityRepository.GetMonitorLog(monitorRowKey);
            if (depMonitor != null)
            {
                Errors = depMonitor.GetActivityErrorsEntityForLatestRun();
                if (Errors.Count > 0)
                {
                    var depActivity = depActivityRepository.GetCurrentActivityStage();
                    if (depActivity.ErrorNotifiedDateUtc.HasValue && !depActivity.ErrorNotifiedDateUtc.Value.IsDateMinValue())
                        ErrorNotifiedDateUtc = depActivity.ErrorNotifiedDateUtc.Value;
                }
                return Errors.Count > 0;
            }
            return false;
        }

        public void CheckWorkflowHasErrorInAnyStagesAndSendNotification()
        {
            if (CheckWorkflowHasErrorInAnyStages(out List<ActivityErrorLog> Errors, out DateTime? ErrorNotifiedDateUtc))
            {
                //Check if any main queues has items to process.
                var hasPendingItems = CheckAllMainQueuesForAnyPendingMessages();
                if (!hasPendingItems && ErrorNotifiedDateUtc == null)
                {
                    //Send error notification.
                    SendErrorNotification(Errors);
                }
            }
        }

        public void SendErrorNotification(List<ActivityErrorLog> Errors)
        {
            notification.SendEmailNotification(Environment.GetEnvironmentVariable("CN_FailureEmailAddress"), NotificationType.ErrorList, null, Errors);
            depActivityRepository.UpdateActivity(DateTime.UtcNow, ActivityStage.ProcessError, null, DateTime.UtcNow);
        }

        public bool CheckAllMainQueuesForAnyPendingMessages()
        {
            //Check if any main queues has items to process.
            var hasPendingItems = CheckMainQueueHasAnyPendingMessages(ActivityStage.SharePointCopy);
            if (!hasPendingItems)
                hasPendingItems = CheckMainQueueHasAnyPendingMessages(ActivityStage.ArchiveSourceFolders);
            return hasPendingItems;
        }
        private bool CheckMainQueueHasAnyPendingMessages(string ActivityStage)
        {
            PeekedMessage queueMsg = null;
            switch (ActivityStage)
            {
                case Models.ActivityStage.PrepItemsForProcessorQueue:
                case Models.ActivityStage.SharePointCopy:
                    queueMsg = queueManager.PeekQueueMessage(fileProcessorQueueStorageConnection, fileProcessorMainQueue);
                    break;
                case Models.ActivityStage.ArchiveFilesOnly:
                case Models.ActivityStage.ArchiveSourceFolders:
                case "":
                    queueMsg = queueManager.PeekQueueMessage(fileProcessorQueueStorageConnection, archiveMainQueue);
                    break;
            }
            return (queueMsg != null);
        }
    }
}
