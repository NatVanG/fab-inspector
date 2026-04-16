using Json.Logic;
using Json.More;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Ric.Operators;

/// <summary>
/// Handles the `slice` operation. Extracts a sub-array from an array, analogous to <c>substr</c> for strings.
/// Parameters: [array, start, end?].
/// <list type="bullet">
///   <item>Negative <c>start</c> counts from the end of the array.</item>
///   <item>If <c>end</c> is omitted, slices to the end. Negative <c>end</c> counts from the end.</item>
/// </list>
/// Follows JavaScript <c>Array.prototype.slice()</c> semantics.
/// </summary>
[Operator("slice")]
[JsonConverter(typeof(SliceJsonConverter))]
public class SliceRule : Json.Logic.Rule
{
    internal Json.Logic.Rule Input { get; }
    internal Json.Logic.Rule Start { get; }
    internal Json.Logic.Rule? End { get; }

    public SliceRule(Json.Logic.Rule input, Json.Logic.Rule start, Json.Logic.Rule? end = null)
    {
        Input = input;
        Start = start;
        End = end;
    }

    public override JsonNode? Apply(JsonNode? data, JsonNode? contextData = null)
    {
        var inputNode = Input.Apply(data, contextData);
        var startNode = Start.Apply(data, contextData);

        if (inputNode is not JsonArray arr)
            throw new JsonLogicException("The slice rule requires a JSON array as the first parameter.");

        var length = arr.Count;

        int start = ResolveInt(startNode, "start");
        if (start < 0) start = Math.Max(length + start, 0);
        if (start > length) start = length;

        int end;
        if (End is not null)
        {
            var endNode = End.Apply(data, contextData);
            end = ResolveInt(endNode, "end");
            if (end < 0) end = Math.Max(length + end, 0);
            if (end > length) end = length;
        }
        else
        {
            end = length;
        }

        var result = new JsonArray();
        for (int i = start; i < end; i++)
        {
            result.Add(arr[i]?.DeepClone());
        }

        return result;
    }

    private static int ResolveInt(JsonNode? node, string paramName)
    {
        if (node is JsonValue val)
        {
            if (val.TryGetValue(out int i)) return i;
            if (val.TryGetValue(out long l)) return (int)l;
            if (val.TryGetValue(out double d)) return (int)d;
            if (val.TryGetValue(out string? s) && int.TryParse(s, out int parsed)) return parsed;
        }
        throw new JsonLogicException($"The slice rule requires a numeric {paramName} parameter.");
    }
}

internal class SliceJsonConverter : WeaklyTypedJsonConverter<SliceRule>
{
    public override SliceRule? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var parameters = reader.TokenType == JsonTokenType.StartArray
            ? options.ReadArray(ref reader, RicSerializerContext.Default.Rule)
            : new[] { options.Read(ref reader, RicSerializerContext.Default.Rule)! };

        if (parameters == null || parameters.Length < 2)
            throw new JsonException("The slice rule needs at least 2 parameters: [array, start, end?].");

        return new SliceRule(
            parameters[0],
            parameters[1],
            parameters.Length > 2 ? parameters[2] : null);
    }

    public override void Write(Utf8JsonWriter writer, SliceRule value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}
