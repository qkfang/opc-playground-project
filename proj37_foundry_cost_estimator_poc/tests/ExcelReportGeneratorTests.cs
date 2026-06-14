using Xunit;
using ClosedXML.Excel;
using Proj37.CostEstimator.Web.Models;
using Proj37.CostEstimator.Web.Services;

namespace Proj37.CostEstimator.Tests;

public class ExcelReportGeneratorTests
{
    private static async Task<EstimationResult> SampleJobAsync()
    {
        var job = new EstimationResult();
        job.Documents.Add(new IngestedDocument { FileName = "brief.md", ExtractedText = "AI web app with API, Foundry agent, file search, SQL.", WordCount = 9, Excerpt = "AI web app..." });
        return await new OfflineEstimationEngine().EstimateAsync(job);
    }

    [Fact]
    public async Task Generate_returns_nonempty_xlsx_bytes()
    {
        var job = await SampleJobAsync();
        var bytes = new ExcelReportGenerator().Generate(job);
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 2000, $"workbook unexpectedly small: {bytes.Length} bytes");
        // XLSX is a zip; first two bytes are 'PK'.
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);
    }

    [Fact]
    public async Task Workbook_has_expected_sheets()
    {
        var job = await SampleJobAsync();
        var bytes = new ExcelReportGenerator().Generate(job);

        using var ms = new MemoryStream(bytes);
        using var wb = new XLWorkbook(ms);

        foreach (var name in new[] { "Summary", "Cost Model", "Requirements", "Scope", "Documents" })
            Assert.True(wb.Worksheets.Contains(name), $"missing sheet: {name}");
    }

    [Fact]
    public async Task CostModel_is_an_editable_table_with_formula_column_and_totals_row()
    {
        var job = await SampleJobAsync();
        var bytes = new ExcelReportGenerator().Generate(job);

        using var ms = new MemoryStream(bytes);
        using var wb = new XLWorkbook(ms);
        var cost = wb.Worksheet("Cost Model");

        // The line items must live in a native Excel Table named "CostModel".
        var table = Assert.Single(cost.Tables);
        Assert.Equal("CostModel", table.Name);

        // Quantity / Unit Price must be plain editable input cells (no formula); Monthly Cost is derived.
        var qtyHeader = table.Field("Quantity").Column.ColumnNumber();
        var priceHeader = table.Field("Unit Price").Column.ColumnNumber();
        var monthlyHeader = table.Field("Monthly Cost").Column.ColumnNumber();
        var firstData = table.DataRange.FirstRow().RowNumber();

        Assert.False(cost.Cell(firstData, qtyHeader).HasFormula, "Quantity must be a directly editable value, not a formula");
        Assert.False(cost.Cell(firstData, priceHeader).HasFormula, "Unit Price must be a directly editable value, not a formula");

        // Monthly Cost is a per-row structured-reference formula = Quantity * Unit Price.
        var monthlyCell = cost.Cell(firstData, monthlyHeader);
        Assert.True(monthlyCell.HasFormula, "Monthly Cost must be a formula");
        Assert.Contains("[@Quantity]", monthlyCell.FormulaA1);
        Assert.Contains("[@[Unit Price]]", monthlyCell.FormulaA1);

        // The table has a totals row that SUMs Monthly Cost (SUBTOTAL auto-includes new/inserted rows).
        Assert.True(table.ShowTotalsRow, "Cost Model table must show a totals row");
        Assert.Equal(XLTotalsRowFunction.Sum, table.Field("Monthly Cost").TotalsRowFunction);
        var totalsCell = table.TotalsRow().Cell(monthlyHeader);
        Assert.True(totalsCell.HasFormula);
        Assert.Contains("SUBTOTAL", totalsCell.FormulaA1, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CostModel[Monthly Cost]", totalsCell.FormulaA1);
    }

    [Fact]
    public async Task Derived_totals_and_defined_names_track_the_table_total()
    {
        var job = await SampleJobAsync();
        var bytes = new ExcelReportGenerator().Generate(job);

        using var ms = new MemoryStream(bytes);
        using var wb = new XLWorkbook(ms);

        // Workbook defined names exist for the four headline figures so the Summary survives table growth.
        foreach (var n in new[] { "Proj37_MonthlySubtotal", "Proj37_Contingency", "Proj37_MonthlyTotal", "Proj37_AnnualTotal" })
            Assert.True(wb.DefinedNames.TryGetValue(n, out _), $"missing defined name: {n}");

        var cost = wb.Worksheet("Cost Model");
        // Contingency + monthly total reference the table's totals cell via a structured reference.
        var contingency = wb.DefinedNames.ToList().First(d => d.Name == "Proj37_Contingency").Ranges.Single().FirstCell();
        var monthlyTotal = wb.DefinedNames.ToList().First(d => d.Name == "Proj37_MonthlyTotal").Ranges.Single().FirstCell();
        Assert.Contains("CostModel[[#Totals],[Monthly Cost]]", contingency.FormulaA1);
        Assert.Contains("CostModel[[#Totals],[Monthly Cost]]", monthlyTotal.FormulaA1);

        // Summary sheet references the defined names (not hard-coded cell addresses).
        var summary = wb.Worksheet("Summary");
        var summaryFormulas = summary.CellsUsed(c => c.HasFormula).Select(c => c.FormulaA1).ToList();
        Assert.Contains(summaryFormulas, f => f.Contains("Proj37_MonthlySubtotal"));
        Assert.Contains(summaryFormulas, f => f.Contains("Proj37_AnnualTotal"));
    }

    [Fact]
    public async Task Workbook_emits_calcChain_so_formulas_are_live()
    {
        var job = await SampleJobAsync();
        var bytes = new ExcelReportGenerator().Generate(job);

        // calcChain.xml present => Excel will recalc the live formulas on open / on edit.
        using var ms = new MemoryStream(bytes);
        using var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read);
        Assert.Contains(zip.Entries, e => e.FullName.Equals("xl/calcChain.xml", StringComparison.OrdinalIgnoreCase));
        // And the table part must be present.
        Assert.Contains(zip.Entries, e => e.FullName.StartsWith("xl/tables/", StringComparison.OrdinalIgnoreCase));
    }
}
