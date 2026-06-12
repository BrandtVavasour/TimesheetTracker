using System.Net;
using TimesheetTracker.Web.Services;

namespace Web.Tests;

/// <summary>
/// Trusted-proxy networks for ForwardedHeaders. Previously the app trusted
/// X-Forwarded-* from ANY peer (KnownIPNetworks.Clear()); this restricts trust
/// so a public-internet peer can't spoof client IP / scheme.
/// </summary>
[TestFixture]
public class ProxyNetworksTests
{
    private static bool Covers(IReadOnlyList<IPNetwork> nets, string ip) =>
        nets.Any(n => n.Contains(IPAddress.Parse(ip)));

    [Test]
    public void Default_TrustsPrivateRanges_NotPublic()
    {
        var nets = ProxyNetworks.Resolve(null);
        Covers(nets, "192.168.1.4").Should().BeTrue();   // cloudflared on the macvlan
        Covers(nets, "10.1.2.3").Should().BeTrue();
        Covers(nets, "172.18.0.5").Should().BeTrue();    // docker bridge
        Covers(nets, "127.0.0.1").Should().BeTrue();
        Covers(nets, "8.8.8.8").Should().BeFalse();      // public IP must NOT be trusted
        Covers(nets, "203.0.113.7").Should().BeFalse();
    }

    [Test]
    public void CustomCidr_ReplacesDefaults()
    {
        var nets = ProxyNetworks.Resolve("192.168.1.0/24");
        Covers(nets, "192.168.1.13").Should().BeTrue();
        Covers(nets, "192.168.7.1").Should().BeFalse();
        Covers(nets, "10.0.0.1").Should().BeFalse();     // defaults no longer apply
    }

    [Test]
    public void MultipleCidrs_AllParsed()
    {
        var nets = ProxyNetworks.Resolve("192.168.1.0/24, 172.20.0.0/16");
        Covers(nets, "192.168.1.4").Should().BeTrue();
        Covers(nets, "172.20.5.5").Should().BeTrue();
    }

    [Test]
    public void BareIp_TreatedAsSingleHost()
    {
        var nets = ProxyNetworks.Resolve("192.168.1.4");
        Covers(nets, "192.168.1.4").Should().BeTrue();
        Covers(nets, "192.168.1.5").Should().BeFalse();
    }

    [Test]
    public void AllInvalid_FallsBackToDefaults()
    {
        var nets = ProxyNetworks.Resolve("garbage, , not-an-ip");
        Covers(nets, "10.0.0.1").Should().BeTrue();
        Covers(nets, "8.8.8.8").Should().BeFalse();
    }
}
