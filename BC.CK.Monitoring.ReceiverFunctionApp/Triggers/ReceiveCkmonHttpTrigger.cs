using System;
using System.Linq;
using System.Threading.Tasks;
using CK.Core;
using CK.Monitoring.ReceiverFunctionApp.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace CK.Monitoring.ReceiverFunctionApp.Triggers
{
    public static class ReceiveCkmonHttpTrigger
    {
        [FunctionName(nameof(ReceiveCkmonHttpTrigger))]
        public static async Task<IActionResult> RunAsync(
            [HttpTrigger(
                AuthorizationLevel.Function,
                "post",
                Route = "ckmon/{appId}" // <host>/api/ckmon/{appId}
                )]
            HttpRequest req,
            [Queue(Constants.CkmonProcessQueueName, Connection = Constants.AzureStorageAccountName)] ICollector<byte[]> queue,
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

            string? keyId = GetKeyId(req);

            log.LogInformation($"Receiving log entries from {req.HttpContext.Connection.RemoteIpAddress} " +
                               $"for AppId \"{appId}\". " +
                               $"User-Agent: {userAgent}. " +
                               $"Content-Encoding: {contentEncoding}. " +
                               $"Content-Length: {contentLength}. " +
                               $"Key ID: {keyId ?? "<unknown>"}.");

            var msg = await LogBatchQueueMessage.ReadAndSerializeAsync(DateTime.UtcNow, appId, req.Body);

            queue.Add(msg);
            return new OkResult();
        }

        private static string? GetKeyId(HttpRequest req)
        {
            var claim = req.HttpContext.User.Claims.FirstOrDefault(c => c.Type == Constants.AzureFunctionsKeyIdClaimType);
            return claim?.Value;
        }
    }
}