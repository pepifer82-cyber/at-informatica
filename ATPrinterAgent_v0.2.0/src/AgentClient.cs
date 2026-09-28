using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ATPrinterAgent;

public sealed class AgentPayload
{
    [JsonPropertyName("serial")] public string Serial { get; set; } = "";
    [JsonPropertyName("install_id")] public string InstallId { get; set; } = "";
    [JsonPropertyName("machine_name")] public string? MachineName { get; set; }
    [JsonPropertyName("windows_user")] public string? WindowsUser { get; set; }
    [JsonPropertyName("app_version")] public string? AppVersion { get; set; }
    [JsonPropertyName("printers")] public List<PrinterInfo> Printers { get; set; } = [];
}

public sealed class SyncResponse
{
    [JsonPropertyName("ok")] public bool Ok { get; set; }
    [JsonPropertyName("project_name")] public string? ProjectName { get; set; }
    [JsonPropertyName("agent_code")] public string? AgentCode { get; set; }
    [JsonPropertyName("printers_received")] public int PrintersReceived { get; set; }
    [JsonPropertyName("error")] public string? Error { get; set; }
}

public sealed class AgentClient
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public async Task<SyncResponse> SyncAsync(AgentConfig config, List<PrinterInfo> printers, CancellationToken cancellationToken = default)
    {
        var payload = new AgentPayload
        {
            Serial = config.Serial.Trim(),
            InstallId = config.InstallId,
            MachineName = Environment.MachineName,
            WindowsUser = Environment.UserName,
            AppVersion = Application.ProductVersion,
            Printers = printers
        };

        using var response = await _http.PostAsJsonAsync(config.Endpoint, payload, cancellationToken);
        SyncResponse? body = null;
        try { body = await response.Content.ReadFromJsonAsync<SyncResponse>(cancellationToken: cancellationToken); } catch { }

        if (body != null) return body;
        return new SyncResponse { Ok = false, Error = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}" };
    }
}