using JJ_AzFnBlobToSharePoint.Core.Contracts;
using JJ_AzFnBlobToSharePoint.Core.Extensions;
using JJ_AzFnBlobToSharePoint.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;

namespace JJ_AzFnBlobToSharePoint.Core
{
    public class NotificationManager : INotificationManager
    {
        private string currentENV;
        private string jobName;
        public NotificationManager(ILogger Logger)
        {
            currentENV = Environment.GetEnvironmentVariable("CN_DeployedEnvironment");
            jobName = Environment.GetEnvironmentVariable("CN_JobName");
        }
        public void SendEmailNotification(string EmailAddress, NotificationType NotificationType, List<BlobQEntity> FilesCopied = null, List<ActivityErrorLog> ActivityErrors = null, string StartTime = null, string CompletionTime = null)
        {
            if (string.IsNullOrWhiteSpace(EmailAddress))
                throw new ArgumentNullException("Notification EmailAddress is not configured.");

            if (!string.IsNullOrWhiteSpace(currentENV))
                currentENV = $"{currentENV} : ";

            var fromAddress = "dep.no-reply@app.org";
            string subject = string.Empty;
            var mainContent = new StringBuilder();
            switch (NotificationType)
            {
                case NotificationType.Archived:
                    mainContent.Append($"{jobName}: Azure Storage to SharePoint transfer completed. <br/>Start time: {StartTime}, Completion time: {CompletionTime}. <br/><br/>Following are the processed files:");
                    var totFileSize = FilesCopied.Sum(c => c.ContentLength ?? 0).GetFileSizeDisplayText();
                    mainContent.Append($"<br/><br/>########################## Total file(s) transferred: <b>{FilesCopied.Count}</b>, Total transfer size: <b>{totFileSize}</b> ##########################<br/>");
                    var infoContent = GenerateFileInfoEmailContent(FilesCopied);
                    mainContent.Append(infoContent);
                    AddEmailFootNote(mainContent);
                    subject = $"{currentENV}{jobName}: Azure Storage to SharePoint transfer completed";
                    break;
                case NotificationType.ErrorList:
                    mainContent.Append("Following errors occurred during process:<br/>");
                    infoContent = GenerateFileInfoEmailContent(ActivityErrors);
                    mainContent.Append(infoContent);
                    AddEmailFootNote(mainContent);
                    subject = $"{currentENV}ERROR: {jobName}: Error occurred during process";
                    break;
            }

            SendEmail(EmailAddress, fromAddress, subject, mainContent.ToString());
        }

        private string GenerateFileInfoEmailContent<TEntity>(List<TEntity> FileRecordList) where TEntity : class, IMonitorRecord
        {
            StringBuilder sbResult = new StringBuilder();
            foreach (var rFile in FileRecordList)
            {
                if (rFile is ActivityErrorLog errFile)
                {
                    sbResult.Append($"<br/><b>ActivityStage:</b> {errFile.ActivityStage}, <b>Error:</b> {errFile.ErrorMessage}, <b>FileName:</b> {errFile.FileName}, <b>Container:</b> {errFile.Container}");
                }
                if (rFile is BlobQEntity recFile)
                {
                    var contentLengthDisplayTextHtml = (bool)(recFile.ContentLength?.FileSizeIsGreaterThan(300)) ? $"<b>{recFile.ContentLengthDisplayText}</b>" : recFile.ContentLengthDisplayText;
                    sbResult.Append($"<br/><b>Container:</b> {recFile.ContainerName}, <b>Full FileName:</b> {recFile.EntityFullName}, <b>FileSize:</b> {contentLengthDisplayTextHtml}");
                }
            }
            return sbResult.ToString();
        }

        private void AddEmailFootNote(StringBuilder MainContent)
        {
            MainContent.Append("<br/><br/><b>Note: The special character # will be removed from folder name(s) in SharePoint.</b>");
        }

        private void SendEmail(string ToAddresses, string FromAddress, string Subject, string EmailContent)
        {
            MailMessage msg = new MailMessage();
            msg.To.Add(ToAddresses);
            msg.From = new MailAddress(FromAddress);
            msg.Subject = Subject ?? "Email subject content not defined.";
            msg.Body = EmailContent ?? "Email content not defined.";
            msg.IsBodyHtml = true;

            var smtpHost = Environment.GetEnvironmentVariable("CN_SMTPHost");
            if (string.IsNullOrWhiteSpace(smtpHost))
                throw new Exception("[APP-ERROR] : SMTP/Email Host not configured.");
            SmtpClient client = new SmtpClient();
            client.UseDefaultCredentials = false;
            client.Port = 587; // You can use Port 25 if 587 is blocked 
            client.Host = smtpHost;
            client.DeliveryMethod = SmtpDeliveryMethod.Network;
            client.EnableSsl = true;
            client.Send(msg);
        }
    }
}
