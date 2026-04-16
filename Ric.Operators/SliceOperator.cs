using System.Text.Json.Serialization;
using PBIRInspectorLibrary;

namespace Ric.Operators;

public class SliceOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "slice";
    public override Type RuleType => typeof(SliceRule);
}
