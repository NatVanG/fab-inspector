using Json.Logic;
using Json.More;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Ric.Operators;

/// <summary>
/// Handles the `strjoin` operation. Joins elements of an array into a single string with a separator.
/// Parameters: [array, separator].
/// </summary>
[Operator("strjoin")]
[JsonConverter(typeof(StringJoinJsonConverter))]
public class StringJoinRule : Json.Logic.Rule
{
    internal Json.Logic.Rule Input { get; }
    internal Json.Logic.Rule Separator { get; }

    public StringJoinRule(Json.Logic.Rule input, Json.Logic.Rule separator)
    {
        Input = input;
        Separator = separator;
    }

    public override JsonNode? Apply(JsonNode? data, JsonNode? contextData = null)
    {
        var inputNode = Input.Apply(data, contextData);
        var separatorNode = Separator.Apply(data, contextData);

        if (inputNode is not JsonArray arr)
            throw new JsonLogicException("The strjoin rule requires a JSON array as the first parameter.");

        if (separatorNode is not JsonValue sepValue || !sepValue.TryGetValue(out string? separator))
            throw new JsonLogicException("The strjoin rule requires a string separator as the second parameter.");

        var parts = arr.Select(item => item?.Stringify() ?? string.Empty);
        return string.Join(separator, parts);
    }
}

internal class StringJoinJsonConverter : WeaklyTypedJsonConverter<StringJoinRule>
{
    public override StringJoinRule? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var parameters = reader.TokenType == JsonTokenType.StartArray
            ? options.ReadArray(ref reader, RicSerializerContext.Default.Rule)
            : new[] { options.Read(ref reader, RicSerializerContext.Default.Rule)! };

        if (parameters is not { Length: 2 })
            throw new JsonException("The strjoin rule needs an array with 2 parameters.");

        return new StringJoinRule(parameters[0], parameters[1]);
    }

    public override void Write(Utf8JsonWriter writer, StringJoinRule value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}
