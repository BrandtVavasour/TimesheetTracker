using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace TimesheetTracker.Web.Services;

/// <summary>
/// Content-hash versions for wwwroot assets, used as <c>?v=</c> cache busters.
/// Without these, browsers heuristically cache ts.js/app.css and keep running
/// stale copies after a deploy.
/// </summary>
public interface IAssetVersion
{
    /// <summary>A short content hash for the given wwwroot-relative asset.</summary>
    string For(string asset);
}

public sealed class AssetVersion(IWebHostEnvironment env) : IAssetVersion
{
    // Hash once per asset per app lifetime — content only changes with a deploy.
    private readonly ConcurrentDictionary<string, string> _cache = new();

    public string For(string asset) => _cache.GetOrAdd(asset, a =>
    {
        var file = env.WebRootFileProvider.GetFileInfo(a);
        if (!file.Exists) return "0";
        using var stream = file.CreateReadStream();
        return Convert.ToHexString(SHA256.HashData(stream))[..12].ToLowerInvariant();
    });
}
