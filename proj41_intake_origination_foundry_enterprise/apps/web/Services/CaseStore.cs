using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Proj41.Underwriting.Web.Models;

namespace Proj41.Underwriting.Web.Services;

/// <summary>
/// In-memory submission case store with a durable JSON journal so cases survive restarts.
/// On Azure App Service the data directory maps to the persisted /home/site/data share.
/// </summary>
public sealed class CaseStore
{
    private readonly ConcurrentDictionary<string, SubmissionCase> _cases = new();
    private readonly string _journalPath;
    private readonly object _ioLock = new();
    private readonly ILogger<CaseStore> _log;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    public CaseStore(IOptions<StorageOptions> options, ILogger<CaseStore> log)
    {
        _log = log;
        var dir = options.Value.DataDirectory;
        if (!Path.IsPathRooted(dir))
            dir = Path.Combine(AppContext.BaseDirectory, dir);
        try { Directory.CreateDirectory(dir); }
        catch (Exception ex) { _log.LogWarning(ex, "Could not create data directory {Dir}; cases will be memory-only.", dir); }
        _journalPath = Path.Combine(dir, "cases.json");
        Load();
    }

    public void Save(SubmissionCase c)
    {
        _cases[c.CaseId] = c;
        Persist();
    }

    public SubmissionCase? Get(string id) => _cases.TryGetValue(id, out var c) ? c : null;

    public IReadOnlyList<SubmissionCase> List() =>
        _cases.Values.OrderByDescending(c => c.CreatedUtc).ToList();

    public void Clear()
    {
        _cases.Clear();
        Persist();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_journalPath)) return;
            var json = File.ReadAllText(_journalPath);
            var items = JsonSerializer.Deserialize<List<SubmissionCase>>(json, Json);
            if (items is null) return;
            foreach (var c in items) _cases[c.CaseId] = c;
            _log.LogInformation("Loaded {Count} cases from journal.", _cases.Count);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to load case journal; starting empty.");
        }
    }

    private void Persist()
    {
        try
        {
            lock (_ioLock)
            {
                var json = JsonSerializer.Serialize(_cases.Values.OrderByDescending(c => c.CreatedUtc), Json);
                File.WriteAllText(_journalPath, json);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to persist case journal (continuing in-memory).");
        }
    }
}
