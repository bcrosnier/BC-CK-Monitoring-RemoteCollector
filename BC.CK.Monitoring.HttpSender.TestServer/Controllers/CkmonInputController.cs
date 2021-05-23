using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using CK.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace CK.Monitoring.HttpSender.TestServer.Controllers
{
    public class CkmonInputController : Controller
    {
        private readonly IOptionsSnapshot<CkmonCollectorOptions> _collectorOptions;

        public CkmonInputController(IOptionsSnapshot<CkmonCollectorOptions> collectorOptions)
        {
            _collectorOptions = collectorOptions;
        }

        [HttpPost("/api/ckmon")]
        public async Task<IActionResult> Index()
        {
            if (!CheckApiKey()) return Unauthorized(new {message = "Invalid API key."});

            string? appId = GetAppId();
            if (appId == null) return BadRequest(new {message = "Invalid app ID."});

            Stream? bodyStream = DecodeRequestBody();
            if(bodyStream == null) return BadRequest(new {message = "Unsupported Content-Encoding."});

            MemoryStream decompressedStream = new MemoryStream(); // Disposed in BatchProcessor
            await bodyStream.CopyToAsync(decompressedStream);
            decompressedStream.Position = 0;

            var batch = new LogEntryBatch(appId, decompressedStream);

            return Ok();
        }

        public readonly struct LogEntryBatch
        {
            public LogEntryBatch(string appId, Stream contentStream)
            {
                AppId = appId;
                ContentStream = contentStream;
            }
            public string AppId { get; }
            public Stream ContentStream { get; }
        }

        private bool ReadHeaderAsync(CKBinaryReader bodyStream)
        {
            byte[] headerLen = new byte[LogReader.FileHeader.Length];
            bodyStream.Read(headerLen, 0, headerLen.Length);
            return headerLen.SequenceEqual(LogReader.FileHeader);
        }

        private Stream? DecodeRequestBody()
        {
            string contentEncoding = Request.Headers.GetValueWithDefault(HeaderNames.ContentEncoding, string.Empty);

            switch (contentEncoding)
            {
                case "br":
                    return new BrotliStream(Request.Body, CompressionMode.Decompress, leaveOpen: true);
                case "gzip":
                    return new GZipStream(Request.Body, CompressionMode.Decompress, leaveOpen: true);
                case "":
                    return Request.Body;
                default:
                    return null;
            }
        }

        bool CheckApiKey()
        {
            if (
                Request.Headers.TryGetValue("x-ckmon-apikey", out var keys)
                && keys.Count == 1
                && !string.IsNullOrEmpty(keys[0])
                && keys[0] == _collectorOptions.Value.ApiKey
            )
            {
                return true;
            }

            return false;
        }

        string? GetAppId()
        {
            if (
                Request.Headers.TryGetValue("x-ckmon-appid", out var keys)
                && keys.Count == 1
                && !string.IsNullOrEmpty(keys[0])
            )
            {
                return keys[0];
            }

            return null;
        }
    }
}