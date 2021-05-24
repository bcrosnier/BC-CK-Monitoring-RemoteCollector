using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using CK.Core;
using CK.Monitoring.ReceiverFunctionApp.Model;
using CK.Monitoring.ReceiverFunctionApp.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace CK.Monitoring.ReceiverFunctionApp.Triggers
{
    public class GetCkmonHttpTriggers
    {
        private readonly LogEntryCollector _logEntryCollector;

        public GetCkmonHttpTriggers(
            LogEntryCollector logEntryCollector
            )
        {
            _logEntryCollector = logEntryCollector;
        }

        [FunctionName("GetCkmonAppNames")]
        public async Task<IActionResult> GetAppNames(
            [HttpTrigger(
                AuthorizationLevel.Function,
                "get",
                Route = "ckmon" // <host>/api/ckmon
                )]
            HttpRequest req,
            ILogger log
            )
        {
            var appNames = await _logEntryCollector.GetAppNamesAsync();
            return new OkObjectResult(appNames);
        }

        [FunctionName("GetCkmonAppFiles")]
        public async Task<IActionResult> GetAppFiles(
            [HttpTrigger(
                AuthorizationLevel.Function,
                "get",
                Route = "ckmon/{appId}" // <host>/api/ckmon
            )]
            HttpRequest req,
            ILogger log,
            string appId
        )
        {
            var appFiles = await _logEntryCollector.GetAppFilesAsync(appId);
            return new OkObjectResult(appFiles);
        }
    }
}
