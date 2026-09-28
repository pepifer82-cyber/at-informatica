using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ATPrinterAgent;

public sealed class AgentWorker : BackgroundService
{
    private readonly ILogger<AgentWorker> _logger;
    private readonly AgentClient _client = new();

    public AgentWorker(ILogger<AgentWorker> logger) => _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AT Printer Agent servicio iniciado. Configuración: {ConfigPath}", ConfigStore.ConfigPath);
        while (!stoppingToken.IsCancellationRequested)
        {
            var config = ConfigStore.Load();
            if (string.IsNullOrWhiteSpace(config.Serial))
            {
                _logger.LogWarning("No hay serial configurado. El servicio esperará configuración.");
            }
            else
            {
                try
                {
                    var printers = PrinterDiscovery.GetInstalledPrinters();
                    if (config.EnableSnmp)
                        await SnmpPrinterMonitor.EnrichAsync(printers, config, stoppingToken);

                    var response = await _client.SyncAsync(config, printers, stoppingToken);
                    if (response.Ok)
                        _logger.LogInformation("Sincronización OK. Proyecto={Project} Impresoras={Count}", response.ProjectName, printers.Count);
                    else
                        _logger.LogWarning("Sincronización rechazada: {Error}", response.Error);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error durante la sincronización del agente");
                }
            }

            var delay = Math.Clamp(config.SyncIntervalSeconds, 30, 3600);
            try { await Task.Delay(TimeSpan.FromSeconds(delay), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        }
    }
}