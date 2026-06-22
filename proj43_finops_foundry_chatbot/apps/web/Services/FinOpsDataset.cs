using Proj43.FinOps.Web.Models;

namespace Proj43.FinOps.Web.Services;

/// <summary>
/// Deterministic, seeded FinOps dataset standing in for governed Microsoft Fabric / OneLake cost data.
/// Generates ~12 months of daily amortised cost across subscriptions, services, resource groups, regions,
/// environments and FinOps tags (costCenter/team). Seeded RNG => identical data every run (test-stable).
///
/// In live mode the same shape of data is fetched from Fabric (via the Fabric data agent tool or an MCP
/// server); this offline dataset guarantees the chatbot is always demonstrable and unit-testable.
/// </summary>
public sealed class FinOpsDataset
{
    public IReadOnlyList<CostRecord> Records { get; }
    public DateOnly StartDate { get; }
    public DateOnly EndDate { get; }
    public string Currency => "USD";

    private static readonly string[] Subscriptions = { "Platform-Prod", "Data-Prod", "Apps-Prod", "Sandbox-NonProd" };
    private static readonly string[] Regions = { "australiaeast", "eastus", "westeurope" };

    // service -> (baseDailyCost, monthlyGrowth%, commitmentEligible)
    private static readonly (string Service, double Base, double Growth, bool Committable)[] Services =
    {
        ("Azure App Service",        320, 0.015, true),
        ("Azure SQL Database",       210, 0.030, true),
        ("Azure Kubernetes Service", 540, 0.025, true),
        ("Azure Storage",            120, 0.010, false),
        ("Azure OpenAI / Foundry",    90, 0.090, false),
        ("Azure Virtual Machines",   480, 0.005, true),
        ("Azure Monitor",             70, 0.020, false),
        ("Azure Cosmos DB",          160, 0.035, true),
        ("Azure Data Factory",        85, 0.015, false),
        ("Azure Front Door",          55, 0.010, false),
    };

    private static readonly (string Rg, string CostCenter, string Team)[] ResourceGroups =
    {
        ("rg-web-prod",      "CC-1001-Digital",   "Web Platform"),
        ("rg-data-prod",     "CC-2002-Analytics", "Data Platform"),
        ("rg-ai-prod",       "CC-3003-AI",        "AI & ML"),
        ("rg-core-prod",     "CC-1000-Shared",    "Cloud Platform"),
        ("rg-sandbox",       "CC-9009-RnD",       "Innovation"),
    };

    public FinOpsDataset(DateOnly? today = null, int seed = 43)
    {
        var end = today ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var start = end.AddMonths(-12).AddDays(1);
        StartDate = start;
        EndDate = end;

        var rng = new Random(seed);
        var records = new List<CostRecord>(capacity: 20_000);

        for (var day = start; day <= end; day = day.AddDays(1))
        {
            int monthIndex = (day.Year - start.Year) * 12 + (day.Month - start.Month);
            // Weekend dip on prod compute; AI/data steadier.
            double weekendFactor = day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? 0.82 : 1.0;

            foreach (var (svc, baseCost, growth, committable) in Services)
            {
                // Spread each service across 1-2 resource groups deterministically.
                var rgList = PickResourceGroups(svc);
                foreach (var rg in rgList)
                {
                    bool nonProd = rg.Rg.Contains("sandbox", StringComparison.OrdinalIgnoreCase);
                    var sub = nonProd ? "Sandbox-NonProd" : SubscriptionForRg(rg.Rg);
                    var region = Regions[Math.Abs(svc.GetHashCode() + rg.Rg.GetHashCode()) % Regions.Length];

                    double growthMult = Math.Pow(1.0 + growth, monthIndex);
                    double envMult = nonProd ? 0.18 : 1.0;
                    double noise = 0.90 + rng.NextDouble() * 0.20;        // +/-10%
                    double daily = baseCost * growthMult * weekendFactor * envMult * noise / rgList.Length;

                    // Inject a deliberate anomaly: Azure SQL in the most recent full month spikes.
                    if (svc == "Azure SQL Database" && monthIndex == 11 && !nonProd)
                        daily *= 1.85;
                    // Azure OpenAI ramps hard in last 2 months (AI adoption).
                    if (svc.StartsWith("Azure OpenAI") && monthIndex >= 10)
                        daily *= 1.4;

                    decimal cost = Math.Round((decimal)daily, 2);
                    decimal committed = committable && !nonProd ? Math.Round(cost * 0.62m, 2) : 0m;

                    records.Add(new CostRecord
                    {
                        Date = day,
                        Subscription = sub,
                        ResourceGroup = rg.Rg,
                        Service = svc,
                        Region = region,
                        Environment = nonProd ? "non-prod" : "prod",
                        CostCenter = rg.CostCenter,
                        Team = rg.Team,
                        Cost = cost,
                        CommittedCost = committed,
                        UsageQuantity = Math.Round((decimal)(daily / Math.Max(1.0, baseCost / 24.0)), 2),
                        UsageUnit = UnitFor(svc),
                    });
                }
            }
        }

        Records = records;
    }

    private static (string Rg, string CostCenter, string Team)[] PickResourceGroups(string service) => service switch
    {
        "Azure App Service" or "Azure Front Door" => new[] { ResourceGroups[0], ResourceGroups[3] },
        "Azure SQL Database" or "Azure Cosmos DB" or "Azure Data Factory" => new[] { ResourceGroups[1], ResourceGroups[3] },
        "Azure OpenAI / Foundry" => new[] { ResourceGroups[2] },
        "Azure Kubernetes Service" or "Azure Virtual Machines" => new[] { ResourceGroups[3], ResourceGroups[0] },
        "Azure Storage" or "Azure Monitor" => new[] { ResourceGroups[3], ResourceGroups[1], ResourceGroups[4] },
        _ => new[] { ResourceGroups[3] },
    };

    private static string SubscriptionForRg(string rg) => rg switch
    {
        "rg-web-prod" => "Apps-Prod",
        "rg-data-prod" => "Data-Prod",
        "rg-ai-prod" => "Data-Prod",
        "rg-core-prod" => "Platform-Prod",
        _ => "Platform-Prod",
    };

    private static string UnitFor(string svc) => svc switch
    {
        "Azure App Service" => "instance-hours",
        "Azure SQL Database" => "vCore-hours",
        "Azure Kubernetes Service" => "node-hours",
        "Azure Storage" => "GB",
        "Azure OpenAI / Foundry" => "1K tokens",
        "Azure Virtual Machines" => "vCPU-hours",
        "Azure Monitor" => "GB ingested",
        "Azure Cosmos DB" => "RU/s-hours",
        "Azure Data Factory" => "pipeline-runs",
        "Azure Front Door" => "GB egress",
        _ => "units",
    };
}
