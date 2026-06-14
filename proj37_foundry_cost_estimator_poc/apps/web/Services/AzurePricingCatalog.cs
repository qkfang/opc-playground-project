using Proj37.CostEstimator.Web.Models;

namespace Proj37.CostEstimator.Web.Services;

/// <summary>
/// Curated reference pricing for common Azure services used in POC estimation.
///
/// IMPORTANT: These are *reference list prices* captured for a stable POC experience and are NOT a
/// live pricing feed. Region: Australia East, currency USD, Pay-As-You-Go. For production estimates,
/// replace this with the Azure Retail Prices API (https://prices.azure.com/api/retail/prices) or the
/// Azure Pricing Calculator export. Rates are intentionally rounded and conservative.
///
/// Rationale for hard-coded reference rates is documented in docs/solution.md and called out in the
/// generated Excel workbook so consumers never mistake POC numbers for a binding quote.
/// </summary>
public static class AzurePricingCatalog
{
    public const string Region = "australiaeast";
    public const string Currency = "USD";

    /// <summary>App Service plan monthly price per instance (Linux, 730 hrs).</summary>
    public static readonly IReadOnlyDictionary<string, decimal> AppServicePlanMonthly = new Dictionary<string, decimal>
    {
        ["B1"] = 13.14m,
        ["B2"] = 26.28m,
        ["S1"] = 73.00m,
        ["P0v3"] = 64.97m,
        ["P1v3"] = 129.94m,
        ["P2v3"] = 259.88m,
    };

    /// <summary>Azure OpenAI / Foundry model token prices (USD per 1K tokens).</summary>
    public sealed record ModelTokenRate(decimal InputPer1K, decimal OutputPer1K);

    public static readonly IReadOnlyDictionary<string, ModelTokenRate> ModelTokenRates = new Dictionary<string, ModelTokenRate>(StringComparer.OrdinalIgnoreCase)
    {
        ["gpt-4o"] = new(0.0050m, 0.0150m),
        ["gpt-4o-mini"] = new(0.000165m, 0.00066m),
        ["gpt-5.4"] = new(0.0050m, 0.0150m),   // reference; treated like a flagship GPT tier
    };

    public static ModelTokenRate GetModelRate(string deployment)
        => ModelTokenRates.TryGetValue(deployment, out var r) ? r : ModelTokenRates["gpt-4o"];

    // --- Flat per-unit reference rates (USD) ---

    /// <summary>Azure AI Search Basic tier, per month.</summary>
    public const decimal AiSearchBasicMonthly = 75.00m;

    /// <summary>Azure AI Search Standard S1, per month.</summary>
    public const decimal AiSearchStandardMonthly = 250.00m;

    /// <summary>Blob storage (Hot LRS) per GB-month.</summary>
    public const decimal BlobHotPerGbMonth = 0.021m;

    /// <summary>Azure SQL Database, General Purpose serverless, per vCore-month (approx if always-on).</summary>
    public const decimal SqlGpServerlessVCoreMonth = 130.00m;

    /// <summary>Azure SQL Database Basic (5 DTU), per month.</summary>
    public const decimal SqlBasicMonthly = 4.90m;

    /// <summary>Cosmos DB serverless, per million RUs.</summary>
    public const decimal CosmosServerlessPerMillionRu = 0.28m;

    /// <summary>Log Analytics ingestion per GB.</summary>
    public const decimal LogAnalyticsPerGb = 2.76m;

    /// <summary>Application Insights is billed via Log Analytics ingestion (same meter).</summary>

    /// <summary>Key Vault operations, per 10K operations.</summary>
    public const decimal KeyVaultPer10KOps = 0.03m;

    /// <summary>Standard public IP / outbound data transfer, per GB (first tier).</summary>
    public const decimal EgressPerGb = 0.12m;

    /// <summary>Azure Functions consumption — per million executions (after free grant).</summary>
    public const decimal FunctionsPerMillionExec = 0.20m;
}
