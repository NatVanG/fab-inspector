using Json.Logic;
using Json.More;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Ric.Operators;

/// <summary>
/// Handles the `strsplit` operation. Splits a string by a delimiter and returns an array of strings.
/// Parameters: [string, delimiter].
/// </summary>
[Operator("strsplit")]
[JsonConverter(typeof(StringSplitJsonConverter))]
public class StringSplitRule : Json.Logic.Rule
{
    internal Json.Logic.Rule InputString { get; }
    internal Json.Logic.Rule Delimiter { get; }

    public StringSplitRule(Json.Logic.Rule inputString, Json.Logic.Rule delimiter)
    {
        InputString = inputString;
        Delimiter = delimiter;
    }

    public override JsonNode? Apply(JsonNode? data, JsonNode? contextData = null)
    {
        var inputNode = InputString.Apply(data, contextData);
        var delimiterNode = Delimiter.Apply(data, contextData);

        if (inputNode is not JsonValue inputValue || !inputValue.TryGetValue(out string? inputString))
            throw new JsonLogicException("The strsplit rule requires a string as the first parameter.");

        if (delimiterNode is not JsonValue delimiterValue || !delimiterValue.TryGetValue(out string? delimiter))
            throw new JsonLogicException("The strsplit rule requires a string delimiter as the second parameter.");

        var parts = inputString.Split(delimiter);
        var result = new JsonArray();
        foreach (var part in parts)
        {
            result.Add(JsonValue.Create(part));
        }

        return result;
    }
}

internal class StringSplitJsonConverter : WeaklyTypedJsonConverter<StringSplitRule>
{
    public override StringSplitRule? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var parameters = reader.TokenType == JsonTokenType.StartArray
            ? options.ReadArray(ref reader, RicSerializerContext.Default.Rule)
            : new[] { options.Read(ref reader, RicSerializerContext.Default.Rule)! };

        if (parameters is not { Length: 2 })
            throw new JsonException("The strsplit rule needs an array with 2 parameters.");

        return new StringSplitRule(parameters[0], parameters[1]);
    }

    public override void Write(Utf8JsonWriter writer, StringSplitRule value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}
