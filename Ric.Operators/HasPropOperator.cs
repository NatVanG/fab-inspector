using System.Text.Json.Serialization;
using PBIRInspectorLibrary;

namespace Ric.Operators;

public class HasPropOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "hasprop";
    public override Type RuleType => typeof(HasPropRule);
}
