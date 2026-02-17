using System.Text.Json.Serialization;

namespace FabInspector.Operators;

[JsonSerializable(typeof(Json.Logic.Rule))]
[JsonSerializable(typeof(RectOverlapRule))]
public partial class FabInspectorSerializerContext : JsonSerializerContext;