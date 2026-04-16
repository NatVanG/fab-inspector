using System.Text.Json.Serialization;
using PBIRInspectorLibrary;

namespace Ric.Operators;

public class StringJoinOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "strjoin";
    public override Type RuleType => typeof(StringJoinRule);
}
