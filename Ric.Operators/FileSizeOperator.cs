using System.Text.Json.Serialization;
using FabInspector.Core;

namespace Ric.Operators;

public class FileSizeOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "filesize";
    public override Type RuleType => typeof(FileSizeRule);
}

