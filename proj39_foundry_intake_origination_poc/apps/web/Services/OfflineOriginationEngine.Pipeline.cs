using System.Text;
using System.Text.RegularExpressions;
using Proj39.IntakeOrigination.Web.Models;

namespace Proj39.IntakeOrigination.Web.Services;

/// <summary>
/// OfflineOriginationEngine part 2: triage/classification, lead research + demand signals, and the
/// origination report/study generator.
/// </summary>
public sealed partial class OfflineOriginationEngine
{
    // ===================================================== 2. TRIAGE =====================================================

    public static TriageResult Triage(InboundEmail email, ExtractionResult x)
    {
        var t = new TriageResult();
        var text = $"{email.Subject}\n{email.Body}";

        if (IsLikelySpam(email))
        {
            t.Classification = "Spam"; t.Score = 0; t.RoutedTo = "Quarantine"; t.SlaTarget = "None";
            t.Recommendation = "Discard — matches spam/scam heuristics (prize/fee solicitation).";
            t.Tags.Add("spam");
            t.Factors.Add(new TriageFactor { Name = "Spam heuristics", Points = 0, Detail = "Prize/fee/scam language detected." });
            return t;
        }

        int score = 0;

        int budget = x.Opportunity.EstimatedValue switch
        {
            >= 1_000_000m => 30, >= 500_000m => 24, >= 100_000m => 16, >= 10_000m => 8,
            _ => x.Opportunity.BudgetStatus == "Budget approved" ? 20 : 4
        };
        score += budget;
        t.Factors.Add(new TriageFactor { Name = "Budget / deal size", Points = budget, Detail = x.Opportunity.EstimatedValue is { } v ? $"{x.Opportunity.Currency} {v:N0}" : (x.Opportunity.BudgetStatus ?? "unknown") });

        int authority = x.Lead.Seniority switch { "C-Level" => 20, "VP" => 17, "Director" => 13, "Manager" => 8, _ => 3 };
        if (x.Lead.IsDecisionMaker) authority = Math.Min(20, authority + 3);
        score += authority;
        t.Factors.Add(new TriageFactor { Name = "Authority", Points = authority, Detail = $"{x.Lead.Seniority}{(x.Lead.IsDecisionMaker ? ", decision maker" : "")}" });

        int need = Math.Min(20, x.Opportunity.Drivers.Count * 6 + (x.Opportunity.ProductInterest is not null ? 4 : 0));
        score += need;
        t.Factors.Add(new TriageFactor { Name = "Need / pain signals", Points = need, Detail = $"{x.Opportunity.Drivers.Count} driver(s); interest: {x.Opportunity.ProductInterest ?? "unclear"}" });

        int timing = ScoreTimeline(x.Opportunity.Timeline, text);
        score += timing;
        t.Factors.Add(new TriageFactor { Name = "Timeline / urgency", Points = timing, Detail = x.Opportunity.Timeline ?? "not stated" });

        int fit = x.Account.EmployeeBand switch
        {
            "10,000+" or "5,001-10,000" => 15, "1,001-5,000" => 12, "251-1,000" => 9, "51-250" => 6,
            _ => x.Account.Industry is not null ? 5 : 2
        };
        score += fit;
        t.Factors.Add(new TriageFactor { Name = "Company fit / size", Points = fit, Detail = $"{x.Account.EmployeeBand ?? "size unknown"}, {x.Account.Industry ?? "industry unclear"}" });

        t.Score = Math.Clamp(score, 0, 100);

        bool enterprise = x.Account.EmployeeBand is "10,000+" or "5,001-10,000" or "1,001-5,000";
        (t.Classification, t.RoutedTo, t.SlaTarget) = t.Score switch
        {
            >= 70 => ("Hot", enterprise ? "Enterprise Sales" : "Inside Sales", "4 business hours"),
            >= 45 => ("Warm", enterprise ? "Enterprise Sales" : "Inside Sales", "1 business day"),
            _ => ("Cold", "Nurture / Marketing", "5 business days"),
        };

        if (x.Opportunity.BudgetStatus is not null) t.Tags.Add(x.Opportunity.BudgetStatus.ToLowerInvariant().Replace(' ', '-'));
        if (x.Account.Industry is not null) t.Tags.Add(x.Account.Industry.Split(' ')[0].ToLowerInvariant());
        if (x.Lead.IsDecisionMaker) t.Tags.Add("decision-maker");

        t.Recommendation = t.Classification switch
        {
            "Hot" => $"Engage immediately. Assign an AE from {t.RoutedTo}; book discovery within {t.SlaTarget}. Lead research + tailored study attached.",
            "Warm" => $"Qualify within {t.SlaTarget}. Confirm budget/timeline, route to {t.RoutedTo}, send capability overview.",
            _ => "Add to nurture sequence; revisit when budget/timeline firm up.",
        };
        return t;
    }

    private static int ScoreTimeline(string? timeline, string text)
    {
        if (timeline is null && !Regex.IsMatch(text, @"\b(urgent|asap|this week|fast|quickly)\b", RegexOptions.IgnoreCase)) return 3;
        var tl = (timeline ?? "").ToLowerInvariant();
        if (Regex.IsMatch(text, @"\b(urgent|asap|this week)\b", RegexOptions.IgnoreCase) || tl.Contains("this week") || tl.Contains("60 days") || tl.Contains("within")) return 15;
        if (tl.Contains("q") || tl.Contains("two quarters") || tl.Contains("90")) return 12;
        if (tl.Contains("this financial year")) return 10;
        if (tl.Contains("next financial year") || tl.Contains("later this year")) return 6;
        return 8;
    }

    private static bool IsLikelySpam(InboundEmail email)
    {
        var text = $"{email.Subject}\n{email.Body}".ToLowerInvariant();
        var redFlags = new[] { "you have won", "lucky winner", "prize", "processing fee", "bank details", "claim your", "million dollars", "act now", "selected for a" };
        int hits = redFlags.Count(f => text.Contains(f));
        var domain = email.From.Contains('@') ? email.From.Split('@')[1].ToLowerInvariant() : "";
        bool suspiciousTld = domain.EndsWith(".biz") || domain.EndsWith(".top") || domain.EndsWith(".win");
        return hits >= 2 || (hits >= 1 && suspiciousTld);
    }

    // ===================================================== 3. LEAD RESEARCH =====================================================

    public static ResearchResult Research(ExtractionResult x, TriageResult triage)
    {
        var r = new ResearchResult();
        if (triage.Classification == "Spam")
        {
            r.CompanyOverview = "No research performed — message quarantined as spam.";
            r.FitAssessment = "Not applicable.";
            return r;
        }

        var acct = x.Account;
        var sb = new StringBuilder();
        sb.Append($"{acct.Name} is ");
        sb.Append(acct.Industry is not null ? $"a {acct.Industry.ToLowerInvariant()} organisation" : "an organisation");
        if (acct.Country is not null) sb.Append($" based in {acct.Country}");
        if (acct.EmployeeBand is not null) sb.Append($" with an estimated {acct.EmployeeBand} employees");
        if (acct.AnnualRevenueBand is not null) sb.Append($" and approx. {acct.AnnualRevenueBand} annual revenue");
        sb.Append(". ");
        sb.Append(x.Opportunity.ProductInterest is not null
            ? $"Inbound interest centres on {x.Opportunity.ProductInterest.ToLowerInvariant()}."
            : "Inbound interest is a general enquiry.");
        r.CompanyOverview = sb.ToString();

        // Demand signals — synthesised from extracted drivers + opportunity context (mock research source).
        foreach (var d in x.Opportunity.Drivers)
        {
            r.DemandSignals.Add(new DemandSignal
            {
                Signal = d,
                Source = "Inbound email (stated pain point)",
                Strength = "Strong",
                Implication = "Directly expressed need — anchor discovery and the proposed solution around this."
            });
        }
        if (x.Opportunity.BudgetStatus == "Budget approved")
            r.DemandSignals.Add(new DemandSignal { Signal = "Funded program with executive sponsorship", Source = "Inbound email", Strength = "Strong", Implication = "Reduced deal risk; accelerate to proposal." });
        if (x.Account.Industry is not null)
            r.DemandSignals.Add(new DemandSignal { Signal = $"{x.Account.Industry} sector modernisation trend", Source = "Industry intelligence (mock)", Strength = "Medium", Implication = "Reference comparable sector wins and compliance posture." });
        if (x.Opportunity.Timeline is not null)
            r.DemandSignals.Add(new DemandSignal { Signal = $"Stated timeline: {x.Opportunity.Timeline}", Source = "Inbound email", Strength = "Medium", Implication = "Align delivery plan and resourcing to this window." });

        // Recommended actions.
        if (triage.Classification == "Hot")
        {
            r.RecommendedActions.Add($"Book a discovery call within {triage.SlaTarget}.");
            r.RecommendedActions.Add("Prepare a tailored solution outline referencing the stated drivers.");
            r.RecommendedActions.Add("Loop in a solutions architect for the technical fit discussion.");
        }
        else if (triage.Classification == "Warm")
        {
            r.RecommendedActions.Add("Send a capability overview and 2 relevant case studies.");
            r.RecommendedActions.Add("Schedule a qualification call to confirm budget and timeline.");
        }
        else
        {
            r.RecommendedActions.Add("Add to a sector-specific nurture sequence.");
            r.RecommendedActions.Add("Re-evaluate in 60-90 days for budget/timeline changes.");
        }

        // Talking points & competitors (illustrative).
        if (x.Account.Industry is not null) r.TalkingPoints.Add($"Proven outcomes in {x.Account.Industry.ToLowerInvariant()}.");
        if (x.Opportunity.Drivers.Count > 0) r.TalkingPoints.Add($"How we address: {x.Opportunity.Drivers[0]}.");
        r.TalkingPoints.Add("Azure-native, secure-by-default architecture (managed identity, HTTPS-only).");
        r.TalkingPoints.Add("Fast time-to-value via a phased POC -> production rollout.");

        r.Competitors.AddRange(x.Account.Industry switch
        {
            "Logistics & Transport" => new[] { "Samsara", "Geotab", "Verizon Connect" },
            "Healthcare" => new[] { "Epic", "InterSystems", "Oracle Health" },
            "Mining & Resources" => new[] { "AVEVA", "GE Digital", "Palantir" },
            _ => new[] { "In-house build", "Generic SI" }
        });

        r.FitAssessment = triage.Score switch
        {
            >= 70 => "Strong fit: funded, senior sponsorship, clear pain, and timeline urgency. Prioritise.",
            >= 45 => "Moderate fit: real need but budget/timeline need confirmation. Qualify before investing heavily.",
            _ => "Low immediate fit: nurture until intent strengthens.",
        };
        return r;
    }

    // ===================================================== 4. REPORT / STUDY =====================================================

    public static OriginationReport BuildReport(OriginationCase c)
    {
        var x = c.Extraction; var t = c.Triage; var research = c.Research;
        var report = new OriginationReport
        {
            Title = $"Origination Study — {x.Account.Name}",
            Disposition = t.Classification switch { "Hot" => "Pursue", "Warm" => "Pursue", "Spam" => "Disqualify", _ => "Nurture" },
        };

        report.ExecutiveSummary =
            $"{x.Account.Name} submitted an inbound enquiry regarding {x.Opportunity.ProductInterest ?? "our solutions"}. " +
            $"Triage classified this as a {t.Classification} lead (score {t.Score}/100), routed to {t.RoutedTo} with a {t.SlaTarget} response SLA. " +
            (x.Opportunity.EstimatedValue is { } v ? $"Estimated opportunity value is {x.Opportunity.Currency} {v:N0}. " : "") +
            $"Recommended disposition: {report.Disposition}.";

        report.Sections.Add(new ReportSection { Heading = "Account", Body =
            $"Name: {x.Account.Name}\nIndustry: {x.Account.Industry ?? "Unknown"}\nSize: {x.Account.EmployeeBand ?? "Unknown"}\n" +
            $"Revenue: {x.Account.AnnualRevenueBand ?? "Unknown"}\nLocation: {x.Account.Country ?? "Unknown"}\nWebsite: {x.Account.Website ?? "Unknown"}" });

        report.Sections.Add(new ReportSection { Heading = "Lead", Body =
            $"Name: {x.Lead.FullName}\nTitle: {x.Lead.Title ?? "Unknown"} ({x.Lead.Seniority})\nEmail: {x.Lead.Email}\n" +
            $"Phone: {x.Lead.Phone ?? "n/a"}\nDecision maker: {(x.Lead.IsDecisionMaker ? "Yes" : "Unclear")}\nPreferred contact: {x.Lead.PreferredContactMethod}" });

        report.Sections.Add(new ReportSection { Heading = "Opportunity", Body =
            $"Name: {x.Opportunity.Name}\nInterest: {x.Opportunity.ProductInterest ?? "Unknown"}\n" +
            $"Estimated value: {(x.Opportunity.EstimatedValue is { } ev ? $"{x.Opportunity.Currency} {ev:N0}" : "Unknown")}\n" +
            $"Timeline: {x.Opportunity.Timeline ?? "Unknown"}\nBudget status: {x.Opportunity.BudgetStatus ?? "Unknown"}\n" +
            $"Drivers:\n{(x.Opportunity.Drivers.Count > 0 ? string.Join("\n", x.Opportunity.Drivers.Select(d => "  - " + d)) : "  - (none extracted)")}" });

        report.Sections.Add(new ReportSection { Heading = "Triage & Classification", Body =
            $"Classification: {t.Classification} (score {t.Score}/100)\nRouting: {t.RoutedTo}\nSLA: {t.SlaTarget}\n" +
            $"Score breakdown:\n{string.Join("\n", t.Factors.Select(f => $"  - {f.Name}: {f.Points} pts ({f.Detail})"))}\n" +
            $"Recommendation: {t.Recommendation}" });

        report.Sections.Add(new ReportSection { Heading = "Lead Research & Demand Signals", Body =
            $"{research.CompanyOverview}\n\nDemand signals:\n" +
            $"{string.Join("\n", research.DemandSignals.Select(s => $"  - [{s.Strength}] {s.Signal} — {s.Implication} (src: {s.Source})"))}\n\n" +
            $"Fit assessment: {research.FitAssessment}" });

        report.Sections.Add(new ReportSection { Heading = "Recommended Next Steps", Body =
            string.Join("\n", research.RecommendedActions.Select((a, i) => $"  {i + 1}. {a}")) });

        report.RecommendedNextStep = research.RecommendedActions.FirstOrDefault() ?? "Add to nurture.";
        report.GeneratedMarkdown = RenderMarkdown(report, c);
        return report;
    }

    private static string RenderMarkdown(OriginationReport report, OriginationCase c)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {report.Title}");
        sb.AppendLine();
        sb.AppendLine($"*Generated {c.CreatedUtc:yyyy-MM-dd HH:mm} UTC · Engine: {c.Engine} · Case {c.CaseId}*");
        sb.AppendLine();
        sb.AppendLine($"**Disposition:** {report.Disposition}  ");
        sb.AppendLine($"**Classification:** {c.Triage.Classification} (score {c.Triage.Score}/100)");
        sb.AppendLine();
        sb.AppendLine("## Executive Summary");
        sb.AppendLine();
        sb.AppendLine(report.ExecutiveSummary);
        sb.AppendLine();
        foreach (var s in report.Sections)
        {
            sb.AppendLine($"## {s.Heading}");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine(s.Body);
            sb.AppendLine("```");
            sb.AppendLine();
        }
        sb.AppendLine("---");
        sb.AppendLine("*Microsoft Foundry Intake & Origination POC — proj39. Mock/demo data; not for production decisions.*");
        return sb.ToString();
    }
}
