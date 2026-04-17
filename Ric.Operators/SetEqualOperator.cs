using System.Text.Json.Serialization;
using FabInspector.Core;

namespace Ric.Operators;

public class SetEqualOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "equalsets";
    public override Type RuleType => typeof(SetEqualRule);
}