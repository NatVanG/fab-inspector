using System.Text.Json.Serialization;
using PBIRInspectorLibrary;

namespace Ric.Operators;

public class KeysOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "keys";
    public override Type RuleType => typeof(KeysRule);
}
