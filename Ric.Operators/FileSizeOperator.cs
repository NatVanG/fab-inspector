using System.Text.Json.Serialization;
using PBIRInspectorLibrary;

namespace Ric.Operators;

public class FileSizeOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "filesize";
    public override Type RuleType => typeof(FileSizeRule);
}

