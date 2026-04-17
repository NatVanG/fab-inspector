using System.Text.Json.Serialization;
using FabInspector.Core;

namespace Ric.Operators;

public class StringJoinOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "strjoin";
    public override Type RuleType => typeof(StringJoinRule);
}
