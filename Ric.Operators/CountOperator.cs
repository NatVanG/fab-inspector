using System.Text.Json.Serialization;
using PBIRInspectorLibrary;

namespace Ric.Operators;

public class CountOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "count";
    public override Type RuleType => typeof(CountRule);
}
