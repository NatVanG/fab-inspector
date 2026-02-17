using System.Text.Json.Serialization;
using PBIRInspectorLibrary;

namespace FabInspector.Operators;

public class RectangleOverlapOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "rectoverlap";
    public override Type RuleType => typeof(RectOverlapRule);
}