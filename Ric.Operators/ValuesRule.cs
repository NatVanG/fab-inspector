using Json.Logic;
using Json.More;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Ric.Operators;

/// <summary>
/// Handles the `values` operation. Returns the property values of a JSON object as an array.
/// </summary>
[Operator("values")]
[JsonConverter(typeof(ValuesJsonConverter))]
public class ValuesRule : Json.Logic.Rule
{
    internal Json.Logic.Rule Input { get; }

    public ValuesRule(Json.Logic.Rule input)
    {
        Input = input;
    }

    public override JsonNode? Apply(JsonNode? data, JsonNode? contextData = null)
    {
        var input = Input.Apply(data, contextData);

        if (input is null) return new JsonArray();
        if (input is not JsonObject obj)
            throw new JsonLogicException("The values rule requires a JSON object as input.");

        var result = new JsonArray();
        foreach (var property in obj)
        {
            result.Add(property.Value?.DeepClone());
        }

        return result;
    }
}

internal class ValuesJsonConverter : WeaklyTypedJsonConverter<ValuesRule>
{
    public override ValuesRule? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var parameters = reader.TokenType == JsonTokenType.StartArray
            ? options.ReadArray(ref reader, RicSerializerContext.Default.Rule)
            : new[] { options.Read(ref reader, RicSerializerContext.Default.Rule)! };

        if (parameters is not { Length: 1 })
            throw new JsonException("The values rule needs an array with 1 parameter.");

        return new ValuesRule(parameters[0]);
    }

    public override void Write(Utf8JsonWriter writer, ValuesRule value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}
