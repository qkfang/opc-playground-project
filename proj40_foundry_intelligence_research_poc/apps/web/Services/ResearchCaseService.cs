using System.Collections.Concurrent;
using System.Text.Json;
using Proj40.IntelligenceResearch.Web.Models;

namespace Proj40.IntelligenceResearch.Web.Services;

/// <summary>
/// Persists generated <see cref="ResearchCase"/> records. POC default: local JSON files under App_Data,
/// plus an in-memory index for fast listing. Swap-in point for Azure Blob storage in production.
/// </summary>
public sealed class ResearchCaseService
{
    private readonly string? _dir;          // null => in-memory only (storage unavailable)
    private readonly ConcurrentDictionary<string, ResearchCase> _cache = new();
    private readonly ILogger<ResearchCaseService> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public ResearchCaseService(IWebHostEnvironment env, StorageOptions storage, ILogger<ResearchCaseService> logger)
    {
        _logger = logger;
        _dir = ResolveWritableDir(env, storage);
        if (_dir is not null) LoadExisting();
    }

    /// <summary>
    /// Pick a writable folder for case JSON and ensure it exists. Order: configured Storage:LocalDataFolder,
    /// then HOME/site/data (writable on Linux App Service even under RUN_FROM_PACKAGE), then the content-root
    /// App_Data (writable when running locally). If none can be created, returns null so the service degrades
    /// to an in-memory store instead of throwing (which would 400 every pipeline/case request).
    /// </summary>
    private string? ResolveWritableDir(IWebHostEnvironment env, StorageOptions storage)
    {
        var home = Environment.GetEnvironmentVariable("HOME");
        var candidates = new[]
        {
            string.IsNullOrWhiteSpace(storage.LocalDataFolder) ? null : storage.LocalDataFolder,
            string.IsNullOrWhiteSpace(home) ? null : Path.Combine(home, "site", "data"),
            Path.Combine(env.ContentRootPath, "App_Data"),
        };

        foreach (var baseDir in candidates)
        {
            if (string.IsNullOrWhiteSpace(baseDir)) continue;
            try
            {
                var dir = Path.Combine(baseDir, "cases");
                Directory.CreateDirectory(dir);
                _logger.LogInformation("ResearchCaseService persisting cases to {Dir}.", dir);
                return dir;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Case folder {BaseDir} is not writable; trying next candidate.", baseDir);
            }
        }

        _logger.LogWarning("No writable case folder available; ResearchCaseService running in-memory only.");
        return null;
    }

    private void LoadExisting()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(_dir!, "*.json"))
            {
                var json = File.ReadAllText(file);
                var c = JsonSerializer.Deserialize<ResearchCase>(json, JsonOpts);
                if (c is not null) _cache[c.CaseId] = c;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load some persisted cases from {Dir}.", _dir);
        }
    }

    public async Task SaveAsync(ResearchCase c, CancellationToken ct = default)
    {
        _cache[c.CaseId] = c;
        if (_dir is null) return;           // in-memory only
        try
        {
            var path = Path.Combine(_dir, $"{c.CaseId}.json");
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(c, JsonOpts), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist case {CaseId}; kept in memory only.", c.CaseId);
        }
    }

    public ResearchCase? Get(string caseId) => _cache.TryGetValue(caseId, out var c) ? c : null;

    public IReadOnlyList<ResearchCase> List(int limit = 50) =>
        _cache.Values.OrderByDescending(c => c.CreatedUtc).Take(limit).ToList();
}
