using JJ_AzFnBlobToSharePoint.Core;
using JJ_AzFnBlobToSharePoint.Core.Contracts;
using JJ_AzFnBlobToSharePoint.Core.DataAccess;
using JJ_AzFnBlobToSharePoint.Core.Models;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace JJ_AzFnBlobToSharePoint.Tests
{
    public class MonitorDEPActivityStagesTests
    {
        private ILogger logger;
        private IDEPActivityRepository depActivityRepositoryStub;
        private IQueueManager queueManagerStub;
        private INotificationManager notificationStub;

        [SetUp]
        public void Setup()
        {
            logger = A.Fake<ILogger>();
            depActivityRepositoryStub = A.Fake<IDEPActivityRepository>();
            queueManagerStub = A.Fake<IQueueManager>();
            notificationStub = A.Fake<INotificationManager>();
        }

        [Test]
        public async Task ShouldNotUpdateCurrentStageWhenActivityIsInProgress()
        {
            var startDateUTC = DateTime.UtcNow.AddMinutes(-30);
            var lastActivityDateUTC = DateTime.UtcNow.AddMinutes(-1);
            A.CallTo(() => depActivityRepositoryStub.GetCurrentActivityStage()).Returns(new DEPActivity { ActivityStage = ActivityStage.SharePointCopy, ActivityStartDateUtc = startDateUTC, LastActivityDateUtc = lastActivityDateUTC });
            IDEPActivityStageManager depActivityStageManager = new DEPActivityStageManager(logger, depActivityRepositoryStub, queueManagerStub, notificationStub);
            var stageUpdated = await depActivityStageManager.UpdateActivityStages();
            Assert.That(stageUpdated, Is.False);
        }

        [Test]
        public async Task ShouldNotUpdateStageWhenMainQueueHasMessage()
        {
            var startDateUTC = DateTime.UtcNow.AddMinutes(-30);
            var lastActivityDateUTC = DateTime.UtcNow.AddMinutes(-5);
            A.CallTo(() => depActivityRepositoryStub.GetCurrentActivityStage()).Returns(new DEPActivity { ActivityStage = ActivityStage.SharePointCopy, ActivityStartDateUtc = startDateUTC, LastActivityDateUtc = lastActivityDateUTC });
            IDEPActivityStageManager depActivityStageManager = new DEPActivityStageManager(logger, depActivityRepositoryStub, queueManagerStub, notificationStub);
            var stageUpdated = await depActivityStageManager.UpdateActivityStages();
            Assert.That(stageUpdated, Is.False);
        }

        [Test]
        public async Task ShouldUpdateStageAsArchiveFilesOnly()
        {
            var startDateUTC = DateTime.UtcNow.AddMinutes(-30);
            var lastActivityDateUTC = DateTime.UtcNow.AddMinutes(-5);
            A.CallTo(() => depActivityRepositoryStub.GetCurrentActivityStage()).Returns(new DEPActivity { ActivityStage = ActivityStage.SharePointCopy, ActivityStartDateUtc = startDateUTC, LastActivityDateUtc = lastActivityDateUTC });
            A.CallTo(() => queueManagerStub.PeekQueueMessage(A<string>.Ignored, A<string>.Ignored)).Returns(null);
            A.CallTo(() => queueManagerStub.MovetoMainQueue(A<string>.Ignored, A<string>.Ignored, false)).Returns(1);

            IDEPActivityStageManager depActivityStageManager = new DEPActivityStageManager(logger, depActivityRepositoryStub, queueManagerStub, notificationStub);
            var stageUpdated = await depActivityStageManager.UpdateActivityStages();

            //Assertions
            A.CallTo(() => depActivityRepositoryStub.UpdateActivityStart(A<DateTime>.Ignored, A<DateTime>.Ignored, A<string>.That.Matches(x => x == ActivityStage.ArchiveFilesOnly))).MustHaveHappenedOnceExactly();
            Assert.That(stageUpdated, Is.True);
        }

        [Test]
        public async Task ShouldUpdateStageAsArchiveSourceFolders()
        {
            var startDateUTC = DateTime.UtcNow.AddMinutes(-30);
            var lastActivityDateUTC = DateTime.UtcNow.AddMinutes(-5);
            A.CallTo(() => depActivityRepositoryStub.GetCurrentActivityStage()).Returns(new DEPActivity { ActivityStage = ActivityStage.ArchiveFilesOnly, ActivityStartDateUtc = startDateUTC, LastActivityDateUtc = lastActivityDateUTC });
            A.CallTo(() => queueManagerStub.PeekQueueMessage(A<string>.Ignored, A<string>.Ignored)).Returns(null);
            A.CallTo(() => queueManagerStub.MovetoMainQueue(A<string>.Ignored, A<string>.Ignored, true)).Returns(1);

            IDEPActivityStageManager depActivityStageManager = new DEPActivityStageManager(logger, depActivityRepositoryStub, queueManagerStub, notificationStub);
            var stageUpdated = await depActivityStageManager.UpdateActivityStages();

            //Assertions
            A.CallTo(() => depActivityRepositoryStub.UpdateActivity(A<DateTime>.Ignored, A<string>.That.Matches(x => x == ActivityStage.ArchiveSourceFolders), null, null)).MustHaveHappenedOnceExactly();
            Assert.That(stageUpdated, Is.True);
        }

        [Test]
        public void ShouldCreateNewMonitorRecord()
        {
            A.CallTo(() => depActivityRepositoryStub.GetMonitorLog(A<string>.Ignored)).Returns(null);
            A.CallTo(() => depActivityRepositoryStub.AddMonitorLogRecord(A<string>.Ignored, A<int>.Ignored)).Returns(true);

            IDEPActivityStageManager depActivityStageManager = new DEPActivityStageManager(logger, depActivityRepositoryStub, queueManagerStub, notificationStub);
            var result = depActivityStageManager.CreateMonitorLogRecordForNewRun();

            //Assertions
            A.CallTo(() => depActivityRepositoryStub.GetMonitorLog(A<string>.That.Matches(x => x == DateTime.UtcNow.ToString("yyyyMMdd")))).MustHaveHappenedOnceExactly();
            A.CallTo(() => depActivityRepositoryStub.AddMonitorLogRecord(A<string>.Ignored, A<int>.Ignored)).MustHaveHappenedOnceExactly();
            Assert.That(result, Is.True);
        }

        [Test]
        public void ShouldUpdateMonitorRecordLatestRunId()
        {
            A.CallTo(() => depActivityRepositoryStub.GetMonitorLog(A<string>.Ignored)).Returns(new DEPMonitor { LatestRunId = 1 });
            A.CallTo(() => depActivityRepositoryStub.UpdateMonitorLogRecord(A<string>.Ignored, A<int>.Ignored, null, null)).Returns(true);

            IDEPActivityStageManager depActivityStageManager = new DEPActivityStageManager(logger, depActivityRepositoryStub, queueManagerStub, notificationStub);
            var result = depActivityStageManager.CreateMonitorLogRecordForNewRun();

            //Assertions
            A.CallTo(() => depActivityRepositoryStub.GetMonitorLog(A<string>.That.Matches(x => x == DateTime.UtcNow.ToString("yyyyMMdd")))).MustHaveHappenedOnceExactly();
            A.CallTo(() => depActivityRepositoryStub.UpdateMonitorLogRecord(A<string>.Ignored, A<int>.That.Matches(x => x == 2), null, null)).MustHaveHappenedOnceExactly();
            Assert.That(result, Is.True);
        }

        [Test]
        public void ShouldSaveFirstErrorToMonitorRecord()
        {
            var error = new ActivityErrorLog { ActivityStage = ActivityStage.SharePointCopy, ErrorMessage = "Error 1", Container = "test-container-1", RunId = 1 };
            A.CallTo(() => depActivityRepositoryStub.GetMonitorLog(A<string>.Ignored)).Returns(new DEPMonitor { LatestRunId = 1 });
            A.CallTo(() => depActivityRepositoryStub.UpdateMonitorLogRecord(A<string>.Ignored, A<int>.Ignored, A<string>.Ignored, A<string>.Ignored)).Returns(true);

            IDEPActivityStageManager depActivityStageManager = new DEPActivityStageManager(logger, depActivityRepositoryStub, queueManagerStub, notificationStub);
            var result = depActivityStageManager.AddErrorsToMonitorLogRecord(error);

            //Assertions
            A.CallTo(() => depActivityRepositoryStub.GetMonitorLog(A<string>.That.Matches(x => x == DateTime.UtcNow.ToString("yyyyMMdd")))).MustHaveHappenedOnceExactly();
            A.CallTo(() => depActivityRepositoryStub.UpdateMonitorLogRecord(A<string>.Ignored, A<int>.Ignored, null, A<string>.That.Matches(x => x.Contains("Error 1")))).MustHaveHappenedOnceExactly();
            Assert.That(result, Is.True);
        }

        [Test]
        public void ShouldAddNewErrorsToExistingErrorList()
        {
            var existingErrors = new List<ActivityErrorLog> { new ActivityErrorLog { ActivityStage = ActivityStage.SharePointCopy, ErrorMessage = "Error 1", Container = "test-container-1", RunId = 1 } };

            var newError = new ActivityErrorLog { ActivityStage = ActivityStage.SharePointCopy, ErrorMessage = "Error 2", Container = "test-container-1", RunId = 1 };
            A.CallTo(() => depActivityRepositoryStub.GetMonitorLog(A<string>.Ignored)).Returns(new DEPMonitor { LatestRunId = 1, Errors = JsonSerializer.Serialize(existingErrors) });
            A.CallTo(() => depActivityRepositoryStub.UpdateMonitorLogRecord(A<string>.Ignored, A<int>.Ignored, A<string>.Ignored, A<string>.Ignored)).Returns(true);

            IDEPActivityStageManager depActivityStageManager = new DEPActivityStageManager(logger, depActivityRepositoryStub, queueManagerStub, notificationStub);
            var result = depActivityStageManager.AddErrorsToMonitorLogRecord(newError);

            //Assertions
            A.CallTo(() => depActivityRepositoryStub.GetMonitorLog(A<string>.That.Matches(x => x == DateTime.UtcNow.ToString("yyyyMMdd")))).MustHaveHappenedOnceExactly();
            A.CallTo(() => depActivityRepositoryStub.UpdateMonitorLogRecord(A<string>.Ignored, A<int>.Ignored, null, A<string>.That.Matches(x => x.Contains("Error 1") && x.Contains("Error 2")))).MustHaveHappenedOnceExactly();
            Assert.That(result, Is.True);
        }

        //[Test]
        //public void OnWorkflowError_ShouldNOTSendErrorNotification_IfMainQueuesHasPendingMessages()
        //{
        //    var existingErrors = new List<ActivityErrorLog> { new ActivityErrorLog { ActivityStage = ActivityStage.SharePointCopy, ErrorMessage = "Error 1", Container = "test-container-1", RunId = 1 } };
        //    A.CallTo(() => depActivityRepositoryStub.GetMonitorLog(A<string>.Ignored)).Returns(new DEPMonitor { ActivityStage = ActivityStage.SharePointCopy, LatestRunId = 1, Errors = JsonSerializer.Serialize(existingErrors) });
        //    //A.CallTo(() => queueManagerStub.PeekQueueMessage(A<string>.Ignored, A<string>.Ignored)).Returns(null);
        //    //A.CallTo(() => queueManagerStub.MovetoMainQueue(A<string>.Ignored, A<string>.Ignored, true)).Returns(1);


        //    IDEPActivityStageManager depActivityStageManager = new DEPActivityStageManager(logger, depActivityRepositoryStub, queueManagerStub, notificationStub);
        //    var activityStageManagerWrapper = A.Fake<IDEPActivityStageManager>(x => x.Wrapping(depActivityStageManager)); //unconfigured calls will be forwarded to real wrapped class.
        //    //A.CallTo(() => activityStageManagerWrapper.CheckAllMainQueuesForAnyPendingMessages()).Returns(true);
        //    activityStageManagerWrapper.CheckWorkflowHasErrorInAnyStagesAndSendNotification();

        //    //Assertions

        //    //Note: Wrapped assertion not working, so commenting out below code.
        //    //A.CallTo(() => activityStageManagerWrapper.CheckAllMainQueuesForAnyPendingMessages()).MustHaveHappenedOnceExactly();
        //    //A.CallTo(() => activityStageManagerWrapper.SendErrorNotification(A<List<ActivityErrorLog>>.Ignored)).MustNotHaveHappened();

        //    A.CallTo(() => queueManagerStub.PeekQueueMessage(A<string>.Ignored, A<string>.Ignored)).MustHaveHappenedTwiceExactly();
        //    A.CallTo(() => notificationStub.PushMessageToEmailQ(Environment.GetEnvironmentVariable("CN_FailureEmailAddress"), A<NotificationType>.That.Matches(x => x == NotificationType.ErrorList), A<List<ActivityErrorLog>>.That.Matches(x => x.Count == 1))).MustNotHaveHappened();

        //}

        #region IntegrationTests

        [Test]
        public void IntegrationTest_ShouldSendErrorNotification()
        {
            var testData = new List<ActivityErrorLog> {
                new ActivityErrorLog { RunId=1, ActivityStage = ActivityStage.SharePointCopy, Container="test-container-1", ErrorMessage="File download failed.", FileName="test001.pdf" },
                new ActivityErrorLog { RunId=1, ActivityStage = ActivityStage.SharePointCopy, Container="test-container-1", ErrorMessage="SP API timeout." },
            };

            //Note: Here notificationManager instance is not a stub, so it will send an actual email to specified address.
            var notification = new NotificationManager(logger);
            var notificationWrapper = A.Fake<INotificationManager>(x => x.Wrapping(notification)); //unconfigured calls will be forwarded to real wrapped class.

            IDEPActivityStageManager depActivityStageManager = new DEPActivityStageManager(logger, depActivityRepositoryStub, queueManagerStub, notificationWrapper);
            depActivityStageManager.SendErrorNotification(testData);

            //Assertions
            A.CallTo(() => notificationWrapper.SendEmailNotification(Environment.GetEnvironmentVariable("CN_FailureEmailAddress"), A<NotificationType>.That.Matches(x => x == NotificationType.ErrorList), null, A<List<ActivityErrorLog>>.That.Matches(x => x.Count == 2), null, null)).MustHaveHappenedOnceExactly();
        }

        [Test]
        public async Task IntegrationTest_ShouldSendEmailAfterCopyingToSharePoint()
        {
            var startDateUTC = DateTime.UtcNow.AddMinutes(-30);
            var lastActivityDateUTC = DateTime.UtcNow.AddMinutes(-5);
            A.CallTo(() => depActivityRepositoryStub.GetCurrentActivityStage()).Returns(new DEPActivity { ActivityStage = ActivityStage.SharePointCopy, ActivityStartDateUtc = startDateUTC, LastActivityDateUtc = lastActivityDateUTC });
            A.CallTo(() => queueManagerStub.PeekQueueMessage(A<string>.Ignored, A<string>.Ignored)).Returns(null);
            A.CallTo(() => queueManagerStub.MovetoMainQueue(A<string>.Ignored, A<string>.Ignored, false)).Returns(1);

            //Note: Here notificationManager instance is not a stub, so it will send an actual email to specified address.
            var notificationManager = new NotificationManager(logger);
            IDEPActivityStageManager depActivityStageManager = new DEPActivityStageManager(logger, depActivityRepositoryStub, queueManagerStub, notificationManager);
            var stageUpdated = await depActivityStageManager.UpdateActivityStages();

            //Assertions
            A.CallTo(() => depActivityRepositoryStub.UpdateActivityStart(A<DateTime>.Ignored, A<DateTime>.Ignored, A<string>.Ignored)).MustHaveHappenedOnceExactly();
            Assert.That(stageUpdated, Is.True);
        }

        [Test]
        public async Task IntegrationTest_ShouldSendEmailAfterArchiveCompletion()
        {
            var startDateUTC = DateTime.UtcNow.AddMinutes(-30);
            var lastActivityDateUTC = DateTime.UtcNow.AddMinutes(-5);
            var filesCopied = new List<BlobQEntity> {
                new BlobQEntity { RunId=1, ContainerName="test-container-1", EntityFullName="abc/test001.pdf", ContentLength=18374145, ContentLengthDisplayText="17.52 MB", IsBlob=true },
                new BlobQEntity { RunId=2, ContainerName="test-container-1", EntityFullName="dep/test002.mp4",ContentLength=1528838398, ContentLengthDisplayText="1.42 GB", IsBlob=true },
                new BlobQEntity { RunId=2, ContainerName="test-container-1", EntityFullName="abc/testdoc001.docx",ContentLength=84922, ContentLengthDisplayText="82.93 KB", IsBlob=true },
                new BlobQEntity { RunId=2, ContainerName="test-container-1", EntityFullName="abc/test003.mp4",ContentLength=627179875, ContentLengthDisplayText="598.13 MB", IsBlob=true }
            };
            A.CallTo(() => queueManagerStub.PeekQueueMessage(A<string>.Ignored, A<string>.Ignored)).Returns(null);
            A.CallTo(() => depActivityRepositoryStub.GetCurrentActivityStage()).Returns(new DEPActivity { ActivityStage = ActivityStage.ArchiveSourceFolders, ActivityStartDateUtc = startDateUTC, LastActivityDateUtc = lastActivityDateUTC });
            A.CallTo(() => depActivityRepositoryStub.GetMonitorLog(A<string>.Ignored)).Returns(new DEPMonitor { LatestRunId = 2, FilesCopied = JsonSerializer.Serialize(filesCopied) });

            //Note: Here notificationManager instance is not a stub, so it will send an actual email to specified address.
            var notificationManager = new NotificationManager(logger);
            IDEPActivityStageManager depActivityStageManager = new DEPActivityStageManager(logger, depActivityRepositoryStub, queueManagerStub, notificationManager);
            var stageUpdated = await depActivityStageManager.UpdateActivityStages();

            //Assertions
            A.CallTo(() => depActivityRepositoryStub.GetMonitorLog(A<string>.Ignored)).MustHaveHappenedTwiceExactly();
            A.CallTo(() => depActivityRepositoryStub.UpdateActivity(A<DateTime>.Ignored, A<string>.Ignored, null, null)).MustHaveHappenedOnceExactly();
            Assert.That(stageUpdated, Is.True);
        }

        #endregion


    }

}