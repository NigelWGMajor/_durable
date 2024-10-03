namespace Degreed.SafeTest;

using System.Text.Json.Serialization;
public class InputData
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("uniqueKey")]
    public string UniqueKey { get; set; } = "";

    [JsonPropertyName("disruptions")]
    public string[] Disruptions { get; set; } = [];
}
