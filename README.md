# CK-Monitoring-RemoteCollector
Sandbox project. A CK-Monitoring handler that sends log entries over HTTP/2.

## Installing

Install the [BC.CK.Monitoring.HttpSender](https://www.nuget.org/packages/BC.CK.Monitoring.HttpSender) ![NuGet](https://img.shields.io/nuget/v/BC.CK.Monitoring.HttpSender) package and configure CK-Monitoring.

### Configuring with CK.Monitoring.Hosting and `appsettings.json`

If you have [CK.Monitoring.Hosting](https://www.nuget.org/packages/CK.Monitoring.Hosting) ![NuGet](https://img.shields.io/nuget/v/CK.Monitoring.Hosting) and use `IHostBuilder.UseMonitoring()`, the following app settings can be used to enable the HttpSender handler:

```json
{
    "Monitoring": {
        "GrandOutput": {
            "Handlers": {
                "HttpSender": {
                    "Url": "<URL>",
                    "ApiKey": "<API Key>",
                    "AppId": "<AppId>"
                }
            }
        }
    }
}
```


## Handler configuration

The following configuration properties are required:

- `Url` : Required. The URL to use.
- `ApiKey` : Required. The API key to use.
- `AppId`: Required. The App ID to send to the server.

Optional properties:

- `ApiKeyHeaderName`: Optional. The name of the HTTP header that contains the API key. Defaults to `x-functions-key`.
- `DisableCertificateValidation`: Optional. Disables SSL validation errors. Defaults to false.
- `Timeout`: Optional. Maximum request timeout. Default to `00:00:30` (30 seconds).

## HTTP message contents

Log entries are buffered and processed on every call to `OnTimer()` (every 5 seconds by default).

The request is a HTTP/2 `POST` request on the configured `URL` with additional headers:
- `X-Functions-Key: <ApiKey>` - The API key. The name of the header can be changed with property `ApiKeyHeaderName`.
- `X-Ckmon-Appid: <AppId>` - The app ID.
- `Content-Length: <computed>`
- `User-Agent: BC.CK.Monitoring.HttpSender/<version>`
- `Content-Encoding: br`

The body is compressed using a `BrotliStream`, and contains the contents of a `.ckmon` binary file:
- `LogReader.FileHeader`,
- `LogReader.CurrentStreamVersion`,
- Every buffered `LogEntry`, one by one.