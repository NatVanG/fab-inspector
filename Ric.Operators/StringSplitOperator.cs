using System.Text.Json.Serialization;
using FabInspector.Core;

namespace Ric.Operators;

public class StringSplitOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "strsplit";
    public override Type RuleType => typeof(StringSplitRule);
}
