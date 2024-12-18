using JJ_AzFnBlobToSharePoint.Core;
using JJ_AzFnBlobToSharePoint.Core.Models;
using FakeItEasy;
using Microsoft.Extensions.Logging;

namespace JJ_AzFnBlobToSharePoint.Tests
{
    public class CopyFilesToSharePointIntegrationTests
    {
        private string fileProcessorQueueStorageConnection;

        [SetUp]
        public void Setup()
        {
            fileProcessorQueueStorageConnection = Environment.GetEnvironmentVariable("CN_DEPFileProcessorQueueStorageConnection");
        }

        [Test]
        public void IntegrationHelper_InsertBlobEntityToArchiveMainQueue()
        {
            //Note: This is not a unit test mock, running this test helper will put the messages into the actual queue.
            //Make sure container and test entity exists before running this test case.
            //Note: JsonNode Property names are case sensitive.
            List<BlobQEntity> archiveQMessage = new List<BlobQEntity> { 
                new BlobQEntity {IsBlob=true,EntityFullName= "Level 0 - XYZ/Level 1 - XYZ/Case1.pdf", ContainerName= "test-container-1" },
                new BlobQEntity {IsBlob=true,EntityFullName= "Level 0 - XYZ/Level 1 - XYZ/Case2.pdf", ContainerName= "test-container-1" }
            };
            var logger = A.Fake<ILogger>();
            var queueManager = new QueueManager(logger);
            var waitQueueName = Environment.GetEnvironmentVariable("CN_DEPArchiveFileOnlyWaitQueue");
            queueManager.AddMessageToQueue(archiveQMessage,fileProcessorQueueStorageConnection,waitQueueName);
            Assert.DoesNotThrowAsync(async () => await queueManager.MovetoMainQueue(waitQueueName, Environment.GetEnvironmentVariable("CN_DEPArchiveMainQueue")));
        }


        [Test]
        public void IntegrationHelper_InsertNonBlobEntityToArchiveMainQueue()
        {
            //Note: This is not a unit test mock, running this test helper will put the messages into the actual queue.
            //Make sure container and test entity exists before running this test case.
            //Note: JsonNode Property names are case sensitive.
            List<BlobQEntity> archiveQMessage = new List<BlobQEntity> {
                new BlobQEntity {IsBlob=false,EntityFullName= "Level 0 - XYZ/Level 1 - XYZ", ContainerName= "test-container-1" },
                new BlobQEntity {IsBlob=false,EntityFullName= "Level 0 - XYZ/Level 1 - ABC", ContainerName= "test-container-1" }
            };
            var logger = A.Fake<ILogger>();
            var queueManager = new QueueManager(logger);
            var waitQueueName = Environment.GetEnvironmentVariable("CN_DEPArchiveFolderOnlyWaitQueue");
            queueManager.AddMessageToQueue(archiveQMessage, fileProcessorQueueStorageConnection, waitQueueName);
            Assert.DoesNotThrowAsync(async () => await queueManager.MovetoMainQueue(waitQueueName, Environment.GetEnvironmentVariable("CN_DEPArchiveMainQueue")));
        }
    }
}