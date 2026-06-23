using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Proj44.Compliance.Web.Pages;

// Lightweight page models for the compliance-mapper tabs. Each tab renders client-side (app.js reads
// the framework graph from the /api endpoints), so these just back the physical routes used by the
// persistent top nav and asserted by the WebApplicationFactory page tests.

public sealed class IndexModel : PageModel { }
public sealed class RequirementsModel : PageModel { }
public sealed class PoliciesModel : PageModel { }
public sealed class StandardsModel : PageModel { }
public sealed class ControlsModel : PageModel { }
public sealed class MappingsModel : PageModel { }
public sealed class GapsModel : PageModel { }
public sealed class TraceabilityModel : PageModel { }
public sealed class PipelineModel : PageModel { }
