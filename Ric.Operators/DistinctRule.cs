using Json.Logic;
using Json.More;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Ric.Operators;

/// <summary>
/// Handles the `distinct` operation. Removes duplicate values from an array.
/// Uses deep equality comparison (IsEquivalentTo) consistent with set operators.
/// </summary>
[Operator("distinct")]
[JsonConverter(typeof(DistinctJsonConverter))]
public class DistinctRule : Json.Logic.Rule
{
    internal Json.Logic.Rule Input { get; }

    public DistinctRule(Json.Logic.Rule input)
    {
        Input = input;
    }

    public override JsonNode? Apply(JsonNode? data, JsonNode? contextData = null)
    {
        var input = Input.Apply(data, contextData);

        if (input is null) return new JsonArray();
        if (input is not JsonArray arr)
            throw new JsonLogicException("The distinct rule requires a JSON array as input.");

        var result = new JsonArray();
        foreach (var item in arr)
        {
            var copy = item?.DeepClone();
            if (!result.Any(x => x.IsEquivalentTo(copy)))
            {
                result.Add(copy);
            }
        }

        return result;
    }
}

internal class DistinctJsonConverter : WeaklyTypedJsonConverter<DistinctRule>
{
    public override DistinctRule? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var parameters = reader.TokenType == JsonTokenType.StartArray
            ? options.ReadArray(ref reader, RicSerializerContext.Default.Rule)
            : new[] { options.Read(ref reader, RicSerializerContext.Default.Rule)! };

        if (parameters is not { Length: 1 })
            throw new JsonException("The distinct rule needs an array with 1 parameter.");

        return new DistinctRule(parameters[0]);
    }

    public override void Write(Utf8JsonWriter writer, DistinctRule value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}
