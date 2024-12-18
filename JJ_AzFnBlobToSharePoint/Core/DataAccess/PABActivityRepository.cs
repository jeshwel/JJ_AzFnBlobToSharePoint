using Azure;
using Azure.Data.Tables;
using JJ_AzFnBlobToSharePoint.Core.Contracts;
using JJ_AzFnBlobToSharePoint.Core.Extensions;
using System;
using System.Collections.Generic;

namespace JJ_AzFnBlobToSharePoint.Core.DataAccess
{
    public class DEPActivityRepository : IDEPActivityRepository
    {
        private readonly string tableName = "DEPActivity";
        //Note: Partition Key is case-sensitive
        private readonly string mainPartitionKey = "Activity";
        private readonly string monitorPartitionKey = "Monitor";
        private readonly string mainRowKey = "Main";
        //TODO use Key-Vault later on. Read from config now
        private readonly string storageConnection;

        public DEPActivityRepository()
        {
            storageConnection = Environment.GetEnvironmentVariable("CN_DEPFileProcessorQueueStorageConnection");
        }

        public DEPActivity GetCurrentActivityStage()
        {
            var depActivity = Get<DEPActivity>(mainPartitionKey, mainRowKey);
            return depActivity;
        }

        public bool UpdateActivityStart(DateTime ActivityStartDateUtc, DateTime LastActivityDateUtc, string ActivityStage)
        {
            //Since we need to just update specific columns, create a Dictionary object with those needed columns instead of
            //the entire columns in DEPActivity table.
            //Azure table only supports "ISO-8601 UTC formats" for DateTime data type.
            IDictionary<string, object> colValues = new Dictionary<string, object> {
            { "ActivityStage", ActivityStage},
            { "LastActivityDateUtc",  LastActivityDateUtc},
            { "ActivityStartDateUtc",  ActivityStartDateUtc},
            { "ErrorNotifiedDateUtc",  DateExtensions.CustomDateMinValue}, //reset date values.
            { "SharePointCopyCompletedDateUtc",  DateExtensions.CustomDateMinValue}, //reset date values.
            };

            UpdateBySpecificColumns(mainPartitionKey, mainRowKey, colValues);
            return true;
        }

        public bool UpdateActivity(DateTime LastActivityDateUtc, string ActivityStage, DateTime? SharePointCopyCompletedDateUtc = null, DateTime? ErrorNotifiedDateUtc = null)
        {
            //Since we need to just update specific columns, create a Dictionary object with those needed columns instead of
            //the entire columns in DEPActivity table.
            //Azure table only supports "ISO-8601 UTC formats" for DateTime data type.
            IDictionary<string, object> colValues = new Dictionary<string, object> {
            { "ActivityStage", ActivityStage}, //Note: If ActivityStage value comes as Null, Azure Table Storage retains existing value.
            { "LastActivityDateUtc",  LastActivityDateUtc},
            };

            if (ErrorNotifiedDateUtc != null)
                colValues.Add("ErrorNotifiedDateUtc", ErrorNotifiedDateUtc);

            if (SharePointCopyCompletedDateUtc != null)
                colValues.Add("SharePointCopyCompletedDateUtc", SharePointCopyCompletedDateUtc);

            UpdateBySpecificColumns(mainPartitionKey, mainRowKey, colValues);
            return true;
        }

        public DEPMonitor GetMonitorLog(string RowKey)
        {
            var depActivity = Get<DEPMonitor>(monitorPartitionKey, RowKey);
            return depActivity;
        }

        public bool AddMonitorLogRecord(string RowKey, int LatestRunId)
        {
            //Since we need to update specific columns, create a Dictionary object with just those needed columns instead of
            //the entire columns in DEPMonitor entity.
            //Azure table only supports "ISO-8601 UTC formats" for DateTime data type.
            IDictionary<string, object> colValues = new Dictionary<string, object> {
            { "LatestRunId", LatestRunId},
            //{ "ActivityStage",  ActivityStage}, //Note: If ActivityStage value comes as Null, Azure Table Storage retains existing value.
            { "ModifiedDateUtc",  DateTime.UtcNow}
            };
            return AddEntityBySpecificColumns(monitorPartitionKey, RowKey, colValues);
        }

        public bool UpdateMonitorLogRecord(string RowKey, int? LatestRunId, string FilesCopied = null, string Errors = null)
        {
            //Since we need to update specific columns, create a Dictionary object with just those needed columns instead of
            //the entire columns in DEPMonitor entity.
            //Azure table only supports "ISO-8601 UTC formats" for DateTime data type.
            IDictionary<string, object> colValues = new Dictionary<string, object> {
            //{ "ActivityStage", ActivityStage}, //Note: If ActivityStage value comes as Null, Azure Table Storage retains existing value.
            { "ModifiedDateUtc",  DateTime.UtcNow},
            };

            if (LatestRunId.HasValue)
            {
                if (LatestRunId > 0)
                    colValues.Add("LatestRunId", LatestRunId);
                else
                    throw new ApplicationException("LatestRunId value must be greater than zero.");
            }

            if (Errors != null)
                colValues.Add("Errors", Errors);

            if (FilesCopied != null)
                colValues.Add("FilesCopied", FilesCopied);

            return UpdateBySpecificColumns(monitorPartitionKey, RowKey, colValues);
        }

        private TEntity Get<TEntity>(string PartitionKey, string RowKey) where TEntity : class, ITableEntity
        {
            try
            {
                var tableClient = GetTableClientInstance();
                var response = tableClient.GetEntityIfExists<TEntity>(PartitionKey, RowKey);
                if (response.HasValue)
                    return response.Value;
                return default;
                //return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"[APP-ERROR] : Error occurred while fetching data from {tableName} table. Error: {ex.Message}");
            }
        }

        private TableClient GetTableClientInstance()
        {
            if (string.IsNullOrWhiteSpace(storageConnection))
                throw new ArgumentNullException("[GetTableClientInstance]: CN_DEPFileProcessorQueueStorageConnection is required.");
            var serviceClient = new TableServiceClient(storageConnection);
            var tableClient = serviceClient.GetTableClient(tableName);
            return tableClient;
        }

        private bool UpdateBySpecificColumns(string PartitionKey, string RowKey, IDictionary<string, object> values)
        {
            if (string.IsNullOrWhiteSpace(PartitionKey))
                throw new ArgumentNullException("[UpdateBySpecificColumns]: PartitionKey is required.");
            if (string.IsNullOrWhiteSpace(RowKey))
                throw new ArgumentNullException("[UpdateBySpecificColumns]: RowKey is required.");

            var tableClient = GetTableClientInstance();
            var entity = new TableEntity(PartitionKey, RowKey);
            foreach (var kv in values)
            {
                entity.Add(kv.Key, kv.Value);
            }
            var response = tableClient.UpdateEntity(entity, ETag.All);
            return (response.Status == 200 || response.Status == 204);
        }

        private bool AddEntityBySpecificColumns(string PartitionKey, string RowKey, IDictionary<string, object> values)
        {
            if (string.IsNullOrWhiteSpace(PartitionKey))
                throw new ArgumentNullException("[AddEntityBySpecificColumns]: PartitionKey is required");
            if (string.IsNullOrWhiteSpace(RowKey))
                throw new ArgumentNullException("[AddEntityBySpecificColumns]: RowKey is required");

            var tableClient = GetTableClientInstance();
            var entity = new TableEntity(PartitionKey, RowKey);
            foreach (var kv in values)
            {
                entity.Add(kv.Key, kv.Value);
            }
            var response = tableClient.AddEntity(entity);
            return (response.Status == 200 || response.Status == 204);
        }

    }
}
