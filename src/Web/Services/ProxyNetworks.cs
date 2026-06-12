using System.Net;

namespace TimesheetTracker.Web.Services;

/// <summary>
/// Resolves the set of networks whose <c>X-Forwarded-*</c> headers the app
/// should trust. The container only ever receives traffic from the reverse
/// proxy (cloudflared / nginx) over a private network, so trust is restricted
/// to private ranges by default — preventing a public-internet peer from
/// spoofing client IP or scheme. Override with the <c>FORWARDED_KNOWN_NETWORKS</c>
/// env var (comma-separated CIDRs or bare IPs) to pin the exact proxy.
/// </summary>
public static class ProxyNetworks
{
    private static readonly string[] PrivateDefaults =
    [
        "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "127.0.0.0/8", "::1/128",
    ];

    public static IReadOnlyList<IPNetwork> Resolve(string? configured)
    {
        var custom = Parse(configured);
        return custom.Count > 0 ? custom : Parse(string.Join(',', PrivateDefaults));
    }

    private static List<IPNetwork> Parse(string? csv)
    {
        var result = new List<IPNetwork>();
        if (string.IsNullOrWhiteSpace(csv)) return result;

        foreach (var raw in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (IPNetwork.TryParse(raw, out var net))
            {
                result.Add(net);
            }
            else if (IPAddress.TryParse(raw, out var ip))
            {
                // A bare address means "this exact host".
                result.Add(new IPNetwork(ip, ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? 128 : 32));
            }
        }
        return result;
    }
}
