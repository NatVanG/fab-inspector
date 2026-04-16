using Json.Logic;
using Json.More;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Ric.Operators;

/// <summary>
/// Handles the `now` operation. Returns the current UTC date/time as an ISO 8601 string,
/// optionally shifted by an offset to produce relative dates.
/// <para>
/// Parameters (all optional): [offset, unit].
/// </para>
/// <list type="bullet">
///   <item><c>{"now":[]}</c> → current UTC time, e.g. "2026-04-16T14:30:00.0000000Z"</item>
///   <item><c>{"now":[-2, "days"]}</c> → 2 days ago</item>
///   <item><c>{"now":[-5, "hours"]}</c> → 5 hours ago</item>
///   <item><c>{"now":[30, "minutes"]}</c> → 30 minutes from now</item>
/// </list>
/// Supported units: seconds, minutes, hours, days, months, years.
/// </summary>
[Operator("now")]
[JsonConverter(typeof(NowJsonConverter))]
public class NowRule : Json.Logic.Rule
{
    internal Json.Logic.Rule? Offset { get; }
    internal Json.Logic.Rule? Unit { get; }

    public NowRule(Json.Logic.Rule? offset = null, Json.Logic.Rule? unit = null)
    {
        Offset = offset;
        Unit = unit;
    }

    public override JsonNode? Apply(JsonNode? data, JsonNode? contextData = null)
    {
        var now = DateTimeOffset.UtcNow;

        if (Offset is null)
            return now.ToString("o", CultureInfo.InvariantCulture);

        var offsetNode = Offset.Apply(data, contextData);
        double offsetValue;
        if (offsetNode is JsonValue ov)
        {
            if (ov.TryGetValue(out int i)) offsetValue = i;
            else if (ov.TryGetValue(out long l)) offsetValue = l;
            else if (ov.TryGetValue(out double d)) offsetValue = d;
            else if (ov.TryGetValue(out string? s) && double.TryParse(s, CultureInfo.InvariantCulture, out double parsed))
                offsetValue = parsed;
            else throw new JsonLogicException("The now rule offset parameter must be numeric.");
        }
        else throw new JsonLogicException("The now rule offset parameter must be numeric.");

        string unit = "days"; // default unit
        if (Unit is not null)
        {
            var unitNode = Unit.Apply(data, contextData);
            if (unitNode is JsonValue uv && uv.TryGetValue(out string? unitStr))
                unit = unitStr.ToLowerInvariant();
            else
                throw new JsonLogicException("The now rule unit parameter must be a string.");
        }

        var result = unit switch
        {
            "seconds" or "second" or "s" => now.AddSeconds(offsetValue),
            "minutes" or "minute" or "min" => now.AddMinutes(offsetValue),
            "hours" or "hour" or "h" => now.AddHours(offsetValue),
            "days" or "day" or "d" => now.AddDays(offsetValue),
            "months" or "month" => now.AddMonths((int)offsetValue),
            "years" or "year" or "y" => now.AddYears((int)offsetValue),
            _ => throw new JsonLogicException($"The now rule does not support unit '{unit}'. Supported: seconds, minutes, hours, days, months, years.")
        };

        return result.ToString("o", CultureInfo.InvariantCulture);
    }
}

internal class NowJsonConverter : WeaklyTypedJsonConverter<NowRule>
{
    public override NowRule? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var parameters = reader.TokenType == JsonTokenType.StartArray
            ? options.ReadArray(ref reader, RicSerializerContext.Default.Rule)
            : new[] { options.Read(ref reader, RicSerializerContext.Default.Rule)! };

        if (parameters == null || parameters.Length == 0)
            return new NowRule();

        return new NowRule(
            parameters[0],
            parameters.Length > 1 ? parameters[1] : null);
    }

    public override void Write(Utf8JsonWriter writer, NowRule value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}
