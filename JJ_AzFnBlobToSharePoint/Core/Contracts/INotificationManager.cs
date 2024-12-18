using JJ_AzFnBlobToSharePoint.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JJ_AzFnBlobToSharePoint.Core.Contracts
{
    public interface INotificationManager
    {
        void SendEmailNotification(string EmailAddress, NotificationType NotificationType, List<BlobQEntity> FilesCopied = null, List<ActivityErrorLog> ActivityErrors = null, string StartTime = null, string CompletionTime = null);
    }
}
