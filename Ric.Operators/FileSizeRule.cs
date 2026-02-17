using Json.Logic;
using Json.More;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using PBIRInspectorLibrary.Part;

namespace Ric.Operators;

/// <summary>
/// Handles the `filesize` operation. 
/// </summary>
[Operator("filesize")]
    [JsonConverter(typeof(FileSizeRuleJsonConverter))]
    public class FileSizeRule : Json.Logic.Rule
    {
        internal Json.Logic.Rule InputString { get; }

        public FileSizeRule(Json.Logic.Rule inputString)
        {
            InputString = inputString;
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
            var node = InputString.Apply(data, contextData);

            if (node is JsonArray) throw new JsonException("The FileSizeRule filePath parameter cannot be an array.");

            var partInfo = PartUtils.TryGetPartInfo(node, setAdvancedProperties: true);

            if (partInfo == null || !partInfo.Exists || partInfo.PartFileSystemType != PartFileSystemTypeEnum.File) { throw new JsonLogicException($"FileSizeRule - file not found. Try using in conjunction with partinfo rule, instead of the part rule."); }

            return partInfo.FileSize;
        }
    }

internal class FileSizeRuleJsonConverter : WeaklyTypedJsonConverter<FileSizeRule>
{
    public override FileSizeRule? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var parameters = reader.TokenType == JsonTokenType.StartArray
       ? options.ReadArray(ref reader, RicSerializerContext.Default.Rule)
       : new[] { options.Read(ref reader, RicSerializerContext.Default.Rule)! };

        if (parameters is not { Length: 1 })
            throw new JsonException("The FileSizeRule rule needs an array with 1 parameters.");

        if (parameters.Length == 1) return new FileSizeRule(parameters[0]);

        return new FileSizeRule(parameters[0]);
    }

    public override void Write(Utf8JsonWriter writer, FileSizeRule value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}
