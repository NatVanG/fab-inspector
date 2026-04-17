using System.Text.Json.Serialization;
using FabInspector.Core;

namespace Ric.Operators;

public class SliceOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "slice";
    public override Type RuleType => typeof(SliceRule);
}
