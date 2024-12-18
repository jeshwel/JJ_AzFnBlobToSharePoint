using Azure.Storage.Blobs;
using System.Threading.Tasks;

namespace JJ_AzFnBlobToSharePoint.Core.Contracts
{
    public interface IAzureBlobManager
    {
        Task<string> DownloadBlob(BlobContainerClient container, string BlobName, string TempDirectoryPath);
    }
}