using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ATPrinterAgent;

public static class ServiceRunner
{
    public static async Task RunAsync()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = "AT Printer Agent";
        });
        builder.Logging.AddEventLog(settings =>
        {
            settings.SourceName = "AT Printer Agent";
        });
        builder.Services.AddHostedService<AgentWorker>();
        using var host = builder.Build();
        await host.RunAsync();
    }
}