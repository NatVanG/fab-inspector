using System.Text.Json.Serialization;
using PBIRInspectorLibrary;

namespace Ric.Operators;

public class DateDiffOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "datediff";
    public override Type RuleType => typeof(DateDiffRule);
}
