using System.Text.Json.Serialization;
using PBIRInspectorLibrary;

namespace Ric.Operators;

public class PartOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "part";
    public override Type RuleType => typeof(PartRule);
}