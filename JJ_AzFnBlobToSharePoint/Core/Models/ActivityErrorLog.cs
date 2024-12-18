using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JJ_AzFnBlobToSharePoint.Core.Models
{
    public class ActivityErrorLog : ActivityLog
    {
        public string ErrorMessage { get; set; }
    }
}
