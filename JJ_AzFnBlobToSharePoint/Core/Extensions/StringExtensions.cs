using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JJ_AzFnBlobToSharePoint.Core.Extensions
{
    public static class StringExtensions
    {
        public static string RemovePrefix(this string s, int prefixLen)
        {
            if (s.Length < prefixLen)
                return string.Empty;
            return s.Substring(prefixLen);
        }
    }
}
