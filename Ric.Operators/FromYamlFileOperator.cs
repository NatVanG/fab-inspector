using PBIRInspectorLibrary;

namespace Ric.Operators;

public class FromYamlFileOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "fromyamlfile";
    public override Type RuleType => typeof(FromYamlFileRule);
}
