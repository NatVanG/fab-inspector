using System.Text.Json.Serialization;
using FabInspector.Core;

namespace Ric.Operators;

public class RegexExtractOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "regexextract";
    public override Type RuleType => typeof(RegexExtractRule);
}
