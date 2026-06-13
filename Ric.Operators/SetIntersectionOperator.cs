using System.Text.Json.Serialization;
using FabInspector.Core;

namespace Ric.Operators;

public class SetIntersectionOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "intersection";
    public override Type RuleType => typeof(SetIntersectionRule);
}