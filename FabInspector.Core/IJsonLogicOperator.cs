using System.Text.Json.Serialization;

namespace FabInspector.Core
{
    public interface IJsonLogicOperator
    {
        string OperatorName { get; }
        Type RuleType { get; }
        void Register(JsonSerializerContext context);
    }
}