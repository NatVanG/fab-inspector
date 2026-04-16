using System.Text.Json.Serialization;
using PBIRInspectorLibrary;

namespace Ric.Operators;

public class ValuesOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "values";
    public override Type RuleType => typeof(ValuesRule);
}
