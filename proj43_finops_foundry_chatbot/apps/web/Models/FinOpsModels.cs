using System.Text.Json.Serialization;

namespace Proj43.FinOps.Web.Models;

/// <summary>One day of cost for one service within one subscription/resource group, with FinOps tags.</summary>
public sealed class CostRecord
{
    public DateOnly Date { get; set; }
    public string Subscription { get; set; } = "";
    public string ResourceGroup { get; set; } = "";
    public string Service { get; set; } = "";          // e.g. "Azure App Service", "Azure SQL Database"
    public string Region { get; set; } = "";
    public string Environment { get; set; } = "";       // prod | non-prod
    public string CostCenter { get; set; } = "";        // showback dimension
    public string Team { get; set; } = "";
    public decimal Cost { get; set; }                   // amortised USD for the day
    public decimal UsageQuantity { get; set; }
    public string UsageUnit { get; set; } = "";
    /// <summary>Portion of <see cref="Cost"/> covered by a reservation / savings plan.</summary>
    public decimal CommittedCost { get; set; }
}

/// <summary>Aggregated spend for a named dimension value over a window.</summary>
public sealed class SpendBucket
{
    public string Name { get; set; } = "";
    public decimal Cost { get; set; }
    public decimal Share { get; set; }                  // fraction of the total (0..1)
}

/// <summary>A month-over-month cost anomaly.</summary>
public sealed class CostAnomaly
{
    public string Dimension { get; set; } = "";         // e.g. "Service: Azure SQL Database"
    public string Month { get; set; } = "";             // yyyy-MM
    public decimal PreviousCost { get; set; }
    public decimal CurrentCost { get; set; }
    public decimal ChangePercent { get; set; }
    public string Severity { get; set; } = "";          // info | warning | critical
}

/// <summary>An optimisation / savings recommendation.</summary>
public sealed class Recommendation
{
    public string Id { get; set; } = "";
    public string Category { get; set; } = "";          // Rightsizing | Commitment | Idle | Storage | Governance
    public string Title { get; set; } = "";
    public string Detail { get; set; } = "";
    public decimal EstimatedMonthlySavings { get; set; }
    public string Effort { get; set; } = "";            // Low | Medium | High
}

/// <summary>Commitment (reservation/savings-plan) coverage summary.</summary>
public sealed class CoverageSummary
{
    public decimal TotalCost { get; set; }
    public decimal CommittedCost { get; set; }
    public decimal OnDemandCost { get; set; }
    public decimal CoveragePercent { get; set; }
}

// ---------------- Chat DTOs ----------------

public sealed class ChatTurn
{
    [JsonPropertyName("role")] public string Role { get; set; } = "user";   // user | assistant
    [JsonPropertyName("content")] public string Content { get; set; } = "";
    [JsonPropertyName("ts")] public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ChatRequest
{
    [JsonPropertyName("conversationId")] public string? ConversationId { get; set; }
    [JsonPropertyName("message")] public string Message { get; set; } = "";
}

public sealed class ChatResponse
{
    [JsonPropertyName("conversationId")] public string ConversationId { get; set; } = "";
    [JsonPropertyName("reply")] public string Reply { get; set; } = "";
    [JsonPropertyName("replyHtml")] public string ReplyHtml { get; set; } = "";
    [JsonPropertyName("engine")] public string Engine { get; set; } = "";
    [JsonPropertyName("tool")] public string? Tool { get; set; }            // e.g. "fabric-data-agent" | "offline-finops"
    [JsonPropertyName("intent")] public string? Intent { get; set; }
}

/// <summary>A streamed SSE event for the chat UI.</summary>
public sealed class ChatStreamEvent
{
    public string Type { get; set; } = "token";        // meta | status | token | done | error
    public string? Data { get; set; }
    public string? ConversationId { get; set; }
    public string? Engine { get; set; }
    public string? Tool { get; set; }
}
