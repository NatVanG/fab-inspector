using System.Text.Json.Serialization;
using PBIRInspectorLibrary;

namespace Ric.Operators;

public class CoalesceOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "coalesce";
    public override Type RuleType => typeof(CoalesceRule);
}
