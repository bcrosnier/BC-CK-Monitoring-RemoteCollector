using System;
using System.Threading.Tasks;
using CK.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace CK.Monitoring.ReceiverFunctionApp
{
    public static class ReceiveCkmonTrigger
    {
        [FunctionName("ReceiveCkmonTrigger")]
        public static async Task<IActionResult> RunAsync(
            [HttpTrigger(
                AuthorizationLevel.Function,
                "post",
                Route = "ckmon/{appId}" // <host>/api/ckmon/{appId}
                )]
            HttpRequest req,
            [Queue("ckmonrcvbc", Connection = "AzureWebJobsCkmon")] ICollector<byte[]> queue,
            ILogger log,
            string appId
            )
        {
            string userAgent = req.Headers.GetValueWithDefault("user-agent", string.Empty);
            string contentEncoding = req.Headers.GetValueWithDefault("content-encoding", string.Empty);
            string contentLength = req.Headers.GetValueWithDefault("content-length", string.Empty);
            
            if (string.IsNullOrEmpty(appId))
            {
                log.LogError($"AppId is missing in request from {req.HttpContext.Connection.RemoteIpAddress}.");
                return new BadRequestResult();
            }
            if (string.IsNullOrEmpty(userAgent))
            {
                log.LogError($"User-Agent is missing in request from {req.HttpContext.Connection.RemoteIpAddress}.");
                return new BadRequestResult();
            }
            if (string.IsNullOrEmpty(contentEncoding))
            {
                log.LogError($"Content-Encoding is missing in request from {req.HttpContext.Connection.RemoteIpAddress}.");
                return new BadRequestResult();
            }
            if (contentEncoding != "br")
            {
                log.LogError($"Unsupported Content-Encoding: \"{contentEncoding}\" from {req.HttpContext.Connection.RemoteIpAddress}. Supported: \"br\".");
                return new BadRequestResult();
            }
            if (string.IsNullOrEmpty(contentLength))
            {
                log.LogError($"Content-Length is missing in request from {req.HttpContext.Connection.RemoteIpAddress}.");
                return new BadRequestResult();
            }
            
            log.LogInformation($"Receiving log entries from {req.HttpContext.Connection.RemoteIpAddress} " +
                               $"for AppId \"{appId}\". " +
                               $"User-Agent: {userAgent}. " +
                               $"Content-Encoding: {contentEncoding}. " +
                               $"Content-Length: {contentLength}.");

            var msg = await LogBatchQueueMessage.ReadAndSerializeAsync(DateTime.UtcNow, appId, req.Body);

            queue.Add(msg);
            return new OkResult();
        }
    }
}