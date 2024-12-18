using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace JJ_AzFnBlobToSharePoint.Core.Extensions
{
    public static class NumberExtensions
    {
        public static string GetFileSizeDisplayText(this long bytes)
        {
            var result = ConvertToNextSize(bytes);
            var sizeText = "KB";
            if (result > 1000)
            {
                result = ConvertToNextSize(result);
                sizeText = "MB";
            }
            if (result > 1000)
            {
                result = ConvertToNextSize(result);
                sizeText = "GB";
            }
            return string.Format("{0:0.##} {1}", result, sizeText);
        }

        public static bool FileSizeIsGreaterThan(this long bytes, int sizeInMB)
        {
            //size in "KB";
            var result = ConvertToNextSize(bytes);
            if (result > 1000)
            {
                //size in "MB";
                result = ConvertToNextSize(result);
                if (result > sizeInMB)
                    return true;
            }
            return false;
        }

        private static double ConvertToNextSize(double bytes)
        {
            return (bytes / 1024f);
        }
    }
}
