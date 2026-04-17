using System.Text.Json.Serialization;
using FabInspector.Core;

namespace Ric.Operators;

public class SetSymmetricDifferenceOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "symdiff";
    public override Type RuleType => typeof(SetSymmetricDifferenceRule);
}