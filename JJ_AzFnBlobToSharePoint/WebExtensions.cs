using Microsoft.SharePoint.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JJ_AzFnBlobToSharePoint
{
    public static class WebExtensions
    {
        public static bool TryGetFileByServerRelativeUrl(this Web web, string serverRelativeUrl, out File file)
        {
            var ctx = web.Context;
            try
            {
                file = web.GetFileByServerRelativeUrl(serverRelativeUrl);
                ctx.Load(file);
                ctx.ExecuteQuery();
                return true;
            }
            catch (ServerException ex)
            {
                if (ex.ServerErrorTypeName == "System.IO.FileNotFoundException")
                {
                    file = null;
                    return false;
                }
                throw;
            }
        }
    }
}
