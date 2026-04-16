using System.Text.Json.Serialization;
using PBIRInspectorLibrary;

namespace Ric.Operators;

public class DistinctOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "distinct";
    public override Type RuleType => typeof(DistinctRule);
}
