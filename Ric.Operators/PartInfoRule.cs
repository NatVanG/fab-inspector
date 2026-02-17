using Json.Logic;
using Json.More;
using PBIRInspectorLibrary.Part;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Ric.Operators;

/// <summary>
/// Handles the `partinfo` operation.
/// </summary>
[Operator("partinfo")]
[JsonConverter(typeof(PartInfoRuleJsonConverter))]
public class PartInfoRule : Json.Logic.Rule
{
    internal Json.Logic.Rule Input { get; }

    internal PartInfoRule(Json.Logic.Rule input)
    {
        Input = input;
    }

    /// <summary>
    /// Applies the rule to the input data.
    /// </summary>
    /// <param name="data">The input data.</param>
    /// <param name="contextData">
    ///     Optional secondary data.  Used by a few operators to pass a secondary
    ///     data context to inner operators.
    /// </param>
    /// <returns>The result of the rule.</returns>
    public override JsonNode? Apply(JsonNode? data, JsonNode? contextData = null)
	{
		JsonNode? result = null;
        var input = Input.Apply(data, contextData);

        if (input is null) throw new ArgumentException("PartInfoRule input cannot be null");
        var stringInput = input.Stringify();

        var contextPartQuery = ContextService.Current.PartQuery;
        var contextPart = ContextService.Current.Part;
		result = PartUtils.PartInfoToJsonNode(contextPartQuery.Invoke(stringInput, contextPart));

        return result;
    }
}

internal class PartInfoRuleJsonConverter : WeaklyTypedJsonConverter<PartInfoRule>
{
	public override PartInfoRule? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
        var parameters = reader.TokenType == JsonTokenType.StartArray
           ? options.ReadArray(ref reader, RicSerializerContext.Default.Rule)
           : new[] { options.Read(ref reader, RicSerializerContext.Default.Rule)! };

        if (parameters is not ({ Length: 1 }))
			throw new JsonException("The part rule needs an array with 1 parameter.");

		return new PartInfoRule(parameters[0]);
	}

	public override void Write(Utf8JsonWriter writer, PartInfoRule value, JsonSerializerOptions options)
	{
		throw new NotImplementedException();
	}
}