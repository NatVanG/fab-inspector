using Json.Patch;
using System.Text.Json.Serialization;

namespace Ric.Operators;

[JsonSerializable(typeof(Json.Logic.Rule))]
[JsonSerializable(typeof(CountRule))]
[JsonSerializable(typeof(DrillVariableRule))]
[JsonSerializable(typeof(IsNullOrEmptyRule))]
[JsonSerializable(typeof(PartRule))]
[JsonSerializable(typeof(PartInfoRule))]
[JsonSerializable(typeof(PathRule))]
[JsonSerializable(typeof(QueryRule))]
[JsonSerializable(typeof(SetDifferenceRule))]
[JsonSerializable(typeof(SetEqualRule))]
[JsonSerializable(typeof(SetIntersectionRule))]
[JsonSerializable(typeof(SetSymmetricDifferenceRule))]
[JsonSerializable(typeof(SetIntersectionRule))]
[JsonSerializable(typeof(SetUnionRule))]
[JsonSerializable(typeof(StringContains))]
[JsonSerializable(typeof(FileTextSearchCountRule))]
[JsonSerializable(typeof(ToRecordRule))]
[JsonSerializable(typeof(ToString))]
[JsonSerializable(typeof(FileSizeRule))]
[JsonSerializable(typeof(FromYamlFileRule))]
[JsonSerializable(typeof(JsonPatch))]
[JsonSerializable(typeof(PatchResult))]
public partial class RicSerializerContext : JsonSerializerContext;