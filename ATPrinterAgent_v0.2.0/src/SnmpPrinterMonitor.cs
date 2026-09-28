using System.Net;
using System.Net.Sockets;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;

namespace ATPrinterAgent;

public static class SnmpPrinterMonitor
{
    private const string SysName = "1.3.6.1.2.1.1.5.0";
    private const string LifeCount = "1.3.6.1.2.1.43.10.2.1.4";
    private const string SuppliesBase = "1.3.6.1.2.1.43.11.1.1";

    public static Task EnrichAsync(List<PrinterInfo> printers, AgentConfig config, CancellationToken cancellationToken = default)
        => Task.Run(() => Enrich(printers, config, cancellationToken), cancellationToken);

    private static void Enrich(List<PrinterInfo> printers, AgentConfig config, CancellationToken cancellationToken)
    {
        if (!config.EnableSnmp) return;

        foreach (var printer in printers.Where(p => p.ConnectionType == "ip" && IPAddress.TryParse(p.IpAddress, out _)))
        {
            if (cancellationToken.IsCancellationRequested) return;
            try
            {
                var result = Probe(printer.IpAddress!, config.SnmpCommunity, config.SnmpTimeoutMs);
                printer.SnmpReachable = result.Reachable;
                printer.SnmpName = result.Name;
                printer.PageCount = result.PageCount;
                printer.TonerBlackPercent = result.Black;
                printer.TonerCyanPercent = result.Cyan;
                printer.TonerMagentaPercent = result.Magenta;
                printer.TonerYellowPercent = result.Yellow;
                printer.SnmpError = result.Error;
            }
            catch (Exception ex)
            {
                printer.SnmpReachable = false;
                printer.SnmpError = ex.Message.Length > 240 ? ex.Message[..240] : ex.Message;
            }
        }
    }

    private static SnmpResult Probe(string ip, string community, int timeoutMs)
    {
        var endpoint = new IPEndPoint(IPAddress.Parse(ip), 161);
        foreach (var version in new[] { VersionCode.V2, VersionCode.V1 })
        {
            try
            {
                var vars = new List<Variable>
                {
                    new(new ObjectIdentifier(SysName)),
                    new(new ObjectIdentifier(LifeCount + ".1.1"))
                };
                var response = Messenger.Get(version, endpoint, new OctetString(community), vars, timeoutMs);
                string? name = null;
                long? pages = null;
                foreach (var v in response)
                {
                    if (v.Id.ToString() == SysName) name = v.Data.ToString();
                    else if (v.Id.ToString() == LifeCount + ".1.1") pages = ToLong(v.Data);
                }

                var supplies = new List<Variable>();
                try
                {
                    Messenger.Walk(version, endpoint, new OctetString(community), new ObjectIdentifier(SuppliesBase), supplies, timeoutMs, WalkMode.WithinSubtree);
                }
                catch { }

                decimal? black = null, cyan = null, magenta = null, yellow = null;
                ParseSupplies(supplies, ref black, ref cyan, ref magenta, ref yellow);
                return new SnmpResult(true, name, pages, black, cyan, magenta, yellow, null);
            }
            catch (Exception ex) when (ex is SnmpException or SocketException or TimeoutException)
            {
                if (version == VersionCode.V1)
                    return new SnmpResult(false, null, null, null, null, null, null, "Sin respuesta SNMP");
            }
        }
        return new SnmpResult(false, null, null, null, null, null, null, "Sin respuesta SNMP");
    }

    private static void ParseSupplies(List<Variable> supplies, ref decimal? black, ref decimal? cyan, ref decimal? magenta, ref decimal? yellow)
    {
        var map = new Dictionary<string, (long max, long level, string description)>(StringComparer.Ordinal);
        foreach (var v in supplies)
        {
            var oid = v.Id.ToString();
            var parts = oid.Split('.');
            if (parts.Length < 2) continue;
            var suffix = string.Join('.', parts.Skip(Math.Max(0, parts.Length - 2)));
            var data = v.Data?.ToString() ?? "";
            if (!long.TryParse(data, out var number)) continue;
            var column = parts[^3];
            if (column == "8") { if (!map.TryGetValue(suffix, out var item)) item = (0, 0, ""); item.max = number; map[suffix] = item; }
            else if (column == "9") { if (!map.TryGetValue(suffix, out var item)) item = (0, 0, ""); item.level = number; map[suffix] = item; }
        }

        foreach (var item in map.Values)
        {
            if (item.max <= 0 || item.level < 0) continue;
            var percent = Math.Clamp((decimal)item.level * 100m / item.max, 0m, 100m);
            if (black is null) black = percent;
            else if (cyan is null) cyan = percent;
            else if (magenta is null) magenta = percent;
            else if (yellow is null) yellow = percent;
        }
    }

    private static long? ToLong(ISnmpData data)
    {
        var s = data?.ToString();
        return long.TryParse(s, out var n) ? n : null;
    }

    private sealed record SnmpResult(bool Reachable, string? Name, long? PageCount, decimal? Black, decimal? Cyan, decimal? Magenta, decimal? Yellow, string? Error);
}