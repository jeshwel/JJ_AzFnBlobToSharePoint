using JJ_AzFnBlobToSharePoint.Core.DataAccess;
using JJ_AzFnBlobToSharePoint.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JJ_AzFnBlobToSharePoint.Core.Contracts
{
    public interface IDEPActivityStageManager
    {
        bool CanPrepItemForFileProcessorQueue(int minutesAfterLastActivity = 3);
        DEPActivity GetCompletedActivityStage(int minutesAfterLastActivity = 3);
        Task<bool> UpdateActivityStages();
        bool UpdateActivity(DateTime LastActivityDateUtc, string ActivityStage, DateTime? SharePointCopyCompletedDateUtc = null);
        bool UpdateActivityStart(DateTime ActivityStartDateUtc, DateTime LastActivityDateUtc, string ActivityStage);
        bool AddErrorsToMonitorLogRecord(ActivityErrorLog NewError);
        bool CheckAllMainQueuesForAnyPendingMessages();
        void SendErrorNotification(List<ActivityErrorLog> Errors);
        bool CheckWorkflowHasErrorInAnyStages(out List<ActivityErrorLog> Errors, out DateTime? ErrorNotifiedDateUtc);
        void CheckWorkflowHasErrorInAnyStagesAndSendNotification();
        bool AddFilesCopiedToMonitorLogRecord(List<BlobQEntity> NewlyCopiedFiles);
        bool CreateMonitorLogRecordForNewRun();
    }
}