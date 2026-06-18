using Json.Logic;
using Json.More;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ric.Operators;

/// <summary>
/// Handles the `intersect` operation as an alias of `intersection`.
/// </summary>
[Operator("intersect")]
[JsonConverter(typeof(SetIntersectRuleJsonConverter))]
public class SetIntersectRule : SetIntersectionRule
{
    public SetIntersectRule(Json.Logic.Rule set1, Json.Logic.Rule set2)
        : base(set1, set2)
    {
    }
}

internal class SetIntersectRuleJsonConverter : WeaklyTypedJsonConverter<SetIntersectRule>
{
    public override SetIntersectRule? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var parameters = reader.TokenType == JsonTokenType.StartArray
           ? options.ReadArray(ref reader, RicSerializerContext.Default.Rule)
           : new[] { options.Read(ref reader, RicSerializerContext.Default.Rule)! };

        if (parameters is not { Length: 2 })
            throw new JsonException("The intersect rule needs an array with 2 parameters.");

        return new SetIntersectRule(parameters[0], parameters[1]);
    }

    public override void Write(Utf8JsonWriter writer, SetIntersectRule value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}
