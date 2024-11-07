namespace Degreed.SafeTest;

using System.Text.Json.Serialization;



public interface IPayload
{
    string UniqueKey { get; set; }
    string Name { get; set; }
    string[] Disruptions { get; set; }
}

public class InputPayload : IPayload
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("uniqueKey")]
    public string UniqueKey { get; set; } = "";

    [JsonPropertyName("disruptions")]
    public string[] Disruptions { get; set; } = [];
    [JsonPropertyName("externalData")]
    public string ExternalData { get; set; } = "ExternalTestData";
}
