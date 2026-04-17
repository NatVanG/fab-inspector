using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace FabInspector.Core;

[JsonConverter(typeof(TestConverter))]
public class Test
{
#pragma warning disable CS8618
    public string Logic { get; set; }
    public JsonNode? Data { get; set; }
    public JsonNode? Expected { get; set; }
#pragma warning restore CS8618
}

public class TestConverter : JsonConverter<Test?>
{
    public override Test? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var node = JsonSerializer.Deserialize<JsonNode?>(ref reader, options);
        if (node is not JsonArray arr) return null;
        var arrLen = arr.Count;
        if (arrLen < 2 || arrLen > 3) throw new InvalidOperationException("ERROR: Rule should be defined as a two or three member array for logic, data (optional) and expected result.");

        var logic = JsonSerializer.Serialize(arr[0], new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        var data = arrLen > 2 ? arr[1] : new JsonObject();
        var expected = arrLen > 2 ? arr[2] : arr[1];

        return new Test
        {
            Logic = logic,
            Data = data,
            Expected = expected
        };
    }

    public override void Write(Utf8JsonWriter writer, Test? value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}