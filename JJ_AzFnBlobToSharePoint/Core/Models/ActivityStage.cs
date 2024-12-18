using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JJ_AzFnBlobToSharePoint.Core.Models
{
    public class ActivityStage
    {
        public const string PrepItemsForProcessorQueue = "PrepItemsForProcessorQueue";
        public const string SharePointCopy = "SharePointCopy";
        public const string ArchiveFilesOnly = "ArchiveFilesOnly";
        public const string ArchiveSourceFolders = "ArchiveSourceFolders";
        public const string ProcessError = "ProcessError";
    }
}
