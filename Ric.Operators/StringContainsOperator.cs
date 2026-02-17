using PBIRInspectorLibrary;

namespace Ric.Operators;

public class StringContainsOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "strcontains";
    public override Type RuleType => typeof(StringContains);

}