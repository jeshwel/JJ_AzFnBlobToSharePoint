using Azure;
using JJ_AzFnBlobToSharePoint.Core.Contracts;
using JJ_AzFnBlobToSharePoint.Core.Models;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace JJ_AzFnBlobToSharePoint.Core.DataAccess
{
    public class DEPActivity : Azure.Data.Tables.ITableEntity
    {
        public string ActivityStage { get; set; }
        public DateTime? ActivityStartDateUtc { get; set; }
        public DateTime? LastActivityDateUtc { get; set; }
        public DateTime? SharePointCopyCompletedDateUtc { get; set; }
        public DateTime? ErrorNotifiedDateUtc { get; set; }
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        //Non-Table field
        public bool StageCompleted { get; set; }
    }
}