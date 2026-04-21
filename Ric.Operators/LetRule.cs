using Json.Logic;
using Json.More;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Ric.Operators;

/// <summary>
/// Handles the `let` operation. Evaluates one or more binding expressions once,
/// assigns their results to named variables, then evaluates a body expression
/// with those names available via <c>{"var": "name"}</c>.
/// <para>
/// Syntax: <c>{"let": [{"name1": expr1, "name2": expr2, ...}, body]}</c>
/// </para>
/// <para>
/// Bindings are evaluated in declaration order against progressively extended data.
/// This allows later bindings to reference earlier bindings from the same <c>let</c>.
/// The body is evaluated against the original data extended with all bound values.
/// Existing data properties are visible in the body and are shadowed by any
/// binding with the same name.
/// </para>
/// <para>
/// Primary use case: call an expensive operator (e.g. <c>apiget</c>, <c>daxquery</c>)
/// once and reference its result multiple times in the body without repeating the call.
/// </para>
/// </summary>
[Operator("let")]
[JsonConverter(typeof(LetJsonConverter))]
public class LetRule : Json.Logic.Rule
{
    internal IReadOnlyDictionary<string, Json.Logic.Rule> Bindings { get; }
    internal Json.Logic.Rule Body { get; }

    public LetRule(IReadOnlyDictionary<string, Json.Logic.Rule> bindings, Json.Logic.Rule body)
    {
        Bindings = bindings;
        Body = body;
    }

    public override JsonNode? Apply(JsonNode? data, JsonNode? contextData = null)
    {
        // Build extended context starting from a copy of original data
        var extended = new JsonObject();

        if (data is JsonObject existing)
            foreach (var (key, val) in existing)
                extended[key] = val?.DeepClone();

        // Evaluate bindings in declaration order against progressively extended data
        // so later bindings can reference earlier binding results.
        foreach (var (name, rule) in Bindings)
            extended[name] = rule.Apply(extended, contextData)?.DeepClone();

        return Body.Apply(extended, contextData);
    }
}

internal class LetJsonConverter : WeaklyTypedJsonConverter<LetRule>
{
    public override LetRule? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Parse the whole value as a JsonNode to avoid managing reader state manually
        var node = JsonNode.Parse(ref reader);

        if (node is not JsonArray arr || arr.Count != 2)
            throw new JsonException("The let rule expects exactly [bindings, body].");

        if (arr[0] is not JsonObject bindingsNode)
            throw new JsonException("The let rule bindings (first element) must be a JSON object.");

        var bindings = new Dictionary<string, Json.Logic.Rule>();
        foreach (var (key, val) in bindingsNode)
        {
            var bindingRule = JsonSerializer.Deserialize<Json.Logic.Rule>(
                val?.ToJsonString() ?? "null", options)
                ?? throw new JsonException($"The let rule binding '{key}' could not be deserialized as a rule.");
            bindings[key] = bindingRule;
        }

        var body = JsonSerializer.Deserialize<Json.Logic.Rule>(
            arr[1]?.ToJsonString() ?? "null", options)
            ?? throw new JsonException("The let rule body (second element) could not be deserialized as a rule.");

        return new LetRule(bindings, body);
    }

    public override void Write(Utf8JsonWriter writer, LetRule value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}
