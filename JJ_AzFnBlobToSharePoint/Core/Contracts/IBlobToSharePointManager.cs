using Azure;
using Azure.Storage.Blobs.Models;
using JJ_AzFnBlobToSharePoint.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JJ_AzFnBlobToSharePoint.Core.Contracts
{
    public interface IBlobToSharePointManager
    {
        string ContainerName { get; }
        Task<string> DownloadBlob(string BlobName);
        Task CopyBlobsToSharePoint(List<BlobQEntity> BlobQMessage);
        void CleanUpDownloadTempDirectory(bool AllFiles = false);
        Pageable<BlobHierarchyItem> GetOneLevelHierarchyBlobs(string prefix);
        void InitContainerInstance(string ContainerName);
    }
}
