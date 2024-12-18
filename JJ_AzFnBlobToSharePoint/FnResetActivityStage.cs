using Azure.Core;
using JJ_AzFnBlobToSharePoint.Core.Contracts;
using JJ_AzFnBlobToSharePoint.Core.DataAccess;
using JJ_AzFnBlobToSharePoint.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using JJ_AzFnBlobToSharePoint.Core.Extensions;

namespace JJ_AzFnBlobToSharePoint
{
    public class FnResetActivityStage
    {
        private readonly ILogger logger;
        public FnResetActivityStage(ILoggerFactory loggerFactory)
        {
            logger = loggerFactory.CreateLogger<FnResetActivityStage>();
        }

        [Function("ResetActivityStage")]
        public async Task<HttpResponseData> RunAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req, ExecutionContext executionContext)
        {
            try
            {
                logger.LogInformation("C# HTTP trigger ResetActivityStage requested.");
                var apiKey = string.Empty;
                if (req.Headers.TryGetValues("dep-api-key", out IEnumerable<string> headerValues))
                    apiKey = headerValues.FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(apiKey) && apiKey.Equals(Environment.GetEnvironmentVariable("CN_APIKey")))
                {
                    IDEPActivityRepository depActivityRepository = new DEPActivityRepository();
                    IQueueManager queueManager = new QueueManager(logger);
                    INotificationManager notificationManager = new NotificationManager(logger);
                    var activityStageManager = new DEPActivityStageManager(logger, depActivityRepository, queueManager, notificationManager);
                    activityStageManager.UpdateActivity(DateExtensions.CustomDateMinValue, string.Empty, DateExtensions.CustomDateMinValue);
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
                    response.WriteString("Success");
                    return response;
                }
                else
                {
                    var response = req.CreateResponse(HttpStatusCode.Unauthorized);
                    return response;
                }

            }
            catch (Exception ex)
            {
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
                response.WriteString(ex.Message);
                return response;
            }
        }
    }
}
