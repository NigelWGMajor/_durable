namespace Degreed.SafeTest;

using System.Text.Json.Serialization;
public class InputData
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("identity")]
    public string Identity { get; set; } = "";

    [JsonPropertyName("testStates")]
    public ActivityState[] TestStates { get; set; } = [];
}
