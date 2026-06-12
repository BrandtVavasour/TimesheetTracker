using System.Net;
using Microsoft.AspNetCore.Http;
using TimesheetTracker.Web.Services;

namespace Web.Tests;

/// <summary>
/// The rate limiter must bucket by the real client IP. Behind the Cloudflare
/// tunnel the canonical header is CF-Connecting-IP; relying on Connection.
/// RemoteIpAddress would lump every user into the single cloudflared IP and
/// 429 legitimate traffic.
/// </summary>
[TestFixture]
public class ClientIpTests
{
    [Test]
    public void PrefersCfConnectingIp()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["CF-Connecting-IP"] = "203.0.113.7";
        ctx.Request.Headers["X-Forwarded-For"] = "198.51.100.1";
        ctx.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.4");

        ClientIp.For(ctx).Should().Be("203.0.113.7");
    }

    [Test]
    public void FallsBackToFirstForwardedFor()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Forwarded-For"] = "198.51.100.1, 192.168.1.4";
        ctx.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.4");

        ClientIp.For(ctx).Should().Be("198.51.100.1");
    }

    [Test]
    public void FallsBackToRemoteIp()
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.5");

        ClientIp.For(ctx).Should().Be("10.0.0.5");
    }

    [Test]
    public void DifferentClients_GetDifferentKeys()
    {
        var a = new DefaultHttpContext();
        a.Request.Headers["CF-Connecting-IP"] = "203.0.113.1";
        var b = new DefaultHttpContext();
        b.Request.Headers["CF-Connecting-IP"] = "203.0.113.2";

        ClientIp.For(a).Should().NotBe(ClientIp.For(b));
    }

    [Test]
    public void Unknown_WhenNothingAvailable() =>
        ClientIp.For(new DefaultHttpContext()).Should().Be("unknown");
}
