using PBIRInspectorLibrary;

namespace Ric.Operators;

public class QueryOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "query";
    public override Type RuleType => typeof(QueryRule);
}