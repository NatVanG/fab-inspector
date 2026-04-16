using System.Text.Json.Serialization;
using PBIRInspectorLibrary;

namespace Ric.Operators;

public class TypeOfOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "typeof";
    public override Type RuleType => typeof(TypeOfRule);
}
