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
    private readonly string _dir;
    private readonly ConcurrentDictionary<string, ResearchCase> _cache = new();
    private readonly ILogger<ResearchCaseService> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public ResearchCaseService(IWebHostEnvironment env, ILogger<ResearchCaseService> logger)
    {
        _logger = logger;
        _dir = Path.Combine(env.ContentRootPath, "App_Data", "cases");
        Directory.CreateDirectory(_dir);
        LoadExisting();
    }

    private void LoadExisting()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(_dir, "*.json"))
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
