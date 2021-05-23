using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CK.Core;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace CK.Monitoring.ReceiverFunctionApp
{
    public class ProcessCkmonQueueTrigger
    {
        private readonly LogEntryCollector _logEntryCollector;

        public ProcessCkmonQueueTrigger(LogEntryCollector logEntryCollector)
        {
            _logEntryCollector = logEntryCollector;
        }

        [FunctionName("ProcessCkmonQueueTrigger")]
        public async Task RunAsync(
            [QueueTrigger("ckmonrcvbc", Connection = "AzureWebJobsCkmon")]
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