using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Proj41.Underwriting.Web.Services.Foundry;

/// <summary>
/// Tolerant System.Text.Json converters for parsing Foundry/LLM JSON responses.
///
/// Large language models frequently emit numbers as strings, with currency symbols, thousands
/// separators or magnitude units ("$10M", "10,000,000", "1.2M", "240"), and dates in free-form.
/// The strict default binder throws <see cref="JsonException"/> on these, which previously forced
/// every such stage onto the offline fallback. These converters coerce those shapes into the CLR
/// types the domain model expects so the live agent path is actually used.
/// </summary>
internal static class LenientJson
{
    internal static readonly JsonSerializerOptions Options = Build();

    private static JsonSerializerOptions Build()
    {
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowNamedFloatingPointLiterals
        };
        o.Converters.Add(new LenientDecimalNullableConverter());
        o.Converters.Add(new LenientDecimalConverter());
        o.Converters.Add(new LenientIntNullableConverter());
        o.Converters.Add(new LenientIntConverter());
        o.Converters.Add(new LenientDoubleConverter());
        o.Converters.Add(new LenientBoolConverter());
        o.Converters.Add(new LenientDateTimeOffsetNullableConverter());
        return o;
    }

    // ---- shared scalar coercion ----

    internal static decimal? ReadDecimal(ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.Number:
                return reader.GetDecimal();
            case JsonTokenType.String:
                return ParseMoney(reader.GetString());
            case JsonTokenType.True:
                return 1m;
            case JsonTokenType.False:
                return 0m;
            default:
                reader.Skip();
                return null;
        }
    }

    internal static long? ReadLong(ref Utf8JsonReader reader)
    {
        var d = ReadDecimal(ref reader);
        if (d is null) return null;
        return (long)Math.Round(d.Value, MidpointRounding.AwayFromZero);
    }

    /// <summary>Parses "$10M", "10,000,000", "1.2M", "750k", "60 million", "240" → decimal.</summary>
    internal static decimal? ParseMoney(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = s.Trim().ToLowerInvariant();
        if (t is "null" or "n/a" or "na" or "unknown" or "-" or "tbd" or "tbc") return null;

        decimal mult = 1m;
        // magnitude words
        if (t.Contains("billion") || t.EndsWith("bn") || t.EndsWith(" b")) mult = 1_000_000_000m;
        else if (t.Contains("million") || t.EndsWith(" m")) mult = 1_000_000m;
        else if (t.Contains("thousand")) mult = 1_000m;

        // strip currency words/symbols and magnitude words
        t = t.Replace("usd", " ").Replace("aud", " ").Replace("eur", " ").Replace("gbp", " ")
             .Replace("billion", " ").Replace("million", " ").Replace("thousand", " ")
             .Replace("$", " ").Replace("\u20ac", " ").Replace("\u00a3", " ")
             .Replace("per year", " ").Replace("per annum", " ").Replace("pa", " ").Replace("/yr", " ");

        // single trailing magnitude suffix directly on the number, e.g. 10m / 1.2bn / 750k
        var sm = System.Text.RegularExpressions.Regex.Match(t, @"(-?\d[\d,]*(?:\.\d+)?)\s*(k|m|bn|b)\b");
        if (sm.Success)
        {
            var num = sm.Groups[1].Value.Replace(",", "");
            if (decimal.TryParse(num, NumberStyles.Any, CultureInfo.InvariantCulture, out var bv))
            {
                var unit = sm.Groups[2].Value;
                decimal um = unit switch { "k" => 1_000m, "m" => 1_000_000m, "b" or "bn" => 1_000_000_000m, _ => 1m };
                return bv * um * (mult == 1m ? 1m : 1m); // suffix wins; word-mult already excluded by strip
            }
        }

        // first numeric run anywhere
        var nm = System.Text.RegularExpressions.Regex.Match(t, @"-?\d[\d,]*(?:\.\d+)?");
        if (nm.Success && decimal.TryParse(nm.Value.Replace(",", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
            return v * mult;

        return null;
    }
}

internal sealed class LenientDecimalNullableConverter : JsonConverter<decimal?>
{
    public override decimal? Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o) => LenientJson.ReadDecimal(ref reader);
    public override void Write(Utf8JsonWriter w, decimal? v, JsonSerializerOptions o)
    { if (v is null) w.WriteNullValue(); else w.WriteNumberValue(v.Value); }
}

internal sealed class LenientDecimalConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o) => LenientJson.ReadDecimal(ref reader) ?? 0m;
    public override void Write(Utf8JsonWriter w, decimal v, JsonSerializerOptions o) => w.WriteNumberValue(v);
}

internal sealed class LenientIntNullableConverter : JsonConverter<int?>
{
    public override int? Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o)
    {
        var l = LenientJson.ReadLong(ref reader);
        if (l is null) return null;
        return (int)Math.Clamp(l.Value, int.MinValue, int.MaxValue);
    }
    public override void Write(Utf8JsonWriter w, int? v, JsonSerializerOptions o)
    { if (v is null) w.WriteNullValue(); else w.WriteNumberValue(v.Value); }
}

internal sealed class LenientIntConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o)
    {
        var l = LenientJson.ReadLong(ref reader) ?? 0;
        return (int)Math.Clamp(l, int.MinValue, int.MaxValue);
    }
    public override void Write(Utf8JsonWriter w, int v, JsonSerializerOptions o) => w.WriteNumberValue(v);
}

internal sealed class LenientDoubleConverter : JsonConverter<double>
{
    public override double Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o)
    {
        var d = LenientJson.ReadDecimal(ref reader);
        return d is null ? 0d : (double)d.Value;
    }
    public override void Write(Utf8JsonWriter w, double v, JsonSerializerOptions o) => w.WriteNumberValue(v);
}

internal sealed class LenientBoolConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.True: return true;
            case JsonTokenType.False: return false;
            case JsonTokenType.Null: return false;
            case JsonTokenType.Number:
                return reader.TryGetDecimal(out var n) && n != 0m;
            case JsonTokenType.String:
                var s = reader.GetString()?.Trim().ToLowerInvariant();
                return s is "true" or "yes" or "y" or "1" or "t" or "appointed" or "known";
            default:
                reader.Skip();
                return false;
        }
    }
    public override void Write(Utf8JsonWriter w, bool v, JsonSerializerOptions o) => w.WriteBooleanValue(v);
}

internal sealed class LenientDateTimeOffsetNullableConverter : JsonConverter<DateTimeOffset?>
{
    public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto)) return dto;
            return null;
        }
        // Unexpected shape: skip without throwing.
        reader.Skip();
        return null;
    }
    public override void Write(Utf8JsonWriter w, DateTimeOffset? v, JsonSerializerOptions o)
    { if (v is null) w.WriteNullValue(); else w.WriteStringValue(v.Value); }
}
