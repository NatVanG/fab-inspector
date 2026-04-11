using System.Text.Json.Serialization;

namespace FabInspector.Operators;

[JsonSerializable(typeof(Json.Logic.Rule))]
[JsonSerializable(typeof(RectOverlapRule))]
[JsonSerializable(typeof(DaxQueryRule))]
[JsonSerializable(typeof(ApiGetRule))]
[JsonSerializable(typeof(ScannerApiRule))]
public partial class FabInspectorSerializerContext : JsonSerializerContext;