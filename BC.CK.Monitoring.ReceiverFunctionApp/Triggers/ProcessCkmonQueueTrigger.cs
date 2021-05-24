using CK.Core;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CK.Monitoring.ReceiverFunctionApp.Model;
using CK.Monitoring.ReceiverFunctionApp.Services;

namespace CK.Monitoring.ReceiverFunctionApp.Triggers
{
    public class ProcessCkmonQueueTrigger
    {
        private readonly LogEntryCollector _logEntryCollector;

        public ProcessCkmonQueueTrigger(LogEntryCollector logEntryCollector)
        {
            _logEntryCollector = logEntryCollector;
        }

        [FunctionName(nameof(ProcessCkmonQueueTrigger))]
        public async Task RunAsync(
            [QueueTrigger(Constants.CkmonProcessQueueName, Connection = Constants.AzureStorageAccountName)]
            byte[] queueItem,
            ILogger log
        )
        {
            var item = await LogBatchQueueMessage.DeserializeAsync(queueItem);

            await using var ms = new MemoryStream(item.Contents);
            await using var br = new BrotliStream(ms, CompressionMode.Decompress, leaveOpen: true);
            using var ckbr = new CKBinaryReader(br, new UTF8Encoding(false), leaveOpen: true);

            if (!ReadHeaderAsync(ckbr))
            {
                throw new InvalidDataException("Invalid header in payload");
            }

            int streamVersion = ckbr.ReadInt32();
            ILogEntry? logEntry;
            while ((logEntry = LogEntry.Read(ckbr, streamVersion, out var _)) != null)
            {
                _logEntryCollector.CollectEntry(item.AppId, logEntry);
            }

            await _logEntryCollector.CommitEntriesAsync();
        }


        private static bool ReadHeaderAsync(CKBinaryReader bodyStream)
        {
            byte[] headerLen = new byte[LogReader.FileHeader.Length];
            bodyStream.Read(headerLen, 0, headerLen.Length);
            return headerLen.SequenceEqual(LogReader.FileHeader);
        }
    }
}