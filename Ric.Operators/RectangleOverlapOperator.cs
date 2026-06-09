using FabInspector.Core;

namespace Ric.Operators;

public class RectangleOverlapOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "rectoverlap";
    public override Type RuleType => typeof(RectOverlapRule);
}
