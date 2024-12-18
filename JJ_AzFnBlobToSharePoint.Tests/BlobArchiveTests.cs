using FakeItEasy;
using JJ_AzFnBlobToSharePoint.Core;
using JJ_AzFnBlobToSharePoint.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace JJ_AzFnBlobToSharePoint.Tests
{
    public class BlobArchiveTests
    {
        private BlobArchiveManager blobArchiveManager;

        [SetUp]
        public void Setup()
        {
            var logger = A.Fake<ILogger>();
            blobArchiveManager = new BlobArchiveManager(logger);
        }

        [Test]
        public void ShouldArchiveBlobEntity()
        {
            //Make sure container and test entity exists before running this test case.
            //Note: JsonNode Property names are case sensitive.
            var qMessage = @"[{""IsBlob"":true,""EntityFullName"":""Level 0 - XYZ/Level 1 - XYZ/Case1.pdf"",""ContainerName"":""test-container-1""}]";
            var qEntityList = JsonSerializer.Deserialize<List<BlobQEntity>>(qMessage);
            Assert.DoesNotThrowAsync(async () => await blobArchiveManager.ArchiveEntity(qEntityList));
        }

        [Test]
        public void ShouldDeleteEmptySubFolder()
        {
            //Make sure container and test entity exists before running this test case.
            //Note: JsonNode Property names are case sensitive.
            var qMessage = @"[{""IsBlob"":false,""EntityFullName"":""Level 0 - XYZ/Level 1 - XYZ"",""ContainerName"":""test-container-1""}]";
            var qEntityList = JsonSerializer.Deserialize<List<BlobQEntity>>(qMessage);
            Assert.DoesNotThrowAsync(async () => await blobArchiveManager.ArchiveEntity(qEntityList));
        }

        [Test]
        public void NonEmptyFolderDelete_ShouldThrowException()
        {
            //Make sure container and test entity exists before running this test case.
            //Note: JsonNode Property names are case sensitive.
            var qMessage = @"[{""IsBlob"":false,""EntityFullName"":""Level 0 - ABC"",""ContainerName"":""test-container-1""}]";
            var qEntityList = JsonSerializer.Deserialize<List<BlobQEntity>>(qMessage);
            var exMsg = Assert.ThrowsAsync<Azure.RequestFailedException>(async () => await blobArchiveManager.ArchiveEntity(qEntityList));
            Assert.That(exMsg?.ErrorCode, Is.EqualTo("DirectoryIsNotEmpty"));
        }
    }
}