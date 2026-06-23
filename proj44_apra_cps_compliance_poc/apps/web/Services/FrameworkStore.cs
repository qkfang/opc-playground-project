using Proj44.Compliance.Web.Models;

namespace Proj44.Compliance.Web.Services;

/// <summary>
/// Holds the most-recently-built compliance framework so read endpoints (/api/framework, /api/policies,
/// /api/gaps, /api/traceability/...) and every UI tab share one consistent graph. The framework is
/// seeded eagerly at startup from the offline engine, and replaced whenever POST /api/run executes the
/// pipeline. Thread-safe via a simple lock; the framework object itself is treated as immutable once set.
/// </summary>
public sealed class FrameworkStore
{
    private readonly object _gate = new();
    private ComplianceFramework _current;

    public FrameworkStore()
    {
        // Seed deterministically so the app has a complete framework before any /api/run is called.
        _current = FrameworkBuilder.Build();
        OfflineComplianceEngine.AppendStageLogs(_current, reasonSuffix: null);
    }

    public ComplianceFramework Current
    {
        get { lock (_gate) return _current; }
    }

    public void Set(ComplianceFramework fw)
    {
        lock (_gate) _current = fw;
    }
}
