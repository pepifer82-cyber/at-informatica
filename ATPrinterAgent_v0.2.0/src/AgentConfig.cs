using System.Text.Json;

namespace ATPrinterAgent;

public sealed class AgentConfig
{
    public string Serial { get; set; } = "";
    public string InstallId { get; set; } = Guid.NewGuid().ToString();
    public string ProjectName { get; set; } = "";
    public string AgentCode { get; set; } = "";
    public string Endpoint { get; set; } = "https://nxpdcwoypvlcgwuvjuro.supabase.co/functions/v1/printer-agent-sync";
    public int SyncIntervalSeconds { get; set; } = 60;
    public string SnmpCommunity { get; set; } = "public";
    public int SnmpTimeoutMs { get; set; } = 900;
    public bool EnableSnmp { get; set; } = true;
}

public static class ConfigStore
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "ATInformática", "ATPrinterAgent");

    private static readonly string FilePath = Path.Combine(Folder, "agent.json");

    public static string ConfigPath => FilePath;

    public static AgentConfig Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new AgentConfig();
            var json = File.ReadAllText(FilePath);
            var config = JsonSerializer.Deserialize<AgentConfig>(json) ?? new AgentConfig();
            if (string.IsNullOrWhiteSpace(config.InstallId)) config.InstallId = Guid.NewGuid().ToString();
            return config;
        }
        catch
        {
            return new AgentConfig();
        }
    }

    public static void Save(AgentConfig config)
    {
        Directory.CreateDirectory(Folder);
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(FilePath, json);
    }
}