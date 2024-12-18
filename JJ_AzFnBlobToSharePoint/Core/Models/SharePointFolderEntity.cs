using Microsoft.SharePoint.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JJ_AzFnBlobToSharePoint.Core.Models
{
    public class SharePointFolderEntity
    {
        public string ServerRelativeFolderUrl { get; set; }
        public Folder SPFolder { get; set; }
        public bool CreateFolderInLibRoot { get; set; }
    }
}
