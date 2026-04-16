using Json.Logic;
using Json.More;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Ric.Operators;

/// <summary>
/// Handles the `coalesce` operation. Returns the first non-null value from a list of expressions.
/// Unlike `or` (which uses truthiness — skipping 0, "", and []), coalesce only skips null values.
/// Parameters: [expr1, expr2, ...].
/// </summary>
[Operator("coalesce")]
[JsonConverter(typeof(CoalesceJsonConverter))]
public class CoalesceRule : Json.Logic.Rule
{
    internal List<Json.Logic.Rule> Items { get; }

    public CoalesceRule(Json.Logic.Rule first, params Json.Logic.Rule[] rest)
    {
        Items = new List<Json.Logic.Rule> { first };
        Items.AddRange(rest);
    }

    public override JsonNode? Apply(JsonNode? data, JsonNode? contextData = null)
    {
        foreach (var item in Items)
        {
            var result = item.Apply(data, contextData);
            if (result is not null)
                return result;
        }

        return null;
    }
}

internal class CoalesceJsonConverter : WeaklyTypedJsonConverter<CoalesceRule>
{
    public override CoalesceRule? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var parameters = reader.TokenType == JsonTokenType.StartArray
            ? options.ReadArray(ref reader, RicSerializerContext.Default.Rule)
            : new[] { options.Read(ref reader, RicSerializerContext.Default.Rule)! };

        if (parameters == null || parameters.Length == 0)
            throw new JsonException("The coalesce rule needs at least one parameter.");

        return new CoalesceRule(parameters[0], parameters.Skip(1).ToArray());
    }

    public override void Write(Utf8JsonWriter writer, CoalesceRule value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}
