using PBIRInspectorLibrary;

namespace Ric.Operators;

public class LetOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "let";
    public override Type RuleType => typeof(LetRule);
}
