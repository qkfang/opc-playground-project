using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Proj45.RelayDesk.Web.Services.Foundry;

/// <summary>
/// Tolerant JSON options for binding LLM output. Language models frequently emit numbers as
/// strings, with currency symbols, units ("$1.2M"), thousands separators, percentages, or
/// free-form dates. Strict System.Text.Json binding would throw and force the offline fallback
/// even when the agent answered correctly. These converters coerce common shapes.
/// </summary>
internal static class LenientJson
{
    public static readonly JsonSerializerOptions Options = Build();

    private static JsonSerializerOptions Build()
    {
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowNamedFloatingPointLiterals
        };
        o.Converters.Add(new LenientDoubleConverter());
        o.Converters.Add(new LenientNullableDoubleConverter());
        o.Converters.Add(new LenientIntConverter());
        o.Converters.Add(new LenientNullableIntConverter());
        o.Converters.Add(new LenientNullableDecimalConverter());
        o.Converters.Add(new LenientDateTimeOffsetConverter());
        o.Converters.Add(new LenientNullableDateTimeOffsetConverter());
        return o;
    }

    internal static double? ParseNumber(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = s.Trim();
        if (t.Equals("null", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("unknown", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("n/a", StringComparison.OrdinalIgnoreCase)) return null;

        bool percent = t.EndsWith("%");
        if (percent) t = t[..^1].Trim();

        double mult = 1d;
        char last = t.Length > 0 ? char.ToLowerInvariant(t[^1]) : '\0';
        if (last is 'k') { mult = 1_000d; t = t[..^1]; }
        else if (last is 'm') { mult = 1_000_000d; t = t[..^1]; }
        else if (last is 'b') { mult = 1_000_000_000d; t = t[..^1]; }

        var cleaned = new string(t.Where(c => char.IsDigit(c) || c == '.' || c == '-' || c == '+').ToArray());
        if (cleaned.Length == 0) return null;
        if (!double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return null;
        v *= mult;
        if (percent) v /= 100d;
        return v;
    }
}

internal sealed class LenientDoubleConverter : JsonConverter<double>
{
    public override double Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o)
    {
        if (reader.TokenType == JsonTokenType.Number) return reader.GetDouble();
        if (reader.TokenType == JsonTokenType.String) return LenientJson.ParseNumber(reader.GetString()) ?? 0d;
        if (reader.TokenType == JsonTokenType.Null) return 0d;
        reader.Skip();
        return 0d;
    }
    public override void Write(Utf8JsonWriter w, double v, JsonSerializerOptions o) => w.WriteNumberValue(v);
}

internal sealed class LenientNullableDoubleConverter : JsonConverter<double?>
{
    public override double? Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType == JsonTokenType.Number) return reader.GetDouble();
        if (reader.TokenType == JsonTokenType.String) return LenientJson.ParseNumber(reader.GetString());
        reader.Skip();
        return null;
    }
    public override void Write(Utf8JsonWriter w, double? v, JsonSerializerOptions o)
    {
        if (v is null) w.WriteNullValue(); else w.WriteNumberValue(v.Value);
    }
}

internal sealed class LenientIntConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o)
    {
        if (reader.TokenType == JsonTokenType.Number) return (int)Math.Round(reader.GetDouble());
        if (reader.TokenType == JsonTokenType.String) return (int)Math.Round(LenientJson.ParseNumber(reader.GetString()) ?? 0d);
        if (reader.TokenType == JsonTokenType.Null) return 0;
        reader.Skip();
        return 0;
    }
    public override void Write(Utf8JsonWriter w, int v, JsonSerializerOptions o) => w.WriteNumberValue(v);
}

internal sealed class LenientNullableIntConverter : JsonConverter<int?>
{
    public override int? Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType == JsonTokenType.Number) return (int)Math.Round(reader.GetDouble());
        if (reader.TokenType == JsonTokenType.String)
        {
            var n = LenientJson.ParseNumber(reader.GetString());
            return n is null ? null : (int)Math.Round(n.Value);
        }
        reader.Skip();
        return null;
    }
    public override void Write(Utf8JsonWriter w, int? v, JsonSerializerOptions o)
    {
        if (v is null) w.WriteNullValue(); else w.WriteNumberValue(v.Value);
    }
}

internal sealed class LenientNullableDecimalConverter : JsonConverter<decimal?>
{
    public override decimal? Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType == JsonTokenType.Number) return reader.GetDecimal();
        if (reader.TokenType == JsonTokenType.String)
        {
            var n = LenientJson.ParseNumber(reader.GetString());
            return n is null ? null : (decimal)n.Value;
        }
        reader.Skip();
        return null;
    }
    public override void Write(Utf8JsonWriter w, decimal? v, JsonSerializerOptions o)
    {
        if (v is null) w.WriteNullValue(); else w.WriteNumberValue(v.Value);
    }
}

internal sealed class LenientDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d)) return d;
        }
        return DateTimeOffset.UtcNow;
    }
    public override void Write(Utf8JsonWriter w, DateTimeOffset v, JsonSerializerOptions o) => w.WriteStringValue(v);
}

internal sealed class LenientNullableDateTimeOffsetConverter : JsonConverter<DateTimeOffset?>
{
    public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d)) return d;
        }
        reader.Skip();
        return null;
    }
    public override void Write(Utf8JsonWriter w, DateTimeOffset? v, JsonSerializerOptions o)
    {
        if (v is null) w.WriteNullValue(); else w.WriteStringValue(v.Value);
    }
}
