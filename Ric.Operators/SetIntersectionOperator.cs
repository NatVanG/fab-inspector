using System.Text.Json.Serialization;
using FabInspector.Core;

namespace Ric.Operators;

public class SetIntersectionOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "intersect";
    public override Type RuleType => typeof(SetIntersectionRule);
}