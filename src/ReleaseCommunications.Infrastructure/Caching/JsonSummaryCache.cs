using System.Text.Json;
using ReleaseCommunications.Core.Abstractions;
using ReleaseCommunications.Core.Models;

namespace ReleaseCommunications.Infrastructure.Caching;

public sealed class JsonSummaryCache : ISummaryCache, IDisposable
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public JsonSummaryCache(string? baseDirectory = null)
    {
        var root = baseDirectory ??
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ReleaseCommunications");
        _path = Path.Combine(root, "summary-cache.json");
    }

    public async Task<ArticleSummary?> TryGetAsync(string key, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var cache = await ReadAsync(cancellationToken);
            return cache.TryGetValue(key, out var entry) ? entry.Summary : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetAsync(string key, ArticleSummary summary, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var cache = await ReadAsync(cancellationToken);
            cache[key] = new CacheEntry(summary, DateTimeOffset.UtcNow);
            await WriteAsync(cache, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task PruneAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var threshold = DateTimeOffset.UtcNow.AddDays(-settings.CacheRetentionDays);
            var cache = await ReadAsync(cancellationToken);
            var retained = cache
                .Where(pair => pair.Value.StoredAt >= threshold)
                .OrderByDescending(pair => pair.Value.StoredAt)
                .Take(settings.MaximumCacheEntries)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            await WriteAsync(retained, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<Dictionary<string, CacheEntry>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return new Dictionary<string, CacheEntry>(StringComparer.Ordinal);
        }

        try
        {
            await using var stream = File.OpenRead(_path);
            return await JsonSerializer.DeserializeAsync<Dictionary<string, CacheEntry>>(
                stream,
                _jsonOptions,
                cancellationToken) ?? new Dictionary<string, CacheEntry>(StringComparer.Ordinal);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                "Summary cache is corrupted. Delete summary-cache.json to rebuild it.",
                ex);
        }
    }

    private async Task WriteAsync(
        Dictionary<string, CacheEntry> cache,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporaryPath = _path + ".tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, cache, _jsonOptions, cancellationToken);
        }

        File.Move(temporaryPath, _path, overwrite: true);
    }

    private sealed record CacheEntry(ArticleSummary Summary, DateTimeOffset StoredAt);

    public void Dispose()
    {
        _gate.Dispose();
        GC.SuppressFinalize(this);
    }
}
