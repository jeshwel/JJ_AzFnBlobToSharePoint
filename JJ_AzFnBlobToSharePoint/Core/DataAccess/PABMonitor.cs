using Azure;
using JJ_AzFnBlobToSharePoint.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace JJ_AzFnBlobToSharePoint.Core.DataAccess
{
    public class DEPMonitor : Azure.Data.Tables.ITableEntity
    {
        public int LatestRunId { get; set; }
        public string Errors { get; set; }
        public string FilesCopied { get; set; }
        public DateTime ModifiedDateUtc { get; set; }
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        //Note: The current Azure.Data.Tables (12.8.0) library does not support [EntityPropertyConverter] and Override void ReadEntity method, so JSON objects
        //stored in tables need to have a workaround method like below to Deserialize them to a Class type.
        public List<ActivityErrorLog> GetActivityErrorsEntity()
        {
            if (!string.IsNullOrWhiteSpace(Errors))
                return JsonSerializer.Deserialize<List<ActivityErrorLog>>(Errors);
            return new List<ActivityErrorLog>();
        }
        public List<ActivityErrorLog> GetActivityErrorsEntityForLatestRun()
        {
            var errors = GetActivityErrorsEntity();
            errors = errors.Where(q => q.RunId == LatestRunId).ToList();
            return errors;
        }

        public List<BlobQEntity> GetFilesCopiedEntity()
        {
            if (!string.IsNullOrWhiteSpace(FilesCopied))
                return JsonSerializer.Deserialize<List<BlobQEntity>>(FilesCopied);
            return new List<BlobQEntity>();
        }

        public List<BlobQEntity> GetFilesCopiedInLatestRun()
        {
            var filesCopied = GetFilesCopiedEntity();
            filesCopied = filesCopied.Where(q => q.RunId == LatestRunId).ToList();
            return filesCopied;
        }

        public void SetActivityErrorsEntityAsString(List<ActivityErrorLog> ActivityErrorLogs)
        {
            Errors = JsonSerializer.Serialize(ActivityErrorLogs);
        }

        public void SetFilesCopiedEntityAsString(List<BlobQEntity> FilesCopied)
        {
            this.FilesCopied = JsonSerializer.Serialize(FilesCopied);
        }
    }
}