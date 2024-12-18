using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JJ_AzFnBlobToSharePoint.Core.Contracts
{
    public interface ISharePointManager
    {
        void CreateFolderIfNotExists(string FolderPath);
        Microsoft.SharePoint.Client.File UploadBlobFile(string SharePointFolderUrl, string TempFilePath);
    }
}
