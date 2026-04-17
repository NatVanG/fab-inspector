using System.Text.Json.Serialization;
using FabInspector.Core;

namespace FabInspector.Operators;

public class ApiGetOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "apiget";
    public override Type RuleType => typeof(ApiGetRule);
}