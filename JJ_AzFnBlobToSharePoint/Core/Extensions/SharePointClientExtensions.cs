using Microsoft.SharePoint.Client;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading.Tasks;

namespace JJ_AzFnBlobToSharePoint.Core.Extensions
{
    public static class SharePointClientExtensions
    {
        public static bool TryGetFolderByServerRelativeUrl(this ClientContext clientContext, string libraryName, string relativeFolderUrl, out Folder folder)
        {
            try
            {
                folder = clientContext.Web.GetFolderByServerRelativeUrl($"{libraryName}/{relativeFolderUrl}");
                clientContext.Load(folder, f => f.Exists);
                clientContext.ExecuteQuery();
                return folder.Exists;
            }
            catch (ServerException ex)
            {
                folder = null;
                if (ex.ServerErrorTypeName == "System.IO.FileNotFoundException")
                    return false;
                throw ex;
            }
        }

        public static string GetParentDirectoryPath(this string path)
        {
            int idx = path.LastIndexOf('/');
            if (idx == -1) { return string.Empty;  }
            return path.Substring(0, idx);
        }
    }
}
