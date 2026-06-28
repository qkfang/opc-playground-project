using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Proj45.RelayDesk.Web.Models;

namespace Proj45.RelayDesk.Web.Services;

/// <summary>
/// In-memory case journal with a best-effort JSON snapshot so processed cases survive restarts.
/// Thread-safe; newest-first listing. The snapshot path comes from Storage:DataDirectory.
/// </summary>
public sealed class CaseStore
{
    private readonly ConcurrentDictionary<string, EmailCase> _cases = new();
    private readonly ConcurrentQueue<string> _order = new();
    private readonly string _file;
    private readonly ILogger<CaseStore> _log;
    private readonly object _ioLock = new();

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = false };

    public CaseStore(IOptions<StorageOptions> storage, IWebHostEnvironment env, ILogger<CaseStore> log)
    {
        _log = log;
        var dir = storage.Value.DataDirectory;
        if (!Path.IsPathRooted(dir)) dir = Path.Combine(env.ContentRootPath, dir);
        try { Directory.CreateDirectory(dir); } catch { /* best-effort */ }
        _file = Path.Combine(dir, "relay-cases.json");
        Load();
    }

    public void Save(EmailCase c)
    {
        if (!_cases.ContainsKey(c.CaseId)) _order.Enqueue(c.CaseId);
        _cases[c.CaseId] = c;
        Persist();
    }

    public EmailCase? Get(string id) => _cases.TryGetValue(id, out var c) ? c : null;

    public IReadOnlyList<EmailCase> List() =>
        _order.Reverse().Select(id => _cases.TryGetValue(id, out var c) ? c : null)
              .Where(c => c is not null).Cast<EmailCase>().ToList();

    public void Clear()
    {
        _cases.Clear();
        while (_order.TryDequeue(out _)) { }
        Persist();
    }

    private void Persist()
    {
        try
        {
            lock (_ioLock)
                File.WriteAllText(_file, JsonSerializer.Serialize(List(), Json));
        }
        catch (Exception ex) { _log.LogDebug(ex, "Case journal persist skipped."); }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_file)) return;
            var items = JsonSerializer.Deserialize<List<EmailCase>>(File.ReadAllText(_file), Json);
            if (items is null) return;
            foreach (var c in items.AsEnumerable().Reverse()) // restore newest-first ordering
            {
                _cases[c.CaseId] = c;
                _order.Enqueue(c.CaseId);
            }
            _log.LogInformation("Restored {Count} cases from journal.", items.Count);
        }
        catch (Exception ex) { _log.LogDebug(ex, "Case journal load skipped."); }
    }
}
