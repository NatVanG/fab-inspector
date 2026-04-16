using Json.Logic;
using Json.More;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Ric.Operators;

/// <summary>
/// Handles the `keys` operation. Returns the property names of a JSON object as an array.
/// </summary>
[Operator("keys")]
[JsonConverter(typeof(KeysJsonConverter))]
public class KeysRule : Json.Logic.Rule
{
    internal Json.Logic.Rule Input { get; }

    public KeysRule(Json.Logic.Rule input)
    {
        Input = input;
    }

    public override JsonNode? Apply(JsonNode? data, JsonNode? contextData = null)
    {
        var input = Input.Apply(data, contextData);

        if (input is null) return new JsonArray();
        if (input is not JsonObject obj)
            throw new JsonLogicException("The keys rule requires a JSON object as input.");

        var result = new JsonArray();
        foreach (var property in obj)
        {
            result.Add(JsonValue.Create(property.Key));
        }

        return result;
    }
}

internal class KeysJsonConverter : WeaklyTypedJsonConverter<KeysRule>
{
    public override KeysRule? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var parameters = reader.TokenType == JsonTokenType.StartArray
            ? options.ReadArray(ref reader, RicSerializerContext.Default.Rule)
            : new[] { options.Read(ref reader, RicSerializerContext.Default.Rule)! };

        if (parameters is not { Length: 1 })
            throw new JsonException("The keys rule needs an array with 1 parameter.");

        return new KeysRule(parameters[0]);
    }

    public override void Write(Utf8JsonWriter writer, KeysRule value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}
