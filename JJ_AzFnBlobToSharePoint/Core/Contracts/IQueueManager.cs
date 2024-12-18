using Azure.Storage.Queues.Models;
using System.Threading.Tasks;

namespace JJ_AzFnBlobToSharePoint.Core.Contracts
{
    public interface IQueueManager
    {
        void AddMessageToQueue(dynamic QMessage, string QueueStorageConnection, string QueueName);
        void ClearQueueMessages(string QueueStorageConnection, string QueueName);
        Task<int> MovetoMainQueue(string WaitQueueName, string MainQueueName, bool DoLIFOSort = false);
        PeekedMessage PeekQueueMessage(string QueueStorageConnection, string QueueName);
    }
}