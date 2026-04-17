using System.Text.Json.Serialization;
using FabInspector.Core;

namespace Ric.Operators;

public class PathOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "path";
    public override Type RuleType => typeof(PathRule);
}