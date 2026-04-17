using System.Text.Json.Serialization;

public class JsonLogicOperatorRegistry
{
    public JsonSerializerContext SerializerContext { get; }
    public IReadOnlyCollection<IJsonLogicOperator> Operators { get; }

    public JsonLogicOperatorRegistry(JsonSerializerContext context, IEnumerable<IJsonLogicOperator> operators)
    {
        SerializerContext = context;
        Operators = operators.ToList().AsReadOnly();
    }

    public void RegisterAll()
    {
        foreach (var op in Operators)
            op.Register(SerializerContext);
    }
}