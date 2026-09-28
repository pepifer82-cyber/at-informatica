using System.Management;
using System.Net;
using System.Text.Json.Serialization;

namespace ATPrinterAgent;

public sealed class PrinterInfo
{
    [JsonPropertyName("printer_key")] public string PrinterKey { get; set; } = "";
    [JsonPropertyName("display_name")] public string DisplayName { get; set; } = "";
    [JsonPropertyName("manufacturer")] public string? Manufacturer { get; set; }
    [JsonPropertyName("model")] public string? Model { get; set; }
    [JsonPropertyName("connection_type")] public string ConnectionType { get; set; } = "unknown";
    [JsonPropertyName("ip_address")] public string? IpAddress { get; set; }
    [JsonPropertyName("windows_port")] public string? WindowsPort { get; set; }
    [JsonPropertyName("usb_port")] public string? UsbPort { get; set; }
    [JsonPropertyName("windows_printer_name")] public string? WindowsPrinterName { get; set; }
    [JsonPropertyName("serial_number")] public string? SerialNumber { get; set; }
    [JsonPropertyName("last_status")] public string LastStatus { get; set; } = "unknown";
    [JsonPropertyName("snmp_reachable")] public bool SnmpReachable { get; set; }
    [JsonPropertyName("snmp_name")] public string? SnmpName { get; set; }
    [JsonPropertyName("page_count")] public long? PageCount { get; set; }
    [JsonPropertyName("toner_black_percent")] public decimal? TonerBlackPercent { get; set; }
    [JsonPropertyName("toner_cyan_percent")] public decimal? TonerCyanPercent { get; set; }
    [JsonPropertyName("toner_magenta_percent")] public decimal? TonerMagentaPercent { get; set; }
    [JsonPropertyName("toner_yellow_percent")] public decimal? TonerYellowPercent { get; set; }
    [JsonPropertyName("snmp_error")] public string? SnmpError { get; set; }
}

public static class PrinterDiscovery
{
    public static List<PrinterInfo> GetInstalledPrinters()
    {
        var result = new List<PrinterInfo>();
        using var searcher = new ManagementObjectSearcher("SELECT Name, DeviceID, DriverName, PortName, PrinterStatus, WorkOffline, Status FROM Win32_Printer");
        using var collection = searcher.Get();

        foreach (ManagementObject printer in collection)
        {
            var name = Convert.ToString(printer["Name"]) ?? "";
            var deviceId = Convert.ToString(printer["DeviceID"]) ?? "";
            var driver = Convert.ToString(printer["DriverName"]);
            var port = Convert.ToString(printer["PortName"]);
            var status = GetStatus(printer);
            if (string.IsNullOrWhiteSpace(name)) continue;

            string? ip = null;
            string? usb = null;
            var type = "unknown";

            if (!string.IsNullOrWhiteSpace(port) && TryExtractIp(port, out var extractedIp))
            {
                ip = extractedIp;
                type = "ip";
            }
            else if (!string.IsNullOrWhiteSpace(port) && port.StartsWith("USB", StringComparison.OrdinalIgnoreCase))
            {
                usb = port;
                type = "usb";
            }

            var keySeed = $"{name}|{deviceId}|{port}";
            var key = "win:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(keySeed))).Substring(0, 16);

            result.Add(new PrinterInfo
            {
                PrinterKey = key,
                DisplayName = name,
                Model = driver,
                Manufacturer = GuessManufacturer(name, driver),
                ConnectionType = type,
                IpAddress = ip,
                WindowsPort = port,
                UsbPort = usb,
                WindowsPrinterName = name,
                LastStatus = status
            });
        }

        return result.OrderBy(p => p.DisplayName).ToList();
    }

    private static string GetStatus(ManagementObject printer)
    {
        try
        {
            var offline = Convert.ToBoolean(printer["WorkOffline"] ?? false);
            if (offline) return "offline";
            var status = Convert.ToInt32(printer["PrinterStatus"] ?? 0);
            return status switch
            {
                3 => "printing",
                4 => "offline",
                5 => "unknown",
                6 => "waiting",
                7 => "power_save",
                _ => "online"
            };
        }
        catch { return "unknown"; }
    }

    private static bool TryExtractIp(string port, out string? ip)
    {
        ip = null;
        var value = port.Trim();
        if (IPAddress.TryParse(value, out var parsed))
        {
            ip = parsed.ToString();
            return true;
        }
        if (value.StartsWith("IP_", StringComparison.OrdinalIgnoreCase))
        {
            var candidate = value[3..];
            if (IPAddress.TryParse(candidate, out parsed))
            {
                ip = parsed.ToString();
                return true;
            }
        }
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, HostAddress FROM Win32_TCPIPPrinterPort");
            using var collection = searcher.Get();
            foreach (ManagementObject tcpPort in collection)
            {
                var name = Convert.ToString(tcpPort["Name"]);
                var host = Convert.ToString(tcpPort["HostAddress"]);
                if (!string.IsNullOrWhiteSpace(name) && name.Equals(value, StringComparison.OrdinalIgnoreCase) && IPAddress.TryParse(host, out _))
                {
                    ip = host;
                    return true;
                }
            }
        }
        catch { }
        return false;
    }

    private static string? GuessManufacturer(string printerName, string? driver)
    {
        var text = $"{printerName} {driver}".ToLowerInvariant();
        string[] makers = ["hp", "hewlett", "epson", "brother", "canon", "ricoh", "kyocera", "xerox", "lexmark", "samsung", "pantum", "fuji", "sharp", "konica", "oki"];
        foreach (var maker in makers)
        {
            if (text.Contains(maker)) return maker.ToUpperInvariant();
        }
        return null;
    }
}