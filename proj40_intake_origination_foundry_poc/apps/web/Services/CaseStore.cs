using System.Collections.Concurrent;
using System.Text.Json;
using Proj40.IntakeOrigination.Web.Models;

namespace Proj40.IntakeOrigination.Web.Services;

/// <summary>
/// In-memory case journal with best-effort durable persistence to a local JSON file. Keeps the most
/// recent cases for the dashboard. On App Service the data folder maps to /home/site/data (writeable
/// and persisted) so the journal survives restarts; failures to persist are non-fatal.
/// </summary>
public sealed class CaseStore
{
    private readonly ConcurrentDictionary<string, IntakeCase> _cases = new();
    private readonly string _journalPath;
    private readonly ILogger<CaseStore> _logger;
    private readonly object _ioLock = new();
    private const int MaxCases = 200;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public CaseStore(StorageOptions storage, IWebHostEnvironment env, ILogger<CaseStore> logger)
    {
        _logger = logger;
        var folder = ResolveFolder(storage.LocalDataFolder, env);
        Directory.CreateDirectory(folder);
        _journalPath = Path.Combine(folder, "cases.json");
        Load();
    }

    private static string ResolveFolder(string configured, IWebHostEnvironment env)
    {
        if (string.IsNullOrWhiteSpace(configured)) configured = "App_Data";
        // Absolute path (e.g. /home/site/data on App Service) used as-is; otherwise relative to content root.
        return Path.IsPathRooted(configured) ? configured : Path.Combine(env.ContentRootPath, configured);
    }

    public IntakeCase Save(IntakeCase c)
    {
        _cases[c.CaseId] = c;
        Trim();
        Persist();
        return c;
    }

    public IntakeCase? Get(string caseId) => _cases.TryGetValue(caseId, out var c) ? c : null;

    public IReadOnlyList<IntakeCase> List() =>
        _cases.Values.OrderByDescending(c => c.CreatedUtc).ToList();

    public void Clear()
    {
        _cases.Clear();
        Persist();
    }

    // ---------------- persistence ----------------

    private void Trim()
    {
        if (_cases.Count <= MaxCases) return;
        foreach (var stale in _cases.Values.OrderByDescending(c => c.CreatedUtc).Skip(MaxCases).ToList())
            _cases.TryRemove(stale.CaseId, out _);
    }

    private void Persist()
    {
        try
        {
            lock (_ioLock)
            {
                var snapshot = _cases.Values.OrderByDescending(c => c.CreatedUtc).ToList();
                File.WriteAllText(_journalPath, JsonSerializer.Serialize(snapshot, JsonOpts));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not persist case journal to {Path} (non-fatal).", _journalPath);
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_journalPath)) return;
            var json = File.ReadAllText(_journalPath);
            var cases = JsonSerializer.Deserialize<List<IntakeCase>>(json, JsonOpts) ?? new();
            foreach (var c in cases) _cases[c.CaseId] = c;
            _logger.LogInformation("Loaded {Count} cases from journal.", _cases.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load case journal from {Path} (starting empty).", _journalPath);
        }
    }
}
