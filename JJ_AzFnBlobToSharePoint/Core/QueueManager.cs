using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JJ_AzFnBlobToSharePoint.Core.Contracts;
using Azure.Storage.Queues;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Azure.Storage.Queues.Models;
using Microsoft.Identity.Client;

namespace JJ_AzFnBlobToSharePoint.Core
{
    public class QueueManager : IQueueManager
    {
        private readonly ILogger logger;

        public QueueManager(ILogger Logger)
        {
            logger = Logger;
        }
        public async Task<int> MovetoMainQueue(string WaitQueueName, string MainQueueName, bool DoLIFOSort = false)
        {
            var fileProcessorStorageConnection = Environment.GetEnvironmentVariable("CN_DEPFileProcessorQueueStorageConnection");
            var targetqueue = GetCloudQueueRef(fileProcessorStorageConnection, MainQueueName);
            var waitQueue = GetCloudQueueRef(fileProcessorStorageConnection, WaitQueueName);
            var count = 0;
            var LIFOStack = new Stack<CloudQueueMessage>();
            try
            {
                while (true)
                {
                    var msg = await waitQueue.GetMessageAsync();
                    if (msg == null)
                        break;

                    await waitQueue.DeleteMessageAsync(msg);
                    if (DoLIFOSort)
                        LIFOStack.Push(msg);
                    else
                        await targetqueue.AddMessageAsync(msg);

                    count++;
                }

                //e.g Inorder to delete source folders without throwing "DirectoryIsNotEmpty" error, the subfolders should be deleted first before parent folder.
                while (LIFOStack.Count > 0)
                {
                    await targetqueue.AddMessageAsync(LIFOStack.Pop());
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Move to {MainQueueName} queue failed : {ex.Message}");
                throw ex;
            }

            return count;
        }

        public void AddMessageToQueue(dynamic QMessage, string QueueStorageConnection, string QueueName)
        {
            var queueClient = GetQueueClient(QueueStorageConnection, QueueName);
            queueClient.SendMessage(JsonSerializer.Serialize(QMessage));
        }

        public void ClearQueueMessages(string QueueStorageConnection, string QueueName)
        {
            var queueClient = GetQueueClient(QueueStorageConnection, QueueName);
            queueClient.ClearMessages();
        }

        public PeekedMessage PeekQueueMessage(string QueueStorageConnection, string QueueName)
        {
            var queueClient = GetQueueClient(QueueStorageConnection, QueueName);
            return queueClient.PeekMessage();
        }

        private QueueClient GetQueueClient(string QueueStorageConnection, string QueueName)
        {
            if (string.IsNullOrWhiteSpace(QueueStorageConnection))
                throw new ArgumentNullException("QueueStorageConnection is required.");
            if (string.IsNullOrWhiteSpace(QueueName))
                throw new ArgumentNullException("QueueName is required.");
            // Instantiate a QueueClient which will be used to create and manipulate the queue
            QueueClient queueClient = new QueueClient(QueueStorageConnection, QueueName, new QueueClientOptions
            {
                MessageEncoding = QueueMessageEncoding.Base64
            });

            // Create the queue if it doesn't already exist
            queueClient.CreateIfNotExists();
            return queueClient;
        }

        private static CloudQueue GetCloudQueueRef(string QueueStorageConnection, string QueueName)
        {
            if (string.IsNullOrWhiteSpace(QueueStorageConnection))
                throw new ArgumentNullException("QueueStorageConnection is required");
            if (string.IsNullOrWhiteSpace(QueueName))
                throw new ArgumentNullException("QueueName is required");

            var storageAccount = CloudStorageAccount.Parse(QueueStorageConnection);
            var queueClient = storageAccount.CreateCloudQueueClient();
            var queue = queueClient.GetQueueReference(QueueName);
            return queue;
        }

        private bool StringParamHasValue(string stringToValidate, string paramName, out string ErrorMsg)
        {
            ErrorMsg = null;
            if (string.IsNullOrWhiteSpace(stringToValidate))
            {
                ErrorMsg = $"{paramName} is required";
                return false;
            }
            return true;
        }
    }
}
