using Proj45.RelayDesk.Web.Models;
using Proj45.RelayDesk.Web.Services.Mcp;

namespace Proj45.RelayDesk.Web.Services;

/// <summary>Task (D365 MCP) + Outcome stage implementations for <see cref="OfflineEmailPipeline"/>.</summary>
public sealed partial class OfflineEmailPipeline
{
    // -------------------------------------------------------- 4. Task (MCP) ---

    private TaskExecution RunTask(IncomingEmail email, EmailExtraction x, TriageResult triage, IntentDecision intent)
    {
        var exec = new TaskExecution();

        if (triage.Category == "Spam")
        {
            exec.Plan = new TaskPlan { Operation = "No operation (spam)", ExpectedEffect = "Message closed as spam.", RiskLevel = "Low", Rationale = "Spam — no D365 action." };
            exec.ExecutionStatus = "skipped";
            exec.Customer = new CustomerContext { Matched = false, MatchNote = "No customer lookup for spam." };
            return exec;
        }

        // ---- Lookups via mock MCP ----
        exec.ToolCalls.Add(_mcp.Invoke("customer.search", new Dictionary<string, string>
        {
            ["query"] = x.AccountHints.FirstOrDefault() ?? email.FromName
        }));

        var account = _mcp.ResolveAccount(x.AccountHints, email.From);
        if (account is null)
        {
            exec.Customer = new CustomerContext { Matched = false, MatchNote = "No matching D365 account found from sender/hints." };
            exec.Plan = new TaskPlan
            {
                PlannedTool = "case.create", Operation = "Open service case (unmatched sender)",
                OperationArgs = new() { ["accountId"] = "", ["title"] = Trunc(email.Subject, 80), ["priority"] = triage.Urgency },
                CustomerSummary = "Sender could not be matched to an existing account.",
                ExpectedEffect = "A triage case is opened for manual account matching.",
                RiskLevel = "Low", Rationale = "No account match; route to manual handling."
            };
            exec.ExecutionStatus = intent.RequiresHuman ? "deferred-to-human" : "simulated";
            return exec;
        }

        exec.ToolCalls.Add(_mcp.Invoke("account.get", new Dictionary<string, string> { ["accountId"] = account.AccountId }));
        exec.ToolCalls.Add(_mcp.Invoke("contact.get", new Dictionary<string, string> { ["accountId"] = account.AccountId }));

        var plan = PlanFor(intent.Intent, account, email, triage);

        // Optional intent-specific lookup before acting.
        var preLookup = intent.Intent switch
        {
            "Billing Dispute" or "Technical Issue" or "Complaint Escalation" => "service.cases.list",
            "Cancellation Request" or "Sales Enquiry" => "opportunity.list",
            _ => null
        };
        if (preLookup is not null)
            exec.ToolCalls.Add(_mcp.Invoke(preLookup, new Dictionary<string, string> { ["accountId"] = account.AccountId }));

        exec.Customer = new CustomerContext
        {
            AccountId = account.AccountId, AccountName = account.Name, Tier = account.Tier, Industry = account.Industry,
            AnnualValue = account.AnnualValue, Owner = account.Owner, Status = account.Status,
            PrimaryContact = account.Contacts.FirstOrDefault(c => c.Primary)?.Name ?? account.Contacts.FirstOrDefault()?.Name ?? "",
            OpenOpportunities = account.Opportunities.Count,
            OpenServiceCases = account.ServiceCases.Count(c => c.Status == "Open"),
            Matched = true,
            MatchNote = $"Matched {account.Name} ({account.AccountId}) via {(ExtractDomain(email.From) is { } d ? d : "hints")}."
        };
        exec.Plan = plan;

        // ---- Execute (simulate) the downstream operation, unless it needs a human first ----
        if (intent.RequiresHuman)
        {
            exec.ExecutionStatus = "deferred-to-human";
            exec.OperationResult = "Held for human review before the operation is committed.";
            return exec;
        }

        if (plan.RequiresApproval)
        {
            // Still call the operation so the demo shows the resulting (pending) artefact id.
            var pending = _mcp.Invoke(plan.PlannedTool, plan.OperationArgs);
            exec.ToolCalls.Add(pending);
            exec.ExecutionStatus = "simulated";
            exec.OperationReference = ExtractRef(pending.ResultJson);
            exec.OperationResult = $"{plan.Operation} prepared (requires approval): {pending.ResultSummary}";
            return exec;
        }

        var opCall = _mcp.Invoke(plan.PlannedTool, plan.OperationArgs);
        exec.ToolCalls.Add(opCall);
        exec.ExecutionStatus = opCall.Ok ? "executed" : "simulated";
        exec.OperationReference = ExtractRef(opCall.ResultJson);
        exec.OperationResult = opCall.ResultSummary;
        return exec;
    }

    private static TaskPlan PlanFor(string intent, D365Account account, IncomingEmail email, TriageResult triage) => intent switch
    {
        "Billing Dispute" => new TaskPlan
        {
            PlannedTool = "creditmemo.raise", Operation = "Raise credit memo",
            OperationArgs = new() { ["accountId"] = account.AccountId, ["amount"] = GuessDisputedAmount(email.Body), ["reason"] = "Customer-reported overcharge — pending finance review" },
            CustomerSummary = $"{account.Name} ({account.Tier}); contract {Money(account.ContractMonthly)}/mo.",
            ExpectedEffect = "A credit memo is raised (pending approval) to correct the disputed charge.",
            RiskLevel = "Medium", RequiresApproval = true,
            Rationale = "Billing disputes that move money require finance approval before commit."
        },
        "Cancellation Request" => new TaskPlan
        {
            PlannedTool = "churn.flag", Operation = "Flag churn risk + retention play",
            OperationArgs = new() { ["accountId"] = account.AccountId, ["severity"] = account.Tier is "Platinum" or "Gold" ? "High" : "Medium", ["reason"] = "Inbound cancellation/non-renewal notice" },
            CustomerSummary = $"{account.Name} ({account.Tier}); status {account.Status}; ARR {Money(account.AnnualValue)}.",
            ExpectedEffect = "Account flagged for retention; renewal owner notified.",
            RiskLevel = "High", Rationale = "Cancellation intent → trigger retention workflow and owner alert."
        },
        "Technical Issue" => new TaskPlan
        {
            PlannedTool = "case.create", Operation = "Open Tier-2 support case",
            OperationArgs = new() { ["accountId"] = account.AccountId, ["title"] = Trunc(email.Subject, 80), ["priority"] = triage.Urgency },
            CustomerSummary = $"{account.Name} ({account.Tier}); industry {account.Industry}.",
            ExpectedEffect = "A prioritised support case is opened and routed to Tier-2.",
            RiskLevel = "Low", Rationale = "Production/integration issue → open a case at the triaged priority."
        },
        "Sales Enquiry" => new TaskPlan
        {
            PlannedTool = "callback.create", Operation = "Schedule sales callback",
            OperationArgs = new() { ["accountId"] = account.AccountId, ["when"] = "within 1 business day", ["topic"] = "Expansion / enterprise pricing" },
            CustomerSummary = $"{account.Name} ({account.Tier}); owner {account.Owner}.",
            ExpectedEffect = "A sales callback is scheduled and the account owner is notified.",
            RiskLevel = "Low", Rationale = "Expansion intent → route to account owner with a callback."
        },
        "Complaint Escalation" => new TaskPlan
        {
            PlannedTool = "case.create", Operation = "Open escalation case + notify CSM",
            OperationArgs = new() { ["accountId"] = account.AccountId, ["title"] = "Escalation: " + Trunc(email.Subject, 70), ["priority"] = "P2" },
            CustomerSummary = $"{account.Name} ({account.Tier}); status {account.Status}.",
            ExpectedEffect = "An escalation case is opened and Customer Success is notified.",
            RiskLevel = "Medium", Rationale = "Complaint with churn/escalation markers → CSM ownership."
        },
        _ => new TaskPlan
        {
            PlannedTool = "case.create", Operation = "Open general case",
            OperationArgs = new() { ["accountId"] = account.AccountId, ["title"] = Trunc(email.Subject, 80), ["priority"] = triage.Urgency },
            CustomerSummary = $"{account.Name} ({account.Tier}).",
            ExpectedEffect = "A general support case is opened for handling.",
            RiskLevel = "Low", Rationale = "No specific intent operation; open a general case."
        }
    };

    private static string ExtractRef(string resultJson)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(resultJson);
            foreach (var prop in new[] { "caseId", "creditMemoId", "callbackId", "churnSignalId" })
                if (doc.RootElement.TryGetProperty(prop, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String)
                    return v.GetString() ?? "";
        }
        catch { }
        return "";
    }

    // ----------------------------------------------------------- 5. Outcome ---

    private OutcomeReport BuildOutcome(IncomingEmail email, EmailExtraction x, TriageResult triage, IntentDecision intent, TaskExecution task)
    {
        var audit = new List<AuditEntry>
        {
            new() { Step = "Email ingested", Detail = $"From {email.FromName} <{email.From}> into {email.Mailbox} via {email.Channel}." },
            new() { Step = "Extraction", Detail = $"Refs [{string.Join(", ", x.OrderRefs)}]; confidence {x.ExtractionConfidence:0.00}." },
            new() { Step = "Triage", Detail = $"{triage.Category} / {triage.Urgency}; sentiment {triage.Sentiment}; SLA {triage.SlaHours}h." },
            new() { Step = "Intent", Detail = intent.RequiresHuman ? $"Routed to human review — {intent.HumanReason}" : $"{intent.Intent} ({intent.IntentConfidence:0.00}) → {intent.SuggestedQueue}." }
        };
        foreach (var c in task.ToolCalls)
            audit.Add(new AuditEntry { Step = $"MCP {c.Tool}", Detail = c.ResultSummary });
        if (!string.IsNullOrWhiteSpace(task.Plan.Operation) && task.ExecutionStatus != "skipped")
            audit.Add(new AuditEntry { Step = "Operation", Detail = $"{task.Plan.Operation} → {task.ExecutionStatus}{(string.IsNullOrEmpty(task.OperationReference) ? "" : $" ({task.OperationReference})")}." });

        string finalStatus;
        if (triage.Category == "Spam") finalStatus = "Closed - spam";
        else if (intent.RequiresHuman) finalStatus = "Routed to human";
        else if (task.ExecutionStatus == "executed") finalStatus = "Action taken";
        else if (task.ExecutionStatus == "simulated") finalStatus = task.Plan.RequiresApproval ? "Pending approval" : "Action taken";
        else finalStatus = "Needs follow-up";

        return new OutcomeReport
        {
            FinalStatus = finalStatus,
            CustomerReplyDraft = DraftReply(email, triage, intent, task),
            ExecutiveSummary = BuildSummary(email, triage, intent, task, finalStatus),
            AuditTrail = audit,
            NextActions = NextActions(intent, task, finalStatus),
            SlaMet = triage.Urgency != "P1" || task.ExecutionStatus is "executed" or "simulated"
        };
    }

    private static string DraftReply(IncomingEmail email, TriageResult triage, IntentDecision intent, TaskExecution task)
    {
        if (triage.Category == "Spam") return "(No reply — message classified as spam.)";
        var name = string.IsNullOrWhiteSpace(email.FromName) ? "there" : email.FromName.Split(' ')[0];
        if (intent.RequiresHuman)
            return $"Hi {name},\n\nThanks for reaching out. Your message has been received and assigned to a specialist who will review the details and follow up shortly.\n\nKind regards,\nCustomer Care";
        return intent.Intent switch
        {
            "Billing Dispute" => $"Hi {name},\n\nThanks for flagging this. We've reviewed the charge and raised a credit adjustment for the discrepancy, which is now pending finance approval. We'll confirm once it's applied.\n\nKind regards,\nBilling Operations",
            "Cancellation Request" => $"Hi {name},\n\nThanks for letting us know. We're sorry to hear you're considering leaving. A member of our team will reach out to confirm the process and discuss options, including data export.\n\nKind regards,\nRetention Desk",
            "Technical Issue" => $"Hi {name},\n\nThanks for reporting this. We've opened a prioritised support case and our Tier-2 engineers are investigating the errors you described. We'll update you with progress shortly.\n\nKind regards,\nTechnical Support",
            "Sales Enquiry" => $"Hi {name},\n\nGreat to hear you're looking to expand! We've scheduled a callback so our team can walk you through enterprise pricing and the analytics add-on.\n\nKind regards,\nAccount Management",
            "Complaint Escalation" => $"Hi {name},\n\nWe're sorry about your experience. This has been escalated to our Customer Success team, who will personally follow up with you today.\n\nKind regards,\nCustomer Success",
            _ => $"Hi {name},\n\nThanks for getting in touch. We've logged your request and will be in contact shortly.\n\nKind regards,\nCustomer Care"
        };
    }

    private static string BuildSummary(IncomingEmail email, TriageResult triage, IntentDecision intent, TaskExecution task, string finalStatus)
    {
        var who = task.Customer.Matched ? $"{task.Customer.AccountName} ({task.Customer.Tier})" : "an unmatched sender";
        return $"Inbound {triage.Category.ToLowerInvariant()} email from {who} was triaged {triage.Urgency} and routed as '{intent.Intent}'. " +
               (intent.RequiresHuman
                    ? $"Confidence was low/ambiguous so it was sent to human review ({intent.SuggestedQueue}). "
                    : $"The task agent used {task.ToolCalls.Count} D365 MCP call(s) and executed '{task.Plan.Operation}'. ") +
               $"Final status: {finalStatus}.";
    }

    private static List<string> NextActions(IntentDecision intent, TaskExecution task, string finalStatus)
    {
        var list = new List<string>();
        if (intent.RequiresHuman) { list.Add($"Reviewer to confirm intent in the {intent.SuggestedQueue} queue."); list.Add("Re-run task execution once intent is confirmed."); return list; }
        if (task.Plan.RequiresApproval) list.Add($"Finance to approve {task.Plan.Operation.ToLowerInvariant()} {task.OperationReference}.");
        if (task.Customer.Matched && task.Customer.Status == "At Risk") list.Add("Account owner to review at-risk status.");
        list.Add("Send the drafted customer reply.");
        if (list.Count == 1) list.Add("Monitor for customer response.");
        return list;
    }
}
