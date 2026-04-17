using FabInspector.Core;
using System.Text.Json.Serialization;

public interface IJsonLogicOperator
{
    string OperatorName { get; }
    Type RuleType { get; }
    void Register(JsonSerializerContext context);
}