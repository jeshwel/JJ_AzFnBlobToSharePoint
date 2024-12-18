using System;
using JJ_AzFnBlobToSharePoint.Core.DataAccess;

namespace JJ_AzFnBlobToSharePoint.Core.Contracts
{
    public interface IDEPActivityRepository
    {
        DEPActivity GetCurrentActivityStage();
        DEPMonitor GetMonitorLog(string RowKey);
        bool UpdateActivityStart(DateTime ActivityStartDateUtc, DateTime LastActivityDateUtc, string ActivityStage);
        bool UpdateActivity(DateTime LastActivityDateUtc, string ActivityStage, DateTime? SharePointCopyCompletedDateUtc = null, DateTime? ErrorNotifiedDateUtc = null);
        bool AddMonitorLogRecord(string RowKey, int LatestRunId);
        bool UpdateMonitorLogRecord(string RowKey, int? LatestRunId, string FilesCopied = null, string Errors = null);
    }
}