using ClosedXML.Excel;
using Proj37.CostEstimator.Web.Models;

namespace Proj37.CostEstimator.Web.Services;

/// <summary>
/// Renders an <see cref="EstimationResult"/> into a multi-sheet Excel workbook with live formulas, so
/// reviewers can adjust quantities/unit prices and see totals recalculate. This is the primary
/// deliverable of the POC ("Excel calculation pages").
///
/// Sheets:
///   1. Summary        — headline numbers + metadata
///   2. Cost Model     — per-service line items with QUANTITY*UNITPRICE formulas, subtotal, contingency, totals
///   3. Requirements   — derived technical requirements
///   4. Scope          — scope summary (in/out/assumptions)
///   5. Documents      — ingested source documents
/// </summary>
public sealed class ExcelReportGenerator
{
    private static readonly XLColor HeaderFill = XLColor.FromHtml("#0B5394");
    private static readonly XLColor HeaderText = XLColor.White;
    private static readonly XLColor AccentFill = XLColor.FromHtml("#E8F0FE");
    private static readonly XLColor TotalFill = XLColor.FromHtml("#FFF2CC");

    public byte[] Generate(EstimationResult r)
    {
        using var wb = new XLWorkbook();
        BuildSummarySheet(wb, r);
        var costSheet = BuildCostSheet(wb, r);
        BuildRequirementsSheet(wb, r);
        BuildScopeSheet(wb, r);
        BuildDocumentsSheet(wb, r);

        wb.Worksheet("Summary").SetTabActive();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void BuildSummarySheet(XLWorkbook wb, EstimationResult r)
    {
        var ws = wb.Worksheets.Add("Summary");
        ws.Column(1).Width = 28;
        ws.Column(2).Width = 60;

        Title(ws, "A1:B1", "Azure Cost Estimation — POC");
        ws.Cell("A2").Value = "Generated (UTC)";
        ws.Cell("B2").Value = r.CreatedUtc.UtcDateTime;
        ws.Cell("B2").Style.DateFormat.Format = "yyyy-mm-dd hh:mm";

        var rows = new (string Label, XLCellValue Value)[]
        {
            ("Project", r.Scope.ProjectName),
            ("Job ID", r.JobId),
            ("Estimation engine", r.Engine),
            ("Workload profile", r.Scope.WorkloadProfile),
            ("Expected scale", r.Scope.ExpectedScale),
            ("Data sensitivity", r.Scope.DataSensitivity),
            ("Environment", r.Scope.Environment),
            ("Region", r.Cost.Region),
            ("Currency", r.Cost.Currency),
            ("Documents ingested", r.Documents.Count),
            ("Technical requirements", r.Requirements.Count),
            ("Cost line items", r.Cost.LineItems.Count),
        };
        int row = 4;
        foreach (var (label, value) in rows)
        {
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 2).Value = value;
            row++;
        }

        row++;
        SectionHeader(ws, $"A{row}:B{row}", "Headline cost (reference, not a quote)");
        row++;
        // Pull totals from the Cost Model sheet so Summary tracks edits.
        ws.Cell(row, 1).Value = "Monthly subtotal"; ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 2).FormulaA1 = "='Cost Model'!B100"; MoneyCell(ws.Cell(row, 2), r.Cost.Currency); row++;
        ws.Cell(row, 1).Value = $"Contingency ({r.Cost.ContingencyPercent}%)"; ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 2).FormulaA1 = "='Cost Model'!B101"; MoneyCell(ws.Cell(row, 2), r.Cost.Currency); row++;
        ws.Cell(row, 1).Value = "Monthly total (incl. contingency)"; ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 2).FormulaA1 = "='Cost Model'!B102"; MoneyCell(ws.Cell(row, 2), r.Cost.Currency);
        ws.Range($"A{row}:B{row}").Style.Fill.BackgroundColor = TotalFill; row++;
        ws.Cell(row, 1).Value = "Annual total (incl. contingency)"; ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 2).FormulaA1 = "='Cost Model'!B103"; MoneyCell(ws.Cell(row, 2), r.Cost.Currency);
        ws.Range($"A{row}:B{row}").Style.Fill.BackgroundColor = TotalFill; row += 2;

        SectionHeader(ws, $"A{row}:B{row}", "Disclaimer");
        row++;
        ws.Cell(row, 1).Value = "Note";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 2).Value = r.Cost.PricingBasis + ". Figures are POC reference estimates derived from supplied documents and should be validated against the Azure Pricing Calculator / Retail Prices API before any commitment.";
        ws.Cell(row, 2).Style.Alignment.WrapText = true;
        ws.Row(row).Height = 60;
    }

    private static IXLWorksheet BuildCostSheet(XLWorkbook wb, EstimationResult r)
    {
        var ws = wb.Worksheets.Add("Cost Model");
        string[] headers = { "Category", "Service", "SKU / Tier", "Meter", "Assumption", "Quantity", "Unit Price", "Unit", "Monthly Cost" };
        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = headers[c];
            HeaderStyle(cell);
        }

        int row = 2;
        int firstDataRow = row;
        foreach (var li in r.Cost.LineItems.OrderBy(l => l.Category).ThenBy(l => l.Service))
        {
            ws.Cell(row, 1).Value = li.Category;
            ws.Cell(row, 2).Value = li.Service;
            ws.Cell(row, 3).Value = li.Sku;
            ws.Cell(row, 4).Value = li.Meter;
            ws.Cell(row, 5).Value = li.Assumption;
            ws.Cell(row, 6).Value = li.Quantity;
            ws.Cell(row, 7).Value = li.UnitPrice;
            MoneyCell(ws.Cell(row, 7), r.Cost.Currency, "#,##0.000000");
            ws.Cell(row, 8).Value = li.Unit;
            // Live formula: quantity * unit price
            ws.Cell(row, 9).FormulaA1 = $"=F{row}*G{row}";
            MoneyCell(ws.Cell(row, 9), r.Cost.Currency);
            if (row % 2 == 0) ws.Range(row, 1, row, 9).Style.Fill.BackgroundColor = AccentFill;
            row++;
        }
        int lastDataRow = row - 1;

        // Totals block anchored at fixed rows (100-103) referenced by the Summary sheet.
        const int subtotalRow = 100, contingencyRow = 101, totalRow = 102, annualRow = 103;
        ws.Cell(subtotalRow, 1).Value = "Monthly subtotal";
        ws.Cell(subtotalRow, 1).Style.Font.Bold = true;
        ws.Cell(subtotalRow, 2).FormulaA1 = $"=SUM(I{firstDataRow}:I{lastDataRow})";
        MoneyCell(ws.Cell(subtotalRow, 2), r.Cost.Currency);

        ws.Cell(contingencyRow, 1).Value = $"Contingency ({r.Cost.ContingencyPercent}%)";
        ws.Cell(contingencyRow, 1).Style.Font.Bold = true;
        ws.Cell(contingencyRow, 2).FormulaA1 = $"=B{subtotalRow}*{r.Cost.ContingencyPercent / 100m}";
        MoneyCell(ws.Cell(contingencyRow, 2), r.Cost.Currency);

        ws.Cell(totalRow, 1).Value = "Monthly total (incl. contingency)";
        ws.Cell(totalRow, 1).Style.Font.Bold = true;
        ws.Cell(totalRow, 2).FormulaA1 = $"=B{subtotalRow}+B{contingencyRow}";
        MoneyCell(ws.Cell(totalRow, 2), r.Cost.Currency);
        ws.Range(totalRow, 1, totalRow, 2).Style.Fill.BackgroundColor = TotalFill;

        ws.Cell(annualRow, 1).Value = "Annual total (incl. contingency)";
        ws.Cell(annualRow, 1).Style.Font.Bold = true;
        ws.Cell(annualRow, 2).FormulaA1 = $"=B{totalRow}*12";
        MoneyCell(ws.Cell(annualRow, 2), r.Cost.Currency);
        ws.Range(annualRow, 1, annualRow, 2).Style.Fill.BackgroundColor = TotalFill;

        // Notes
        int noteRow = annualRow + 2;
        ws.Cell(noteRow, 1).Value = "Notes";
        ws.Cell(noteRow, 1).Style.Font.Bold = true;
        foreach (var note in r.Cost.Notes)
        {
            noteRow++;
            ws.Cell(noteRow, 1).Value = "• " + note;
        }

        ws.Columns(1, 9).AdjustToContents();
        ws.Column(5).Width = Math.Min(ws.Column(5).Width, 45);
        ws.SheetView.FreezeRows(1);
        ws.Range(1, 1, lastDataRow, 9).SetAutoFilter();
        return ws;
    }

    private static void BuildRequirementsSheet(XLWorkbook wb, EstimationResult r)
    {
        var ws = wb.Worksheets.Add("Requirements");
        string[] headers = { "ID", "Category", "Priority", "Requirement", "Rationale" };
        for (int c = 0; c < headers.Length; c++) { var cell = ws.Cell(1, c + 1); cell.Value = headers[c]; HeaderStyle(cell); }

        int row = 2;
        foreach (var req in r.Requirements)
        {
            ws.Cell(row, 1).Value = req.Id;
            ws.Cell(row, 2).Value = req.Category;
            ws.Cell(row, 3).Value = req.Priority;
            ws.Cell(row, 4).Value = req.Requirement;
            ws.Cell(row, 5).Value = req.Rationale;
            if (row % 2 == 0) ws.Range(row, 1, row, 5).Style.Fill.BackgroundColor = AccentFill;
            row++;
        }
        ws.Columns(1, 5).AdjustToContents();
        ws.Column(4).Width = Math.Min(Math.Max(ws.Column(4).Width, 40), 70);
        ws.Column(5).Width = Math.Min(Math.Max(ws.Column(5).Width, 40), 70);
        ws.Column(4).Style.Alignment.WrapText = true;
        ws.Column(5).Style.Alignment.WrapText = true;
        ws.SheetView.FreezeRows(1);
        if (row > 2) ws.Range(1, 1, row - 1, 5).SetAutoFilter();
    }

    private static void BuildScopeSheet(XLWorkbook wb, EstimationResult r)
    {
        var ws = wb.Worksheets.Add("Scope");
        ws.Column(1).Width = 22; ws.Column(2).Width = 80;
        Title(ws, "A1:B1", "Scope Summary");

        int row = 3;
        void Field(string k, string v) { ws.Cell(row, 1).Value = k; ws.Cell(row, 1).Style.Font.Bold = true; ws.Cell(row, 2).Value = v; ws.Cell(row, 2).Style.Alignment.WrapText = true; row++; }
        void ListBlock(string k, List<string> items)
        {
            ws.Cell(row, 1).Value = k; ws.Cell(row, 1).Style.Font.Bold = true;
            if (items.Count == 0) { ws.Cell(row, 2).Value = "—"; row++; return; }
            foreach (var it in items) { ws.Cell(row, 2).Value = "• " + it; row++; }
            row++;
        }

        Field("Project", r.Scope.ProjectName);
        Field("Overview", r.Scope.Overview);
        Field("Business goal", r.Scope.BusinessGoal);
        Field("Workload profile", r.Scope.WorkloadProfile);
        Field("Expected scale", r.Scope.ExpectedScale);
        Field("Data sensitivity", r.Scope.DataSensitivity);
        Field("Environment", r.Scope.Environment);
        row++;
        ListBlock("In scope", r.Scope.InScope);
        ListBlock("Out of scope", r.Scope.OutOfScope);
        ListBlock("Assumptions", r.Scope.Assumptions);
    }

    private static void BuildDocumentsSheet(XLWorkbook wb, EstimationResult r)
    {
        var ws = wb.Worksheets.Add("Documents");
        string[] headers = { "File", "Content Type", "Size (bytes)", "Words", "Characters", "Excerpt" };
        for (int c = 0; c < headers.Length; c++) { var cell = ws.Cell(1, c + 1); cell.Value = headers[c]; HeaderStyle(cell); }
        int row = 2;
        foreach (var d in r.Documents)
        {
            ws.Cell(row, 1).Value = d.FileName;
            ws.Cell(row, 2).Value = d.ContentType;
            ws.Cell(row, 3).Value = d.SizeBytes;
            ws.Cell(row, 4).Value = d.WordCount;
            ws.Cell(row, 5).Value = d.CharacterCount;
            ws.Cell(row, 6).Value = d.Excerpt ?? "";
            row++;
        }
        ws.Columns(1, 6).AdjustToContents();
        ws.Column(6).Width = Math.Min(Math.Max(ws.Column(6).Width, 40), 80);
        ws.Column(6).Style.Alignment.WrapText = true;
        ws.SheetView.FreezeRows(1);
    }

    // ------- styling helpers -------
    private static void Title(IXLWorksheet ws, string range, string text)
    {
        ws.Range(range).Merge();
        var c = ws.Range(range).FirstCell();
        c.Value = text;
        c.Style.Font.Bold = true;
        c.Style.Font.FontSize = 16;
        c.Style.Font.FontColor = HeaderText;
        ws.Range(range).Style.Fill.BackgroundColor = HeaderFill;
        ws.Range(range).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ws.Row(ws.Range(range).FirstCell().Address.RowNumber).Height = 26;
    }

    private static void SectionHeader(IXLWorksheet ws, string range, string text)
    {
        ws.Range(range).Merge();
        var c = ws.Range(range).FirstCell();
        c.Value = text;
        c.Style.Font.Bold = true;
        c.Style.Font.FontColor = HeaderText;
        ws.Range(range).Style.Fill.BackgroundColor = HeaderFill;
    }

    private static void HeaderStyle(IXLCell cell)
    {
        cell.Style.Font.Bold = true;
        cell.Style.Font.FontColor = HeaderText;
        cell.Style.Fill.BackgroundColor = HeaderFill;
        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private static void MoneyCell(IXLCell cell, string currency, string format = "#,##0.00")
    {
        cell.Style.NumberFormat.Format = $"\"{CurrencySymbol(currency)}\"{format}";
    }

    private static string CurrencySymbol(string currency) => currency.ToUpperInvariant() switch
    {
        "USD" => "$",
        "AUD" => "A$",
        "EUR" => "€",
        "GBP" => "£",
        _ => currency + " "
    };
}
