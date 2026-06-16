using System.Globalization;
using System.Text.RegularExpressions;
using Proj41.Underwriting.Web.Models;

namespace Proj41.Underwriting.Web.Services;

/// <summary>
/// Heuristic NLP + deterministic scoring that powers the offline underwriting pipeline.
/// Kept in a partial class so the orchestration in OfflineUnderwritingPipeline.cs stays readable.
/// </summary>
public sealed partial class OfflineUnderwritingPipeline
{
    // ---------------------------------------------------------------- 1. Extraction ---

    private static ExtractedRecords ExtractRecords(SubmissionEmail email)
    {
        var text = $"{email.Subject}\n{email.Body}".Trim();
        var lower = text.ToLowerInvariant();

        var producer = ExtractProducer(email, text, lower);
        var insured = ExtractInsured(email, text, lower);
        var submission = ExtractSubmission(text, lower);

        var missing = new List<string>();
        if (insured.TotalInsurableValue is null && IsPropertyLike(submission.LineOfBusiness))
            missing.Add("Total insurable value (TIV) / statement of values");
        if (submission.RequestedLimit is null) missing.Add("Requested coverage limit");
        if (submission.EffectiveDate is null) missing.Add("Requested effective date");
        if (insured.EmployeeCount is null) missing.Add("Employee count / payroll");
        if (insured.YearsInBusiness == 0) missing.Add("Years in business / operating history");
        if (string.IsNullOrWhiteSpace(insured.Industry)) missing.Add("Business description / industry");

        return new ExtractedRecords
        {
            Producer = producer,
            Insured = insured,
            Submission = submission,
            MissingForUnderwriting = missing,
            RawText = text
        };
    }

    private static Producer ExtractProducer(SubmissionEmail email, string text, string lower)
    {
        var name = !string.IsNullOrWhiteSpace(email.FromName) ? email.FromName.Trim() : GuessPersonName(text);
        var brokerage = ExtractBrokerage(email, text, lower);
        var title = ExtractTitle(lower);

        var tier = "Independent";
        if (Regex.IsMatch(lower, @"\b(marsh|aon|gallagher|willis|lockton|wtw|hub international)\b")) tier = "National";
        else if (lower.Contains("wholesale") || lower.Contains("mga") || lower.Contains("managing general")) tier = "Wholesale";
        else if (Regex.IsMatch(lower, @"\b(regional|statewide)\b")) tier = "Regional";

        var appointed = !Regex.IsMatch(lower, @"\b(new broker|not appointed|first submission)\b");

        double conf = 0.45;
        if (!string.IsNullOrWhiteSpace(email.FromName)) conf += 0.20; // base bump for a named sender
        if (!string.IsNullOrWhiteSpace(brokerage)) conf += 0.2;
        if (!string.IsNullOrWhiteSpace(title)) conf += 0.1;

        return new Producer
        {
            ContactName = name,
            Title = title,
            Brokerage = brokerage,
            Email = email.From,
            Phone = ExtractPhone(text),
            BrokerTier = tier,
            Appointed = appointed,
            Confidence = Math.Round(Math.Min(conf, 0.97), 2)
        };
    }

    private static Insured ExtractInsured(SubmissionEmail email, string text, string lower)
    {
        var company = ExtractInsuredName(text, lower, email);
        var industry = ExtractIndustry(lower);
        var country = ExtractCountry(text, lower);

        var insured = new Insured
        {
            CompanyName = company,
            Industry = industry,
            SicDivision = SicDivisionFor(industry, lower),
            Country = country,
            Headquarters = ExtractCity(text) is { Length: > 0 } city ? $"{city}{(string.IsNullOrEmpty(country) ? "" : ", " + country)}" : country,
            EmployeeCount = ExtractEmployees(lower),
            AnnualRevenue = ExtractMoney(lower, "revenue", "turnover", "sales"),
            TotalInsurableValue = ExtractMoney(lower, "tiv", "insurable value", "values", "property value", "building value"),
            LocationCount = ExtractLocations(lower),
            YearsInBusiness = ExtractYears(lower)
        };

        // Enrichment notes (clearly-labelled, deterministic).
        var enrich = new List<string>();
        if (insured.EmployeeCount is int ec)
            enrich.Add(ec >= 1000 ? "Large enterprise headcount" : ec >= 200 ? "Mid-market headcount" : "Small-business headcount");
        if (insured.YearsInBusiness >= 15) enrich.Add("Established operating history (15+ yrs)");
        else if (insured.YearsInBusiness > 0 && insured.YearsInBusiness < 3) enrich.Add("Newer venture (<3 yrs) — limited loss history");
        if (insured.LocationCount > 5) enrich.Add($"Multi-location schedule ({insured.LocationCount} sites)");
        insured.Enrichment = enrich;

        insured.Confidence = Math.Round(
            0.4
            + (string.IsNullOrWhiteSpace(company) ? 0 : 0.25)
            + (string.IsNullOrWhiteSpace(industry) ? 0 : 0.15)
            + (insured.EmployeeCount is null ? 0 : 0.1)
            + (insured.TotalInsurableValue is null ? 0 : 0.07), 2);
        return insured;
    }

    private static RiskSubmission ExtractSubmission(string text, string lower)
    {
        var lob = ExtractLineOfBusiness(lower);
        var sub = new RiskSubmission
        {
            LineOfBusiness = lob,
            CoverageType = CoverageFor(lob, lower),
            RequestedLimit = ExtractMoney(lower, "limit", "coverage of", "cover of", "sum insured", "aggregate"),
            Deductible = ExtractMoney(lower, "deductible", "excess", "retention"),
            EffectiveDate = ExtractEffectiveDate(lower),
            SubmissionType = lower.Contains("renewal") ? "Renewal"
                : lower.Contains("rewrite") || lower.Contains("re-write") ? "Rewrite" : "New Business",
            IncumbentCarriers = ExtractCarriers(lower)
        };

        // Premium indication: estimate when not stated, from limit/TIV and line hazard.
        sub.EstimatedAnnualPremium = ExtractMoney(lower, "premium", "annual premium")
            ?? EstimatePremium(sub, ExtractMoney(lower, "tiv", "insurable value", "values"));

        sub.Confidence = Math.Round(
            0.45
            + (string.IsNullOrWhiteSpace(lob) ? 0 : 0.25)
            + (sub.RequestedLimit is null ? 0 : 0.15)
            + (sub.EffectiveDate is null ? 0 : 0.1), 2);
        return sub;
    }

    // ---------------------------------------------------------------- 2. Triage ---

    private static AppetiteDecision Triage(ExtractedRecords r)
    {
        var ins = r.Insured;
        var sub = r.Submission;
        var flags = new List<string>();
        var referrals = new List<string>();

        // ---- Fit (desirability) ----
        int fit = 50;
        if (ins.EmployeeCount is int ec) fit += ec >= 1000 ? 18 : ec >= 200 ? 12 : ec >= 50 ? 6 : 0;
        if (ins.YearsInBusiness >= 15) fit += 12; else if (ins.YearsInBusiness is > 0 and < 3) fit -= 12;
        if (r.Producer.BrokerTier is "National" or "Regional") fit += 8;
        if (r.Producer.Appointed) fit += 4; else { fit -= 6; referrals.Add("Unappointed / new producer"); }
        if (sub.SubmissionType == "Renewal") fit += 6;

        // ---- Risk (scrutiny needed) ----
        int risk = 35;
        var hazard = HazardClass(ins.Industry, ins.SicDivision);
        risk += hazard switch { "High" => 28, "Medium" => 14, _ => 4 };
        if (sub.LineOfBusiness is "Cyber") risk += 12;
        if (sub.LineOfBusiness is "Professional Liability") risk += 8;
        if (ins.YearsInBusiness is > 0 and < 3) risk += 12;
        if (sub.RequestedLimit is decimal lim && lim >= 25_000_000m) { risk += 14; referrals.Add("High limit (>= $25M) exceeds desk authority"); }
        if (ins.TotalInsurableValue is decimal tiv && tiv >= 100_000_000m) { risk += 12; referrals.Add("Large property schedule (TIV >= $100M)"); }

        // ---- Loss / adverse keywords from the source text ----
        var bodyLower = $"{r.Submission.CoverageType} {ins.Industry}".ToLowerInvariant();
        // (loss signals are mostly derived in research; surface obvious flags here)
        if (CatExposed(ins)) { risk += 10; flags.Add("Catastrophe-exposed location (coastal/wildfire/flood zone)"); }

        risk = Math.Clamp(risk, 0, 100);
        fit = Math.Clamp(fit, 0, 100);

        // ---- Spam / non-risk submissions ----
        bool spam = LooksLikeSpam(r);
        // ---- Appetite class ----
        string appetite;
        string recommendation;
        bool declined = false;

        if (spam)
        {
            appetite = "Decline"; recommendation = "Decline"; declined = true;
            flags.Add("Not a genuine submission (marketing/solicitation)");
        }
        else if (OutOfAppetite(r, out var oaReason))
        {
            appetite = "Out of Appetite"; recommendation = "Decline"; declined = true;
            flags.Add(oaReason);
        }
        else if (referrals.Count > 0 || risk >= 70)
        {
            appetite = "Refer to Underwriter"; recommendation = "Refer";
        }
        else
        {
            appetite = "In Appetite"; recommendation = "Quote";
        }

        // ---- Priority + SLA ----
        string priority; int sla;
        if (declined) { priority = "P3"; sla = 24; }
        else if (fit >= 70 && recommendation == "Quote") { priority = "P1"; sla = 24; }
        else if (recommendation == "Refer" || fit >= 55) { priority = "P2"; sla = 48; }
        else { priority = "P3"; sla = 72; }

        var (queue, desk) = RouteDesk(sub.LineOfBusiness, appetite, ins);

        var rationale = BuildTriageRationale(appetite, recommendation, fit, risk, hazard, ins, sub, referrals);

        return new AppetiteDecision
        {
            AppetiteClass = appetite,
            Recommendation = recommendation,
            RiskScore = risk,
            FitScore = fit,
            Priority = priority,
            SlaHours = sla,
            RoutingQueue = queue,
            AssignedDesk = desk,
            Declined = declined,
            ReferralTriggers = referrals.Distinct().ToList(),
            RiskFlags = flags.Distinct().ToList(),
            Rationale = rationale
        };
    }

    // ---------------------------------------------------------------- 3. Research ---

    private static LeadResearch Research(SubmissionEmail email, ExtractedRecords r, AppetiteDecision triage)
    {
        var ins = r.Insured;
        var signals = new List<ExposureSignal>();

        // Industry hazard signal
        var hazard = HazardClass(ins.Industry, ins.SicDivision);
        signals.Add(new ExposureSignal
        {
            Category = "IndustryHazard",
            Headline = $"{(string.IsNullOrWhiteSpace(ins.Industry) ? "General" : ins.Industry)} sector — {hazard.ToLowerInvariant()} hazard class",
            Detail = hazard == "High"
                ? "Operations carry elevated frequency/severity potential; expect engineering review and conditions."
                : hazard == "Medium"
                    ? "Moderate hazard profile typical for the class; standard controls expected."
                    : "Low-hazard operations; favourable from a loss-cost perspective.",
            Sentiment = hazard == "High" ? "Adverse" : hazard == "Low" ? "Positive" : "Neutral",
            Impact = hazard == "High" ? "High" : "Medium"
        });

        // Catastrophe exposure
        if (CatExposed(ins))
            signals.Add(new ExposureSignal
            {
                Category = "CatastropheExposure",
                Headline = "Nat-cat accumulation risk at insured location(s)",
                Detail = "Location data suggests coastal wind / wildfire / flood accumulation; CAT modelling and sub-limits recommended.",
                Sentiment = "Adverse",
                Impact = "High"
            });

        // Loss history (deterministic synthesis from operating history)
        var lossSeed = StableHash(ins.CompanyName + ins.Industry);
        bool priorLoss = (lossSeed % 100) < (hazard == "High" ? 55 : hazard == "Medium" ? 30 : 12);
        signals.Add(new ExposureSignal
        {
            Category = "LossHistory",
            Headline = priorLoss ? "Probable prior claim activity in the class" : "No adverse loss indicators surfaced",
            Detail = priorLoss
                ? "Comparable risks in this class/region show recurring attritional losses; request 5-year loss runs."
                : "No public adverse events found; clean-risk treatment pending loss runs.",
            Sentiment = priorLoss ? "Adverse" : "Positive",
            Impact = priorLoss ? "High" : "Low"
        });

        // Financial stress / growth
        if (ins.YearsInBusiness is > 0 and < 3)
            signals.Add(new ExposureSignal
            {
                Category = "FinancialStress",
                Headline = "Limited operating history",
                Detail = "Newer venture with thin balance-sheet history; consider higher retention or financial covenants.",
                Sentiment = "Adverse",
                Impact = "Medium"
            });
        else if (ins.EmployeeCount is int ec && ec >= 500)
            signals.Add(new ExposureSignal
            {
                Category = "Growth",
                Headline = "Scaled, stable employer",
                Detail = $"{ec:N0} employees implies established operations and dedicated risk management.",
                Sentiment = "Positive",
                Impact = "Medium"
            });

        // Regulatory
        if (NeedsRegulatory(ins))
            signals.Add(new ExposureSignal
            {
                Category = "Regulatory",
                Headline = "Regulated data / safety obligations",
                Detail = "Sector implies privacy/safety regulation (e.g. HIPAA/PCI/OSHA); align coverage wording with compliance exposure.",
                Sentiment = "Neutral",
                Impact = "Medium"
            });

        // Intent / urgency
        int intent = 45;
        if (triage.Recommendation == "Quote") intent += 20;
        if (r.Submission.EffectiveDate is DateTimeOffset eff && eff <= DateTimeOffset.UtcNow.AddDays(30)) intent += 20;
        if (r.Submission.IncumbentCarriers.Count > 0) intent += 8; // shopping = active
        if (triage.Declined) intent = Math.Min(intent, 20);
        intent = Math.Clamp(intent, 0, 100);
        var band = intent >= 70 ? "Hot — bind-ready" : intent >= 45 ? "Warm — quote stage" : "Cool — early";

        var highlights = new List<string>();
        highlights.Add($"Hazard class: {hazard}");
        if (ins.TotalInsurableValue is decimal tiv) highlights.Add($"TIV ~ {Money(tiv)}");
        if (r.Submission.RequestedLimit is decimal lim) highlights.Add($"Requested limit {Money(lim)}");
        if (ins.LocationCount > 1) highlights.Add($"{ins.LocationCount} locations");
        highlights.Add(priorLoss ? "Loss runs required (probable prior activity)" : "No adverse loss indicators");

        var questions = BuildQuestions(r, hazard, priorLoss);

        var overview = BuildOverview(ins, r.Submission, hazard);

        return new LeadResearch
        {
            AccountOverview = overview,
            IntentScore = intent,
            IntentBand = band,
            ExposureHighlights = highlights,
            Signals = signals,
            RecommendedQuestions = questions
        };
    }

    // ---------------------------------------------------------------- 4. Study ---

    private static UnderwritingStudy BuildStudy(ExtractedRecords r, AppetiteDecision triage, LeadResearch research)
    {
        var ins = r.Insured;
        var sub = r.Submission;

        string overall = triage.Declined
            ? (triage.AppetiteClass == "Decline" && triage.RiskFlags.Any(f => f.Contains("marketing")) ? "Decline" : "Decline")
            : triage.Recommendation == "Refer" ? "Refer"
            : (triage.RiskFlags.Count > 0 || research.Signals.Any(s => s.Sentiment == "Adverse")) ? "Quote with conditions"
            : "Bind";

        var indicated = triage.Declined ? null : (sub.EstimatedAnnualPremium ?? EstimatePremium(sub, ins.TotalInsurableValue));

        var summary = BuildExecutiveSummary(ins, sub, triage, research, overall, indicated);

        var conditions = new List<string>();
        var exclusions = new List<string>();
        if (!triage.Declined)
        {
            if (CatExposed(ins)) { conditions.Add("Apply nat-cat sub-limit and percentage deductible for windstorm/flood."); }
            if (research.Signals.Any(s => s.Category == "LossHistory" && s.Sentiment == "Adverse"))
                conditions.Add("Obtain 5-year currently-valued loss runs prior to binding.");
            if (HazardClass(ins.Industry, ins.SicDivision) == "High")
                conditions.Add("Require risk-engineering survey within 60 days of inception.");
            if (sub.LineOfBusiness == "Cyber")
            { conditions.Add("Mandate MFA, EDR and tested backups as warranty."); exclusions.Add("Prior-and-pending / known-incident exclusion."); }
            if (ins.YearsInBusiness is > 0 and < 3)
                conditions.Add("Increase retention to reflect limited operating history.");
            if (conditions.Count == 0) conditions.Add("Standard policy terms; no special conditions indicated.");
        }

        var sections = new List<ReportSection>
        {
            new() { Heading = "Risk Profile", Body = BuildOverview(ins, sub, HazardClass(ins.Industry, ins.SicDivision)) },
            new() { Heading = "Appetite & Triage", Body = triage.Rationale },
            new() { Heading = "Exposure Analysis", Body = string.Join(" ", research.Signals.Select(s => $"[{s.Category}/{s.Sentiment}] {s.Headline}: {s.Detail}")) },
            new() { Heading = "Pricing Indication", Body = BuildPricingRationale(sub, ins, indicated, triage) },
        };

        var nextActions = BuildNextActions(overall, triage, r, research);

        return new UnderwritingStudy
        {
            Title = $"Underwriting Risk Study — {(string.IsNullOrWhiteSpace(ins.CompanyName) ? "Unnamed Insured" : ins.CompanyName)}",
            ExecutiveSummary = summary,
            OverallRecommendation = overall,
            IndicatedPremium = indicated,
            PricingRationale = BuildPricingRationale(sub, ins, indicated, triage),
            KeyRiskFlags = triage.RiskFlags.Concat(research.Signals.Where(s => s.Sentiment == "Adverse").Select(s => s.Headline)).Distinct().ToList(),
            RecommendedConditions = conditions,
            Exclusions = exclusions,
            Sections = sections,
            NextActions = nextActions
        };
    }
}
