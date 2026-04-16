using Json.Logic;
using Json.More;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Ric.Operators;

/// <summary>
/// Handles the `datediff` operation. Computes the difference between two ISO 8601 date strings.
/// Returns a numeric value in the specified unit (default: days).
/// <para>
/// Parameters: [date1, date2, unit?].
/// </para>
/// <list type="bullet">
///   <item><c>{"datediff":["2026-04-14T00:00:00Z", "2026-04-16T00:00:00Z"]}</c> → 2 (days)</item>
///   <item><c>{"datediff":["2026-04-16T10:00:00Z", "2026-04-16T15:30:00Z", "hours"]}</c> → 5.5</item>
/// </list>
/// The result is <c>date2 - date1</c>, so a positive value means date2 is after date1.
/// Supported units: seconds, minutes, hours, days (default).
/// </summary>
[Operator("datediff")]
[JsonConverter(typeof(DateDiffJsonConverter))]
public class DateDiffRule : Json.Logic.Rule
{
    internal Json.Logic.Rule Date1 { get; }
    internal Json.Logic.Rule Date2 { get; }
    internal Json.Logic.Rule? Unit { get; }

    public DateDiffRule(Json.Logic.Rule date1, Json.Logic.Rule date2, Json.Logic.Rule? unit = null)
    {
        Date1 = date1;
        Date2 = date2;
        Unit = unit;
    }

    public override JsonNode? Apply(JsonNode? data, JsonNode? contextData = null)
    {
        var date1Node = Date1.Apply(data, contextData);
        var date2Node = Date2.Apply(data, contextData);

        var dt1 = ParseDate(date1Node, "date1");
        var dt2 = ParseDate(date2Node, "date2");

        var diff = dt2 - dt1;

        string unit = "days";
        if (Unit is not null)
        {
            var unitNode = Unit.Apply(data, contextData);
            if (unitNode is JsonValue uv && uv.TryGetValue(out string? unitStr))
                unit = unitStr.ToLowerInvariant();
            else
                throw new JsonLogicException("The datediff rule unit parameter must be a string.");
        }

        double result = unit switch
        {
            "seconds" or "second" or "s" => diff.TotalSeconds,
            "minutes" or "minute" or "min" => diff.TotalMinutes,
            "hours" or "hour" or "h" => diff.TotalHours,
            "days" or "day" or "d" => diff.TotalDays,
            _ => throw new JsonLogicException($"The datediff rule does not support unit '{unit}'. Supported: seconds, minutes, hours, days.")
        };

        // Return as integer if it's a whole number, otherwise as double
        if (result == Math.Floor(result))
            return (int)result;

        return Math.Round(result, 6);
    }

    private static DateTimeOffset ParseDate(JsonNode? node, string paramName)
    {
        if (node is JsonValue val && val.TryGetValue(out string? dateStr))
        {
            if (DateTimeOffset.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
                return dto;

            throw new JsonLogicException($"The datediff rule could not parse '{dateStr}' as a date for parameter '{paramName}'.");
        }

        throw new JsonLogicException($"The datediff rule requires an ISO 8601 date string for parameter '{paramName}'.");
    }
}

internal class DateDiffJsonConverter : WeaklyTypedJsonConverter<DateDiffRule>
{
    public override DateDiffRule? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var parameters = reader.TokenType == JsonTokenType.StartArray
            ? options.ReadArray(ref reader, RicSerializerContext.Default.Rule)
            : new[] { options.Read(ref reader, RicSerializerContext.Default.Rule)! };

        if (parameters == null || parameters.Length < 2)
            throw new JsonException("The datediff rule needs at least 2 parameters: [date1, date2, unit?].");

        return new DateDiffRule(
            parameters[0],
            parameters[1],
            parameters.Length > 2 ? parameters[2] : null);
    }

    public override void Write(Utf8JsonWriter writer, DateDiffRule value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}
