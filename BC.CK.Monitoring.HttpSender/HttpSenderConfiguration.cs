using System;
using CK.Core;

// ReSharper disable CheckNamespace
namespace CK.Monitoring.Handlers
{
    /// <summary>
    /// The configuration to use for the <see cref="HttpSender"/> handler.
    /// </summary>
    public class HttpSenderConfiguration : IHandlerConfiguration
    {
        /// <summary>
        /// The URL to POST log entry batches to.
        /// It should include an "&lt;appId&gt;" token
        /// that is replaced with the AppId.
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// The API key sent in the HTTP header defined in <see cref="ApiKeyHeaderName"/>.
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// The name of the HTTP header containing the <see cref="ApiKey"/>.
        /// Defaults to "x-functions-key" (Azure Functions key).
        /// Supports US-ASCII letters, numbers and punctuation.
        /// </summary>
        public string ApiKeyHeaderName { get; set; } = "x-functions-key";

        /// <summary>
        /// The application identifier sent in HTTP header "x-ckmon-appid", and/or in <see cref="Url"/>.
        /// Supports US-ASCII letters, numbers and punctuation.
        /// </summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>
        /// Whether to disable server certificate validation entirely.
        /// Do not set to true in production environments.
        /// </summary>
        public bool DisableCertificateValidation { get; set; }

        /// <summary>
        /// The maximum time a request can take to send a log batch.
        /// Defaults to 30 seconds.
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <inheritdoc />
        public IHandlerConfiguration Clone()
        {
            return new HttpSenderConfiguration()
            {
                Url = Url,
                ApiKey = ApiKey,
                AppId = AppId,
                DisableCertificateValidation = DisableCertificateValidation,
            };
        }

        /// <summary>
        /// Checks that this <see cref="HttpSenderConfiguration"/>
        /// is valid for usage by the <see cref="HttpSender"/>.
        /// </summary>
        /// <param name="m">The monitor to log to.</param>
        /// <returns>True if valid, false otherwise.</returns>
        public bool CheckValid(IActivityMonitor m)
        {
            bool isValid = true;

            if (string.IsNullOrEmpty(Url))
            {
                m.SendLine(
                    LogLevel.Error,
                    $"Property {nameof(Url)} is not set" +
                    $" in {nameof(HttpSenderConfiguration)}."
                );
                isValid = false;
            }

            if (!string.IsNullOrEmpty(Url) && !IsValidURI(Url))
            {
                m.SendLine(
                    LogLevel.Error,
                    $"{nameof(Url)} \"{Url}\" is not a valid HTTP or HTTPS URL" +
                    $" in {nameof(HttpSenderConfiguration)}."
                );
                isValid = false;
            }

            if (string.IsNullOrEmpty(AppId))
            {
                m.SendLine(
                    LogLevel.Error,
                    $"Property {nameof(AppId)} is not set" +
                    $" in {nameof(HttpSenderConfiguration)}."
                );
                isValid = false;
            }

            if (string.IsNullOrEmpty(ApiKey))
            {
                m.SendLine(
                    LogLevel.Error,
                    $"Property {nameof(ApiKey)} is not set" +
                    $" in {nameof(HttpSenderConfiguration)}."
                );
                isValid = false;
            }

            if (string.IsNullOrEmpty(ApiKey))
            {
                m.SendLine(
                    LogLevel.Error,
                    $"Property {nameof(ApiKeyHeaderName)} is not set" +
                    $" in {nameof(HttpSenderConfiguration)}."
                );
                isValid = false;
            }

            return isValid;
        }

        static bool IsValidURI(string uri)
        {
            if (!Uri.TryCreate(uri, UriKind.Absolute, out var tmp))
                return false;
            return tmp.Scheme == Uri.UriSchemeHttp || tmp.Scheme == Uri.UriSchemeHttps;
        }
    }
}