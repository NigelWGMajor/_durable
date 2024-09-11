namespace Degreed.SafeTest;

using System.Text.Json.Serialization;
public class InputData
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("identity")]
    public long Identity { get; set; } = 0;
}
