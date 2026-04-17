using System.Text.Json.Serialization;
using FabInspector.Core;

namespace Ric.Operators;

public class SetUnionOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "union";
    public override Type RuleType => typeof(SetUnionRule);
}