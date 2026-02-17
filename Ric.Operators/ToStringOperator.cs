using PBIRInspectorLibrary;

namespace Ric.Operators;

public class ToStringOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "tostring";
    public override Type RuleType => typeof(ToString);
}