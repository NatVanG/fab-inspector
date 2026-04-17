using FabInspector.Core;

namespace FabInspector.Operators;

public class DfsGetOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "dfsget";
    public override Type RuleType => typeof(DfsGetRule);
}