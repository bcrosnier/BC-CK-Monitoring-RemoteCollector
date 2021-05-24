using CK.Core;
using CK.Monitoring.Handlers;
using CK.Monitoring.ReceiverFunctionApp;
using CK.Monitoring.ReceiverFunctionApp.Services;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

[assembly: FunctionsStartup(typeof(Startup))]

namespace CK.Monitoring.ReceiverFunctionApp
{
    public class Startup : FunctionsStartup
    {
        public Startup()
        {
            var goc = new GrandOutputConfiguration()
            {
                Handlers =
                {
                    new ConsoleConfiguration()
                }
            };
            GrandOutput.EnsureActiveDefault(goc);

            var m = new ActivityMonitor();
            m.Info($"Starting up.");
        }

        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.TryAddScoped<IActivityMonitor>((_) => new ActivityMonitor());
            builder.Services.AddSingleton<LogEntryCollector>();
        }
    }
}