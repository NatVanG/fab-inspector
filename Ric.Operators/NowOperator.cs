using System.Text.Json.Serialization;
using FabInspector.Core;

namespace Ric.Operators;

public class NowOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "now";
    public override Type RuleType => typeof(NowRule);
}
