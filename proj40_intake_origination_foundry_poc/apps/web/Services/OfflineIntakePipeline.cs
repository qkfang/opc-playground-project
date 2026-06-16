using System.Diagnostics;
using System.Text.RegularExpressions;
using Proj40.IntakeOrigination.Web.Models;

namespace Proj40.IntakeOrigination.Web.Services;

/// <summary>
/// Deterministic, dependency-free origination pipeline. It uses pragmatic heuristics (regex, keyword
/// dictionaries, firmographic tables) to turn an inbound email into a believable Lead/Account/Opportunity,
/// triage decision, demand-signal research, and an origination report.
///
/// This is what makes the POC demoable without any Azure/Foundry connectivity, and it doubles as the
/// safety net the Foundry pipeline falls back to on any error.
/// </summary>
public sealed partial class OfflineIntakePipeline : IIntakePipeline
{
    public string Name => "offline";

    public Task<IntakeCase> RunAsync(InboundEmail email, CancellationToken ct = default)
    {
        var c = new IntakeCase { Email = email, Engine = Name, Status = "processing" };

        c.Records = TimeStage(c, "extraction", () => ExtractRecords(email),
            r => $"Extracted Lead '{r.Lead.FullName}', Account '{r.Account.CompanyName}' ({r.Account.Segment}), Opportunity ${r.Opportunity.EstimatedAnnualValue:N0} ARR — {r.Confidence}% confidence.");

        c.Triage = TimeStage(c, "triage", () => Triage(c.Records, email),
            t => $"Classified {c.Records.Account.CompanyName} as {t.Classification}/{t.Priority}, score {t.LeadScore}, routed to {t.RoutingQueue} (SLA {t.SlaHours}h).");

        c.Research = TimeStage(c, "research", () => Research(c.Records),
            r => $"Compiled {r.Signals.Count} demand signals; intent {r.IntentScore} ({r.BuyingStage}).");

        c.Report = TimeStage(c, "report", () => BuildReport(c),
            r => $"Generated origination brief '{r.Title}' with {r.Sections.Count} sections.");

        c.Status = c.Triage.Disqualified ? "disqualified" : "completed";
        return Task.FromResult(c);
    }

    // ------------------------------------------------------------------ Extraction ----

    public ExtractedRecords ExtractRecords(InboundEmail email)
    {
        var text = $"{email.Subject}\n{email.Body}";
        var rec = new ExtractedRecords();
        var missing = new List<string>();

        // --- Lead ---
        rec.Lead.Email = email.From;
        rec.Lead.FullName = !string.IsNullOrWhiteSpace(email.FromName) ? email.FromName : GuessNameFromEmail(email.From);
        rec.Lead.Title = ExtractTitle(text);
        rec.Lead.Seniority = SeniorityFromTitle(rec.Lead.Title);
        rec.Lead.IsDecisionMaker = rec.Lead.Seniority is "C-Level" or "VP" or "Director";
        rec.Lead.Phone = PhoneRegex().Match(text) is { Success: true } pm ? pm.Value.Trim() : "";
        if (string.IsNullOrWhiteSpace(rec.Lead.Title)) missing.Add("Lead.Title");
        if (string.IsNullOrWhiteSpace(rec.Lead.Phone)) missing.Add("Lead.Phone");

        // --- Account ---
        var domain = DomainFromEmail(email.From);
        rec.Account.Domain = domain;
        rec.Account.CompanyName = CompanyFromSignatureOrDomain(text, domain);
        rec.Account.Industry = ClassifyIndustry(text, domain);
        rec.Account.Country = ExtractCountry(text);
        rec.Account.Region = RegionFromCountry(rec.Account.Country);
        (rec.Account.Segment, rec.Account.EmployeeBand) = SegmentFromText(text);
        rec.Account.IsExistingCustomer = ContainsAny(text, "renew", "existing contract", "current customer", "our account team");
        if (string.IsNullOrWhiteSpace(rec.Account.Country)) missing.Add("Account.Country");

        // --- Opportunity ---
        rec.Opportunity.ProductInterest = ProductInterest(text);
        rec.Opportunity.UseCase = FirstSentenceAbout(email.Body);
        rec.Opportunity.Timeline = ExtractTimeline(text);
        rec.Opportunity.BudgetStatus = ContainsAny(text, "budget approved", "budgeted", "have budget", "funding secured")
            ? "Budgeted"
            : ContainsAny(text, "no budget", "exploring", "early stage", "just researching") ? "Exploring" : "Unknown";
        rec.Opportunity.Competitors = DetectCompetitors(text);
        rec.Opportunity.EstimatedAnnualValue = EstimateArr(rec.Account.Segment, text);
        rec.Opportunity.Name = $"{rec.Account.CompanyName} — {rec.Opportunity.ProductInterest}";
        rec.Opportunity.Notes = Truncate(email.Body.Replace("\r", " ").Replace("\n", " "), 240);
        if (rec.Opportunity.EstimatedAnnualValue == 0) missing.Add("Opportunity.EstimatedAnnualValue");

        rec.MissingFields = missing;
        rec.Confidence = ScoreExtractionConfidence(rec, missing);
        return rec;
    }

    private static int ScoreExtractionConfidence(ExtractedRecords r, List<string> missing)
    {
        int score = 60;
        if (!string.IsNullOrWhiteSpace(r.Lead.Title)) score += 8;
        if (r.Lead.IsDecisionMaker) score += 6;
        if (!string.IsNullOrWhiteSpace(r.Account.CompanyName) && r.Account.CompanyName != "Unknown") score += 8;
        if (r.Account.Segment is "Enterprise" or "Strategic") score += 5;
        if (r.Opportunity.EstimatedAnnualValue > 0) score += 7;
        if (!string.IsNullOrWhiteSpace(r.Opportunity.Timeline)) score += 4;
        score -= missing.Count * 3;
        return Math.Clamp(score, 35, 98);
    }

    // --------------------------------------------------------------------- Triage ----

    public TriageDecision Triage(ExtractedRecords rec, InboundEmail email)
    {
        var text = $"{email.Subject}\n{email.Body}".ToLowerInvariant();
        var t = new TriageDecision();

        if (ContainsAny(text, "unsubscribe", "seo services", "buy followers", "lottery", "crypto giveaway", "work from home opportunity"))
        {
            t.Classification = "Spam/Disqualified";
            t.Priority = "P3";
            t.LeadScore = 5;
            t.Disqualified = true;
            t.RoutingQueue = "No action";
            t.RecommendedAction = "Suppress sender; no follow-up.";
            t.SlaHours = 0;
            t.Rationale = "Message matched disqualification heuristics (solicitation/spam).";
            t.RiskFlags.Add("Disqualified sender");
            return t;
        }

        if (rec.Account.IsExistingCustomer && ContainsAny(text, "renew", "renewal", "extend contract"))
            t.Classification = "Renewal";
        else if (rec.Account.IsExistingCustomer)
            t.Classification = "Expansion";
        else if (ContainsAny(text, "support ticket", "broken", "not working", "outage", "bug report"))
            t.Classification = "Support";
        else
            t.Classification = "New Business";

        int fit = rec.Account.Segment switch
        {
            "Strategic" => 35,
            "Enterprise" => 30,
            "Mid-Market" => 20,
            "SMB" => 10,
            _ => 12
        };
        if (rec.Lead.IsDecisionMaker) fit += 10;
        if (rec.Account.IsExistingCustomer) fit += 5;

        int intent = 0;
        if (rec.Opportunity.BudgetStatus == "Budgeted") intent += 22;
        if (ContainsAny(text, "this quarter", "asap", "urgent", "immediately", "by end of")) intent += 14;
        if (ContainsAny(text, "evaluating", "rfp", "proposal", "demo", "pricing", "poc", "pilot")) intent += 14;
        if (rec.Opportunity.EstimatedAnnualValue >= 250_000) intent += 10;
        if (rec.Opportunity.Competitors.Count > 0) intent += 6;

        t.LeadScore = Math.Clamp(fit + intent, 0, 100);

        (t.Priority, t.SlaHours) = t.LeadScore switch
        {
            >= 75 => ("P1", 4),
            >= 50 => ("P2", 24),
            _ => ("P3", 72)
        };

        var seg = rec.Account.Segment is "Strategic" or "Enterprise" ? "Enterprise AE"
            : rec.Account.Segment == "Mid-Market" ? "Mid-Market AE" : "SMB / Inside Sales";
        var region = string.IsNullOrWhiteSpace(rec.Account.Region) ? "Global" : rec.Account.Region;
        t.RoutingQueue = $"{seg} — {region}";

        t.RecommendedAction = t.Priority switch
        {
            "P1" => "Book executive discovery call within 24h; loop in solutions engineering.",
            "P2" => "Personalised outreach within 1 business day; share tailored deck.",
            _ => "Add to nurture sequence; qualify budget/authority before live engagement."
        };

        if (rec.Opportunity.Competitors.Count > 0) t.RiskFlags.Add($"Competitor mentioned: {string.Join(", ", rec.Opportunity.Competitors)}");
        if (rec.Opportunity.BudgetStatus == "Exploring") t.RiskFlags.Add("Budget not confirmed");
        if (rec.Confidence < 60) t.RiskFlags.Add("Low extraction confidence — enrich before outreach");
        if (ContainsAny(text, "next year", "long term", "2027")) t.RiskFlags.Add("Long sales horizon");

        t.Rationale =
            $"Fit {fit}/50 ({rec.Account.Segment}, {(rec.Lead.IsDecisionMaker ? "decision-maker" : "non-DM")}) + " +
            $"intent {intent}/50 ({rec.Opportunity.BudgetStatus.ToLowerInvariant()} budget, timeline '{rec.Opportunity.Timeline}') " +
            $"=> score {t.LeadScore}. Classified {t.Classification}.";

        return t;
    }

    // ------------------------------------------------------------------- Research ----

    public LeadResearch Research(ExtractedRecords rec)
    {
        var r = new LeadResearch();
        var acct = rec.Account;
        var ind = string.IsNullOrWhiteSpace(acct.Industry) ? "Technology" : acct.Industry;

        r.CompanyOverview =
            $"{acct.CompanyName} is a {acct.Segment.ToLowerInvariant()} organisation in the {ind} sector" +
            (string.IsNullOrWhiteSpace(acct.Country) ? "" : $", headquartered in {acct.Country}") +
            $" ({(string.IsNullOrWhiteSpace(acct.EmployeeBand) ? "size unknown" : acct.EmployeeBand + " employees")}). " +
            (acct.IsExistingCustomer ? "Existing customer with an active relationship." : "Net-new logo for the origination pipeline.");

        int seed = StableSeed(acct.CompanyName + acct.Domain);
        var rng = new Random(seed);

        r.Signals.AddRange(BuildSignals(ind, acct.Segment, rng));
        if (rec.Opportunity.ProductInterest.Contains("AI", StringComparison.OrdinalIgnoreCase))
            r.Signals.Insert(0, new DemandSignal { Title = "Active AI platform evaluation", Category = "TechAdoption", Detail = $"Inbound enquiry explicitly references {rec.Opportunity.ProductInterest}; signals an in-flight evaluation.", Source = "Inbound email", Recency = "today", Strength = 88 });

        r.IntentScore = r.Signals.Count == 0 ? 0 : Math.Clamp((int)Math.Round(r.Signals.Average(s => s.Strength)), 0, 100);
        r.BuyingStage = r.IntentScore switch { >= 70 => "Decision", >= 45 => "Consideration", _ => "Awareness" };

        r.KeyInitiatives = InitiativesFor(ind);
        r.TalkingPoints = new()
        {
            $"Tie our value prop to {acct.CompanyName}'s {ind} priorities and {r.BuyingStage.ToLowerInvariant()}-stage needs.",
            rec.Lead.IsDecisionMaker
                ? $"{rec.Lead.FullName} is a decision-maker — lead with business outcomes & ROI."
                : $"{rec.Lead.FullName} is likely a champion — equip them to sell internally.",
            rec.Opportunity.Competitors.Count > 0
                ? $"Prepare competitive differentiation vs {string.Join(", ", rec.Opportunity.Competitors)}."
                : "No competitor named — anchor on our platform's differentiated capabilities."
        };
        return r;
    }

    private static IEnumerable<DemandSignal> BuildSignals(string industry, string segment, Random rng)
    {
        var pool = new List<DemandSignal>
        {
            new() { Title = "Headcount growth in data & engineering", Category = "Hiring", Detail = "Multiple open roles for data engineers and ML practitioners indicate platform investment.", Source = "Careers page (synthesised)", Recency = "last 30 days", Strength = rng.Next(55, 85) },
            new() { Title = "Recent funding / budget cycle", Category = "Funding", Detail = "Signals of a fresh capital or annual budget cycle increase near-term purchasing capacity.", Source = "Market intel (synthesised)", Recency = "last quarter", Strength = rng.Next(45, 80) },
            new() { Title = "Cloud / modernisation push", Category = "TechAdoption", Detail = $"{industry} peers are accelerating cloud modernisation; aligns with our offering.", Source = "Industry trend (synthesised)", Recency = "ongoing", Strength = rng.Next(50, 78) },
            new() { Title = "Leadership change in technology org", Category = "Leadership", Detail = "A new technology leader often triggers a re-evaluation of the vendor landscape.", Source = "Exec moves (synthesised)", Recency = "last 60 days", Strength = rng.Next(40, 75) },
            new() { Title = "Regulatory / compliance pressure", Category = "Regulatory", Detail = $"Tightening requirements in {industry} drive demand for governed, auditable platforms.", Source = "Regulatory watch (synthesised)", Recency = "this year", Strength = rng.Next(35, 70) },
        };
        int take = segment is "Strategic" or "Enterprise" ? 4 : 3;
        return pool.OrderByDescending(s => s.Strength).Take(take);
    }

    // --------------------------------------------------------------------- Report ----

    public OriginationReport BuildReport(IntakeCase c)
    {
        var rec = c.Records; var t = c.Triage; var r = c.Research;
        var rep = new OriginationReport
        {
            Title = $"Origination Brief — {rec.Account.CompanyName}",
            GeneratedBy = c.Engine,
            NextBestAction = t.RecommendedAction
        };

        rep.ExecutiveSummary =
            $"{rec.Account.CompanyName} ({rec.Account.Segment}, {rec.Account.Industry}) submitted an inbound enquiry via {c.Email.Channel} " +
            $"regarding {rec.Opportunity.ProductInterest}. The origination engine classified this as {t.Classification} at {t.Priority} " +
            $"with a lead score of {t.LeadScore}/100 and an estimated ${rec.Opportunity.EstimatedAnnualValue:N0} {rec.Opportunity.Currency} ARR. " +
            $"Buying stage assessed as {r.BuyingStage} (intent {r.IntentScore}/100). Recommended next step: {t.RecommendedAction}";

        rep.Highlights = new()
        {
            $"Lead: {rec.Lead.FullName}{(string.IsNullOrWhiteSpace(rec.Lead.Title) ? "" : ", " + rec.Lead.Title)} ({(rec.Lead.IsDecisionMaker ? "decision-maker" : "influencer")})",
            $"Opportunity: {rec.Opportunity.ProductInterest} | timeline '{rec.Opportunity.Timeline}' | budget {rec.Opportunity.BudgetStatus}",
            $"Routing: {t.RoutingQueue} | SLA {t.SlaHours}h",
            $"Demand signals: {r.Signals.Count} (top: {(r.Signals.FirstOrDefault()?.Title ?? "n/a")})"
        };

        rep.Recommendations = new() { t.RecommendedAction };
        if (rec.Confidence < 70) rep.Recommendations.Add("Enrich missing firmographic fields before first contact.");
        if (t.RiskFlags.Count > 0) rep.Recommendations.Add("Mitigate risk flags: " + string.Join("; ", t.RiskFlags) + ".");
        rep.Recommendations.AddRange(r.TalkingPoints.Take(2));

        rep.Sections = new()
        {
            new ReportSection { Heading = "Account & firmographics", Body = r.CompanyOverview },
            new ReportSection { Heading = "Opportunity assessment", Body = $"{rec.Opportunity.UseCase} Estimated ARR ${rec.Opportunity.EstimatedAnnualValue:N0}. Budget status: {rec.Opportunity.BudgetStatus}. {(rec.Opportunity.Competitors.Count > 0 ? "Competitive context: " + string.Join(", ", rec.Opportunity.Competitors) + "." : "No competitor named.")}" },
            new ReportSection { Heading = "Triage & routing rationale", Body = t.Rationale + $" Routed to {t.RoutingQueue}." },
            new ReportSection { Heading = "Demand-signal research", Body = string.Join(" ", r.Signals.Select(s => $"[{s.Category}] {s.Title} — {s.Detail} (strength {s.Strength}).")) },
            new ReportSection { Heading = "Engagement plan", Body = string.Join(" ", r.TalkingPoints.Select((p, i) => $"{i + 1}. {p}")) }
        };

        return rep;
    }

    private static T TimeStage<T>(IntakeCase c, string stage, Func<T> work, Func<T, string> summarise)
    {
        var sw = Stopwatch.StartNew();
        var result = work();
        sw.Stop();
        c.Trace.Add(new AgentTrace { Stage = stage, Agent = c.Engine, DurationMs = sw.ElapsedMilliseconds, Summary = summarise(result) });
        return result;
    }
}
