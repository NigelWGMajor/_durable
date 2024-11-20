
using System.Text.Json.Serialization;

namespace Models;

public interface IPayload
{
    string UniqueKey { get; set; }
    string Name { get; set; }
    string[] Disruptions { get; set; }
}

public class InputPayload : IPayload
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("UniqueKey")]
    public string UniqueKey { get; set; } = "";

    [JsonPropertyName("Disruptions")]
    public string[] Disruptions { get; set; } = [];
    [JsonPropertyName("ExternalData")]
    public string ExternalData { get; set; } = "ExternalTestData";
}
