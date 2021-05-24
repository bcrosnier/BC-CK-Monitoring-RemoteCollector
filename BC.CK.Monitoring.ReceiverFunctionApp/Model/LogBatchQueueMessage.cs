using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CK.Core;

namespace CK.Monitoring.ReceiverFunctionApp.Model
{
    public class LogBatchQueueMessage
    {
        public string AppId { get; }
        public DateTime TimeUtc { get; }
        public byte[] Contents { get; }

        private LogBatchQueueMessage(string appId, DateTime timeUtc, byte[] contents)
        {
            AppId = appId;
            TimeUtc = timeUtc;
            Contents = contents;
        }

        public static async Task<byte[]> ReadAndSerializeAsync(
            DateTime timeUtc,
            string appId,
            Stream inStream
        )
        {
            await using var ms = new MemoryStream();
            await using (var bw = new CKBinaryWriter(ms, Encoding.Default, leaveOpen: true))
            {
                bw.Write(appId);
                bw.Write(timeUtc);
            }
            await inStream.CopyToAsync(ms);

            ms.Position = 0;
            return ms.ToArray();
        }

        public static async Task<LogBatchQueueMessage> DeserializeAsync(byte[] msg)
        {
            string appId;
            DateTime timeUtc;
            await using var ms = new MemoryStream(msg);
            using (var br = new CKBinaryReader(ms, Encoding.Default, leaveOpen: true))
            {
                appId = br.ReadString();
                timeUtc = br.ReadDateTime();
            }

            var contents = new ArraySegment<byte>(
                msg,
                Convert.ToInt32(ms.Position),
                Convert.ToInt32(ms.Length - ms.Position)
            ).ToArray();

            return new LogBatchQueueMessage(
                appId,
                timeUtc,
                contents
            );
        }
    }
}