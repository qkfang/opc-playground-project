using System.Collections.Concurrent;
using Proj45.RelayDesk.Web.Models;

namespace Proj45.RelayDesk.Web.Services;

/// <summary>
/// Holds cases whose intent was uncertain/ambiguous (below the configured confidence threshold or
/// flagged sensitive). The Intent page lists pending items and lets a reviewer resolve them.
/// </summary>
public sealed class HumanReviewQueue
{
    private readonly ConcurrentDictionary<string, HumanReviewItem> _items = new();

    public void Enqueue(HumanReviewItem item) => _items[item.CaseId] = item;

    public IReadOnlyList<HumanReviewItem> Pending() =>
        _items.Values.Where(i => i.Status == "pending").OrderBy(i => i.QueuedUtc).ToList();

    public IReadOnlyList<HumanReviewItem> All() =>
        _items.Values.OrderByDescending(i => i.QueuedUtc).ToList();

    public HumanReviewItem? Get(string caseId) => _items.TryGetValue(caseId, out var i) ? i : null;

    public HumanReviewItem? Resolve(string caseId, string resolvedIntent, string resolvedBy)
    {
        if (!_items.TryGetValue(caseId, out var i)) return null;
        i.Status = "resolved";
        i.ResolvedIntent = resolvedIntent;
        i.ResolvedBy = string.IsNullOrWhiteSpace(resolvedBy) ? "reviewer" : resolvedBy;
        i.ResolvedUtc = DateTimeOffset.UtcNow;
        return i;
    }

    public void Clear() => _items.Clear();
}
