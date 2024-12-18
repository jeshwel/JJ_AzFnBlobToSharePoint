using JJ_AzFnBlobToSharePoint.Core.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JJ_AzFnBlobToSharePoint.Core.Models
{
    public class BlobQEntity : IMonitorRecord
    {
        public bool IsBlob { get; set; }
        public string EntityFullName { get; set; }

        /// <summary>
        /// Size in bytes.
        /// </summary>
        public long? ContentLength { get; set; }

        /// <summary>
        /// Displays size in KB, MB or GB.
        /// </summary>
        public string ContentLengthDisplayText { get; set; }
        public string ContainerName { get; set; }
        public int MessageRetryCount { get; set; }
        public int? RunId { get; set; }
    }
}
