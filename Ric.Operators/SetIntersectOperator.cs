using FabInspector.Core;

namespace Ric.Operators;

public class SetIntersectOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "intersect";
    public override Type RuleType => typeof(SetIntersectRule);
}
