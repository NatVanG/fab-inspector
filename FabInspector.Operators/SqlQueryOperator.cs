using FabInspector.Core;

namespace FabInspector.Operators;

public class SqlQueryOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "sqlquery";
    public override Type RuleType => typeof(SqlQueryRule);
}