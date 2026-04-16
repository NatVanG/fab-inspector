using Json.Logic;
using Json.More;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Ric.Operators;

/// <summary>
/// Handles the `hasprop` operation. Checks whether a JSON object contains a given property key.
/// Unlike `var` (which returns null for both missing keys and explicitly-null values),
/// this operator distinguishes "property exists with null value" from "property does not exist".
/// </summary>
[Operator("hasprop")]
[JsonConverter(typeof(HasPropJsonConverter))]
public class HasPropRule : Json.Logic.Rule
{
    internal Json.Logic.Rule Object { get; }
    internal Json.Logic.Rule Key { get; }

    public HasPropRule(Json.Logic.Rule obj, Json.Logic.Rule key)
    {
        Object = obj;
        Key = key;
    }

    public override JsonNode? Apply(JsonNode? data, JsonNode? contextData = null)
    {
        var obj = Object.Apply(data, contextData);
        var key = Key.Apply(data, contextData);

        if (obj is null) return false;
        if (obj is not JsonObject jsonObj)
            throw new JsonLogicException("The hasprop rule requires a JSON object as the first parameter.");

        if (key is not JsonValue keyValue || !keyValue.TryGetValue(out string? keyString))
            throw new JsonLogicException("The hasprop rule requires a string key as the second parameter.");

        return jsonObj.ContainsKey(keyString);
    }
}

internal class HasPropJsonConverter : WeaklyTypedJsonConverter<HasPropRule>
{
    public override HasPropRule? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var parameters = reader.TokenType == JsonTokenType.StartArray
            ? options.ReadArray(ref reader, RicSerializerContext.Default.Rule)
            : new[] { options.Read(ref reader, RicSerializerContext.Default.Rule)! };

        if (parameters is not { Length: 2 })
            throw new JsonException("The hasprop rule needs an array with 2 parameters.");

        return new HasPropRule(parameters[0], parameters[1]);
    }

    public override void Write(Utf8JsonWriter writer, HasPropRule value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}
