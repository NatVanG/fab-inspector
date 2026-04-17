using System.Text.Json.Serialization;
using FabInspector.Core;

namespace FabInspector.Operators;

public class DaxQueryOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "daxquery";
    public override Type RuleType => typeof(DaxQueryRule);
}
