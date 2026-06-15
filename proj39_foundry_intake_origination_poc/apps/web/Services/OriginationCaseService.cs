using System.Collections.Concurrent;
using System.Text.Json;
using Proj39.IntakeOrigination.Web.Models;

namespace Proj39.IntakeOrigination.Web.Services;

/// <summary>
/// Orchestrates the intake/origination pipeline for an inbound email and persists the resulting
/// <see cref="OriginationCase"/> to a local JSON folder (App Service: /home/site/data). Keeps an
/// in-memory index for fast listing. Blob persistence can be layered later via <see cref="StorageOptions"/>.
/// </summary>
public sealed class OriginationCaseService
{
    private readonly IOriginationEngine _engine;
    private readonly StorageOptions _storage;
    private readonly ILogger<OriginationCaseService> _logger;
    private readonly string _dataDir;
    private readonly ConcurrentDictionary<string, OriginationCase> _cases = new();

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public OriginationCaseService(IOriginationEngine engine, StorageOptions storage, IWebHostEnvironment env, ILogger<OriginationCaseService> logger)
    {
        _engine = engine;
        _storage = storage;
        _logger = logger;

        _dataDir = Path.IsPathRooted(_storage.LocalDataFolder)
            ? _storage.LocalDataFolder
            : Path.Combine(env.ContentRootPath, _storage.LocalDataFolder);
        Directory.CreateDirectory(_dataDir);
        LoadExisting();
    }

    public async Task<OriginationCase> RunAsync(InboundEmail email, CancellationToken ct = default)
    {
        OriginationCase result;
        try
        {
            result = await _engine.ProcessAsync(email, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pipeline failed for email {EmailId}", email.Id);
            result = new OriginationCase { Email = email, Status = "failed", Engine = _engine.Name };
            result.AgentSteps.Add(new AgentStepLog { Agent = "Pipeline", Step = "error", Summary = $"Pipeline error: {ex.Message}" });
        }

        _cases[result.CaseId] = result;
        Persist(result);
        return result;
    }

    public IReadOnlyList<OriginationCase> List() => _cases.Values.OrderByDescending(c => c.CreatedUtc).ToList();

    public OriginationCase? Get(string caseId) => _cases.TryGetValue(caseId, out var c) ? c : null;

    public bool TryGetReportMarkdown(string caseId, out string markdown, out string fileName)
    {
        markdown = ""; fileName = $"origination-study-{caseId}.md";
        var c = Get(caseId);
        if (c is null || string.IsNullOrEmpty(c.Report.GeneratedMarkdown)) return false;
        markdown = c.Report.GeneratedMarkdown;
        return true;
    }

    private void Persist(OriginationCase c)
    {
        try
        {
            var path = Path.Combine(_dataDir, $"{c.CaseId}.json");
            File.WriteAllText(path, JsonSerializer.Serialize(c, JsonOpts));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist case {CaseId}", c.CaseId);
        }
    }

    private void LoadExisting()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(_dataDir, "*.json"))
            {
                try
                {
                    var c = JsonSerializer.Deserialize<OriginationCase>(File.ReadAllText(file), JsonOpts);
                    if (c is not null) _cases[c.CaseId] = c;
                }
                catch { /* skip malformed */ }
            }
            _logger.LogInformation("Loaded {Count} existing origination case(s).", _cases.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load existing cases from {Dir}", _dataDir);
        }
    }
}
