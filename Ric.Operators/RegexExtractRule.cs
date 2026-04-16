using Json.Logic;
using Json.More;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Ric.Operators;

/// <summary>
/// Handles the `regexextract` operation. Extracts matches from a string using a regex pattern.
/// Parameters: [string, pattern, group?].
/// <list type="bullet">
///   <item>If <c>group</c> is omitted, returns an array of all full matches.</item>
///   <item>If <c>group</c> is specified (0-based capture group index), returns an array of that group's values from each match.</item>
/// </list>
/// Returns an empty array when there are no matches.
/// </summary>
[Operator("regexextract")]
[JsonConverter(typeof(RegexExtractJsonConverter))]
public class RegexExtractRule : Json.Logic.Rule
{
    internal Json.Logic.Rule InputString { get; }
    internal Json.Logic.Rule Pattern { get; }
    internal Json.Logic.Rule? Group { get; }

    public RegexExtractRule(Json.Logic.Rule inputString, Json.Logic.Rule pattern, Json.Logic.Rule? group = null)
    {
        InputString = inputString;
        Pattern = pattern;
        Group = group;
    }

    public override JsonNode? Apply(JsonNode? data, JsonNode? contextData = null)
    {
        var inputNode = InputString.Apply(data, contextData);
        var patternNode = Pattern.Apply(data, contextData);

        if (inputNode is not JsonValue inputValue || !inputValue.TryGetValue(out string? input))
            throw new JsonLogicException("The regexextract rule requires a string as the first parameter.");

        if (patternNode is not JsonValue patternValue || !patternValue.TryGetValue(out string? pattern))
            throw new JsonLogicException("The regexextract rule requires a string regex pattern as the second parameter.");

        int groupIndex = 0;
        bool useGroup = false;
        if (Group is not null)
        {
            var groupNode = Group.Apply(data, contextData);
            if (groupNode is JsonValue gv)
            {
                if (gv.TryGetValue(out int g))
                {
                    groupIndex = g;
                    useGroup = true;
                }
                else if (gv.TryGetValue(out string? gs) && int.TryParse(gs, out int parsedGroup))
                {
                    groupIndex = parsedGroup;
                    useGroup = true;
                }
            }
        }

        var matches = Regex.Matches(input, pattern);
        var result = new JsonArray();

        foreach (Match match in matches)
        {
            if (useGroup)
            {
                if (groupIndex < match.Groups.Count)
                {
                    result.Add(JsonValue.Create(match.Groups[groupIndex].Value));
                }
            }
            else
            {
                result.Add(JsonValue.Create(match.Value));
            }
        }

        return result;
    }
}

internal class RegexExtractJsonConverter : WeaklyTypedJsonConverter<RegexExtractRule>
{
    public override RegexExtractRule? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var parameters = reader.TokenType == JsonTokenType.StartArray
            ? options.ReadArray(ref reader, RicSerializerContext.Default.Rule)
            : new[] { options.Read(ref reader, RicSerializerContext.Default.Rule)! };

        if (parameters == null || parameters.Length < 2)
            throw new JsonException("The regexextract rule needs at least 2 parameters: [string, pattern, group?].");

        return new RegexExtractRule(
            parameters[0],
            parameters[1],
            parameters.Length > 2 ? parameters[2] : null);
    }

    public override void Write(Utf8JsonWriter writer, RegexExtractRule value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}
