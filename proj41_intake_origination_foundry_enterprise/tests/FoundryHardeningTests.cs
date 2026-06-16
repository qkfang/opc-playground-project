using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Proj41.Underwriting.Web.Models;
using Proj41.Underwriting.Web.Services.Foundry;
using Xunit;

namespace Proj41.Underwriting.Tests;

/// <summary>
/// Deterministic tests for the live-Foundry hardening: the tolerant JSON parsing that stops the
/// live agent path from being forced onto the offline fallback by string/currency/unit numbers,
/// and the active Foundry health probe (offline mode, no Azure needed).
/// </summary>
public class FoundryHardeningTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public FoundryHardeningTests(WebApplicationFactory<Program> factory) => _factory = factory;

    // ---- lenient JSON parsing (the root-cause fix for live JsonException fallbacks) ----

    private sealed class Probe
    {
        public decimal? Limit { get; set; }
        public int? Employees { get; set; }
        public double Confidence { get; set; }
        public bool Appointed { get; set; }
        public DateTimeOffset? EffectiveDate { get; set; }
    }

    [Theory]
    [InlineData("{\"limit\":\"$10M\"}", 10_000_000)]
    [InlineData("{\"limit\":\"10,000,000\"}", 10_000_000)]
    [InlineData("{\"limit\":\"1.2M\"}", 1_200_000)]
    [InlineData("{\"limit\":\"750k\"}", 750_000)]
    [InlineData("{\"limit\":\"$85 million\"}", 85_000_000)]
    [InlineData("{\"limit\":50000000}", 50_000_000)]
    [InlineData("{\"limit\":\"unknown\"}", null)]
    [InlineData("{\"limit\":null}", null)]
    public void Money_strings_are_coerced(string json, object? expected)
    {
        var p = JsonSerializer.Deserialize<Probe>(json, LenientJson.Options)!;
        if (expected is null) Assert.Null(p.Limit);
        else Assert.Equal(Convert.ToDecimal(expected), p.Limit);
    }

    [Theory]
    [InlineData("{\"appointed\":\"true\"}", true)]
    [InlineData("{\"appointed\":\"yes\"}", true)]
    [InlineData("{\"appointed\":\"Appointed\"}", true)]
    [InlineData("{\"appointed\":\"no\"}", false)]
    [InlineData("{\"appointed\":\"unknown\"}", false)]
    [InlineData("{\"appointed\":true}", true)]
    [InlineData("{\"appointed\":null}", false)]
    public void Bool_strings_are_coerced(string json, bool expected)
    {
        // This was the exact live failure: producer.appointed returned as a string by the model.
        var p = JsonSerializer.Deserialize<Probe>(json, LenientJson.Options)!;
        Assert.Equal(expected, p.Appointed);
    }

    [Fact]
    public void Int_and_double_strings_are_coerced()
    {
        var p = JsonSerializer.Deserialize<Probe>("{\"employees\":\"240\",\"confidence\":\"0.92\"}", LenientJson.Options)!;
        Assert.Equal(240, p.Employees);
        Assert.Equal(0.92d, p.Confidence, 3);
    }

    [Fact]
    public void Trailing_commas_and_iso_dates_are_handled()
    {
        var p = JsonSerializer.Deserialize<Probe>("{\"effectiveDate\":\"2026-08-01\",\"employees\":12,}", LenientJson.Options)!;
        Assert.Equal(12, p.Employees);
        Assert.Equal(2026, p.EffectiveDate!.Value.Year);
        Assert.Equal(8, p.EffectiveDate!.Value.Month);
    }

    [Fact]
    public void Full_extracted_records_with_llm_style_numbers_deserialize()
    {
        // Mirrors the exact shape that previously threw JsonException from a live agent response.
        const string json = """
        {
          "producer": { "contactName": "Alex Carter", "brokerTier": "National", "appointed": "yes", "confidence": "0.9" },
          "insured":  { "companyName": "Vertex Payments Pty Ltd", "industry": "Financial Technology",
                        "employeeCount": "240", "annualRevenue": "$60M", "totalInsurableValue": null,
                        "locationCount": "1", "yearsInBusiness": "8", "confidence": "0.88" },
          "submission": { "lineOfBusiness": "Cyber", "requestedLimit": "$10M", "deductible": "50,000",
                          "effectiveDate": "2026-08-01", "submissionType": "New Business", "confidence": "0.9" },
          "missingForUnderwriting": ["SOC2 report"]
        }
        """;
        var rec = JsonSerializer.Deserialize<ExtractedRecords>(json, LenientJson.Options)!;
        Assert.Equal("Vertex Payments Pty Ltd", rec.Insured.CompanyName);
        Assert.Equal(240, rec.Insured.EmployeeCount);
        Assert.Equal(60_000_000m, rec.Insured.AnnualRevenue);
        Assert.Equal(10_000_000m, rec.Submission.RequestedLimit);
        Assert.Equal(50_000m, rec.Submission.Deductible);
        Assert.Equal("Cyber", rec.Submission.LineOfBusiness);
        Assert.True(rec.Producer.Appointed);
    }

    // ---- active Foundry health probe (offline mode: no Azure dependency) ----

    [Fact]
    public async Task Foundry_health_probe_reports_offline_mode_ok()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/health/foundry");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode); // offline-by-design is healthy
        var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ok", doc.GetProperty("status").GetString());
        Assert.Equal("offline", doc.GetProperty("foundryMode").GetString());
        Assert.False(doc.GetProperty("foundryLive").GetBoolean());
        Assert.False(doc.GetProperty("foundryConfigured").GetBoolean());
    }

    [Fact]
    public async Task Health_exposes_foundry_mode_field()
    {
        var client = _factory.CreateClient();
        var doc = await client.GetFromJsonAsync<JsonElement>("/api/health");
        Assert.Equal("offline", doc.GetProperty("foundryMode").GetString());
        Assert.False(doc.GetProperty("foundryEnabled").GetBoolean());
    }
}
