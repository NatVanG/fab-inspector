using System.Text.Json.Serialization;
using FabInspector.Core;

namespace Ric.Operators;

public class FileTextSearchCountOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "filetextsearchcount";
    public override Type RuleType => typeof(FileTextSearchCountRule);
}