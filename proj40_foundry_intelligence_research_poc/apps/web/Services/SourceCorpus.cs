using System.Text.Json;
using System.Text.Json.Serialization;
using Proj40.IntelligenceResearch.Web.Models;

namespace Proj40.IntelligenceResearch.Web.Services;

/// <summary>
/// A mocked corpus of internal + external sources. Records are keyed by entity/topic keywords; the
/// pipeline pulls matching records for the entities extracted from the customer email + document.
/// This is the seam where real connectors (CRM, news, filings, search) would plug in unchanged.
/// </summary>
public sealed class SourceCorpus
{
    private sealed class Raw
    {
        [JsonPropertyName("internal")] public List<RawRecord> Internal { get; set; } = new();
        [JsonPropertyName("external")] public List<RawRecord> External { get; set; } = new();
    }

    private sealed class RawRecord
    {
        public List<string> Keys { get; set; } = new();
        public string SourceName { get; set; } = "";
        public string Title { get; set; } = "";
        public string Snippet { get; set; } = "";
        public string? Url { get; set; }
        public DateTime? Dated { get; set; }
        public string Relevance { get; set; } = "Medium";
    }

    private readonly List<(RawRecord rec, string type)> _records = new();

    public SourceCorpus(IWebHostEnvironment env, ILogger<SourceCorpus> logger)
    {
        var path = Path.Combine(env.ContentRootPath, "Data", "source-corpus.json");
        try
        {
            var json = File.ReadAllText(path);
            var raw = JsonSerializer.Deserialize<Raw>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? new Raw();
            foreach (var r in raw.Internal) _records.Add((r, "Internal"));
            foreach (var r in raw.External) _records.Add((r, "External"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load source corpus from {Path}; source pulls will be empty.", path);
        }
    }

    /// <summary>
    /// Pull source hits whose keys match any of the supplied entities (case-insensitive substring either
    /// direction). Results are de-duplicated by (source, title) and ordered Internal-first then by relevance.
    /// </summary>
    public List<SourceHit> Pull(IEnumerable<string> entities)
    {
        var ents = entities
            .Where(e => !string.IsNullOrWhiteSpace(e) && e.Trim().Length >= 3)
            .Select(e => e.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var hits = new List<SourceHit>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (rec, type) in _records)
        {
            var matchedEntity = ents.FirstOrDefault(e => rec.Keys.Any(k => KeyMatches(k, e)));
            if (matchedEntity is null) continue;

            var dedupe = $"{rec.SourceName}|{rec.Title}";
            if (!seen.Add(dedupe)) continue;

            hits.Add(new SourceHit
            {
                Entity = matchedEntity,
                SourceName = rec.SourceName,
                SourceType = type,
                Title = rec.Title,
                Snippet = rec.Snippet,
                Url = rec.Url,
                Dated = rec.Dated,
                Relevance = rec.Relevance
            });
        }

        return hits
            .OrderBy(h => h.SourceType == "Internal" ? 0 : 1)
            .ThenBy(h => h.Relevance switch { "High" => 0, "Medium" => 1, _ => 2 })
            .ToList();
    }

    private static bool KeyMatches(string key, string entity)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        return key.Contains(entity, StringComparison.OrdinalIgnoreCase)
            || entity.Contains(key, StringComparison.OrdinalIgnoreCase);
    }
}
