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
    public async Task Workbook_has_expected_sheets_and_formulas()
    {
        var job = await SampleJobAsync();
        var bytes = new ExcelReportGenerator().Generate(job);

        using var ms = new MemoryStream(bytes);
        using var wb = new XLWorkbook(ms);

        foreach (var name in new[] { "Summary", "Cost Model", "Requirements", "Scope", "Documents" })
            Assert.True(wb.Worksheets.Contains(name), $"missing sheet: {name}");

        var cost = wb.Worksheet("Cost Model");
        // Subtotal cell B100 must be a SUM formula referenced by the Summary sheet.
        Assert.True(cost.Cell(100, 2).HasFormula);
        Assert.Contains("SUM", cost.Cell(100, 2).FormulaA1, StringComparison.OrdinalIgnoreCase);

        // At least one line item row has a quantity*unitprice formula in column I.
        Assert.True(cost.Cell(2, 9).HasFormula);
        Assert.Contains("F2*G2", cost.Cell(2, 9).FormulaA1);
    }
}
