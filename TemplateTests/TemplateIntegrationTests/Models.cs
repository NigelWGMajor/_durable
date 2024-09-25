using System.Text.Json;
using System.Text.Json.Serialization;

namespace TemplateIntegrationTests
{
   public class InputData
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("identity")]
    public string Identity { get; set; } = "";

    [JsonPropertyName("testStates")]
    public string TestStates { get; set; } = "";
}
    public enum ActivityState
    {
        unknown = 0,
        Ready,
        Active,
        Redundant,
        Deferred,
        Completed,
        Stuck,
        Stalled,
        Failed
    }
}
