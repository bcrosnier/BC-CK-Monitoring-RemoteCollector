using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using CK.Core;

// ReSharper disable UnusedType.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable CheckNamespace
namespace CK.Monitoring.Handlers
{
    /// <summary>
    /// A <see cref="IGrandOutputHandler"/> that collects log entries
    /// and POSTs then to an HTTP server when the <see cref="DispatcherSink"/> calls <see cref="OnTimer"/> on it.
    /// </summary>
    /// <seealso cref="HttpSenderConfiguration"/>
    public class HttpSender : IGrandOutputHandler
    {
        /// <summary>
        /// <para>
        /// The "NoSend" CKTrait. Log entries tagged with this trait are not collected by
        /// the <see cref="HttpSender"/> handler and are not transmitted, but are still processed locally.
        /// </para>
        /// <para>
        /// Notably used by the sender infrastructure, to avoid sending logs about itself
        /// (and potentially causing recursive send loops).
        /// </para>
        /// </summary>
        public static readonly CKTrait NoSendTrait = ActivityMonitor.Tags.Register("NoSend");

        private static readonly MediaTypeHeaderValue MediaType = new MediaTypeHeaderValue("application/ckmon-entries");
        private static readonly Encoding PayloadEncoding = new UTF8Encoding(false);

        private static readonly string AppIdHeaderName = @"x-ckmon-appid";

        private readonly List<GrandOutputEventInfo> _bufferedEntries;

        private HttpSenderConfiguration _configuration;
        private HttpClient? _httpClient;
        private MemoryStream? _buffer;
        private SocketsHttpHandler? _httpHandler;
        private Uri? _uri;

        public HttpSender(HttpSenderConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _bufferedEntries = new List<GrandOutputEventInfo>();
        }

        /// <inheritdoc />
        public bool Activate(IActivityMonitor m)
        {
            using (m.TemporarilySetAutoTags(NoSendTrait))
            {
                if (!_configuration.CheckValid(m)) return false;

                InitHttpClient(m);
                return true;
            }
        }

        /// <inheritdoc />
        public void OnTimer(IActivityMonitor m, TimeSpan timerSpan)
        {
            SendBufferedEntries(m);
        }

        private void SendBufferedEntries( IActivityMonitor m )
        {
            Debug.Assert(_httpClient != null, "_httpClient != null");
            Debug.Assert(_uri != null, nameof(_uri) + " != null");
            if (_bufferedEntries.Count == 0) return;

            using (m.TemporarilySetAutoTags(NoSendTrait))
            {
                m.SendLine(LogLevel.Trace, $"Sending {_bufferedEntries.Count} entries.");

                using (var msg = new HttpRequestMessage(HttpMethod.Post, _uri)
                {
                    Content = CreateContentFromEntries()
                })
                {
                    m.SendLine(LogLevel.Trace, $"Content-Length: {msg.Content.Headers.ContentLength}");
                    using var response = _httpClient.SendAsync(msg)
                        .GetAwaiter().GetResult(); // TODO: Use an async persistent queue?

                    response.EnsureSuccessStatusCode();
                    _bufferedEntries.Clear();
                }
            }
        }

        private HttpContent CreateContentFromEntries()
        {
            Debug.Assert(_buffer != null, "_buffer != null");

            _buffer.SetLength(0); // Clear buffer before writing entries

            using (var br = new BrotliStream(_buffer, CompressionLevel.Optimal, leaveOpen: true))
            using (var bw = new CKBinaryWriter(br, PayloadEncoding, leaveOpen: true))
            {
                bw.Write(LogReader.FileHeader);
                bw.Write(LogReader.CurrentStreamVersion);
                foreach (var bufferedEntry in _bufferedEntries)
                {
                    bufferedEntry.Entry.WriteLogEntry(bw);
                }
            }

            // _buffer cannot be used directly (StreamContent and HttpRequestMessage would dispose it).
            // But we can create a MemoryStream pointing to the same reference.
            var bufferClone = new MemoryStream(_buffer.GetBuffer()); // Disposed by StreamContent.
            bufferClone.SetLength(_buffer.Length);

            var c = new StreamContent(bufferClone); // Disposed by HttpRequestMessage.
            c.Headers.ContentLength = bufferClone.Length;
            c.Headers.ContentType = MediaType;
            c.Headers.ContentEncoding.Add("br"); // Brotli

            return c;
        }

        /// <inheritdoc />
        public void Handle(IActivityMonitor m, GrandOutputEventInfo logEvent)
        {
            Debug.Assert(_httpClient != null, "_httpClient != null");
            if (!logEvent.Entry.Tags.IsSupersetOf(NoSendTrait))
            {
                _bufferedEntries.Add(logEvent);
            }
        }

        /// <inheritdoc />
        public bool ApplyConfiguration(IActivityMonitor m, IHandlerConfiguration c)
        {
            using (m.TemporarilySetAutoTags(NoSendTrait))
            {
                if (c is HttpSenderConfiguration sc && sc.CheckValid(m))
                {
                    _configuration = sc;
                    InitHttpClient(m);
                    return true;
                }

                return false;
            }
        }

        /// <inheritdoc />
        public void Deactivate(IActivityMonitor m)
        {
            if (_httpClient != null)
            {
                SendBufferedEntries(m);
            }
            DestroyHttpClient();
        }

        void InitHttpClient( IActivityMonitor m )
        {
            DestroyHttpClient();

            _httpHandler = new SocketsHttpHandler();
            if (_configuration.DisableCertificateValidation)
            {
                m.SendLine(LogLevel.Warn, "Remote certificate validation is disabled in the HttpSender. " +
                                          "Make sure it is enabled in production environments.");
                _httpHandler.SslOptions.RemoteCertificateValidationCallback = delegate { return true; };
            }

            _httpClient = new HttpClient(new SocketsHttpHandler(), disposeHandler: true)
            {
                Timeout = _configuration.Timeout,
                DefaultRequestVersion = new Version(2, 0) // Use HTTP/2
            };

            _uri = new Uri(_configuration.Url.Replace(@"<appId>",
                _configuration.AppId)); // Use configured URI as default address

            _httpClient.DefaultRequestHeaders.UserAgent.Add(CreateProductInfoHeader());
            _httpClient.DefaultRequestHeaders.Add(_configuration.ApiKeyHeaderName, _configuration.ApiKey);
            _httpClient.DefaultRequestHeaders.Add(AppIdHeaderName, _configuration.AppId);
            
            using(m.OpenGroup(LogLevel.Trace, $"HttpSender will send logs to {_uri}."))
            {
                m.SendLine(LogLevel.Trace, $"User-Agent: {_httpClient.DefaultRequestHeaders.UserAgent}");
                m.SendLine(LogLevel.Trace, $"Timeout: {_configuration.Timeout}");
                m.SendLine(LogLevel.Trace, $"{AppIdHeaderName}: {_configuration.AppId}");
            }

            _buffer = new MemoryStream();
        }

        void DestroyHttpClient()
        {
            _httpClient?.Dispose();
            _httpClient = null;
            _httpHandler?.Dispose();
            _httpHandler = null;
            _uri = null;
        }

        static ProductInfoHeaderValue CreateProductInfoHeader()
        {
            var assembly = typeof(HttpSender).Assembly;
            FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);

            string productVersion = fileVersionInfo.ProductVersion;
            int delimiterIdx = productVersion.IndexOf('/');
            if (delimiterIdx >= 0)
            {
                productVersion = productVersion.Substring(0, delimiterIdx);
            }

            return new ProductInfoHeaderValue(
                assembly.GetName().Name,
                productVersion
            );
        }
    }
}