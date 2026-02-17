using PBIRInspectorLibrary;

namespace Ric.Operators;

public class SetDifferenceOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "diff";
    public override Type RuleType => typeof(SetDifferenceRule);
}