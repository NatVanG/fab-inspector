using PBIRInspectorLibrary;

namespace FabInspector.Operators;

public class ScannerApiOperator : BaseJsonLogicOperator
{
    public override string OperatorName => "scannerapi";
    public override Type RuleType => typeof(ScannerApiRule);
}
