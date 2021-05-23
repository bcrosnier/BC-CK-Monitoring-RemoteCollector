using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CK.Core;
using CK.Monitoring.Handlers;
using Console = System.Console;

namespace CK.Monitoring.HttpSender.TestApp
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, eventArgs) => cts.Cancel();
            using var go = InitLogs();

            var p = new Program();
            await p.RunAsync(cts.Token);
        }

        async Task RunAsync(CancellationToken ct)
        {
            IActivityMonitor m = new ActivityMonitor();
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    m.Debug("Hello world (Debug)");
                    m.Trace("Hello world (Trace)");
                    m.Info("Hello world (Info)");
                    m.Warn("Hello world (Warn)");
                    m.Error("Hello world (Error)");
                    m.Fatal("Hello world (Fatal)");
                    await Task.Delay(100, ct);
                }
            }
            catch (Exception ex)
            {
                m.Fatal(ex);
            }
        }

        private static GrandOutput InitLogs()
        {
            LogFile.RootLogPath = Path.Combine(Environment.CurrentDirectory, "Logs");
            var goConfig = new GrandOutputConfiguration()
            {
                MinimalFilter = LogFilter.Debug,
                ExternalLogLevelFilter = LogLevelFilter.Debug,
                TimerDuration = TimeSpan.FromSeconds(5),
                Handlers =
                {
                    new HttpSenderConfiguration()
                    {
                        Url = "http://localhost:4712/api/ckmon/<appId>",
                        ApiKey = "MyApiKey",
                        AppId = "MyAppId",
                    },
                    new TextFileConfiguration()
                    {
                        Path = "Text"
                    },
                    new ConsoleConfiguration()
                }
            };
            return GrandOutput.EnsureActiveDefault(goConfig);
        }
    }
}