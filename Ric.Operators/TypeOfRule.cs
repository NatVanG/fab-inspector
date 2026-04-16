using Json.Logic;
using Json.More;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Ric.Operators;

/// <summary>
/// Handles the `typeof` operation. Returns the type of a value as a string:
/// "string", "number", "boolean", "array", "object", or "null".
/// </summary>
[Operator("typeof")]
[JsonConverter(typeof(TypeOfJsonConverter))]
public class TypeOfRule : Json.Logic.Rule
{
    internal Json.Logic.Rule Input { get; }

    public TypeOfRule(Json.Logic.Rule input)
    {
        Input = input;
    }

    public override JsonNode? Apply(JsonNode? data, JsonNode? contextData = null)
    {
        var input = Input.Apply(data, contextData);

        if (input is null) return "null";

        return input switch
        {
            JsonArray => "array",
            JsonObject => "object",
            JsonValue val => GetValueType(val),
            _ => "null"
        };
    }

    private static string GetValueType(JsonValue val)
    {
        if (val.TryGetValue(out bool _)) return "boolean";
        if (val.TryGetValue(out int _) || val.TryGetValue(out long _) ||
            val.TryGetValue(out double _) || val.TryGetValue(out decimal _) ||
            val.TryGetValue(out float _)) return "number";
        if (val.TryGetValue(out string? _)) return "string";
        return "null";
    }
}

internal class TypeOfJsonConverter : WeaklyTypedJsonConverter<TypeOfRule>
{
    public override TypeOfRule? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var parameters = reader.TokenType == JsonTokenType.StartArray
            ? options.ReadArray(ref reader, RicSerializerContext.Default.Rule)
            : new[] { options.Read(ref reader, RicSerializerContext.Default.Rule)! };

        if (parameters is not { Length: 1 })
            throw new JsonException("The typeof rule needs an array with 1 parameter.");

        return new TypeOfRule(parameters[0]);
    }

    public override void Write(Utf8JsonWriter writer, TypeOfRule value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}
