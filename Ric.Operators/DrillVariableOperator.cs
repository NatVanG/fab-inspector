using PBIRInspectorLibrary;

namespace Ric.Operators;

public class DrillVariableOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "drillvar";
    public override Type RuleType => typeof(DrillVariableRule);
}

