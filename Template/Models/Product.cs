using System.Diagnostics;
using System.Text.Json.Serialization;
using Degreed.SafeTest;
using Microsoft.DurableTask;
using Microsoft.Identity.Client;
using Microsoft.Net.Http.Headers;

[DebuggerStepThrough]
public class Product
{
    public Product()
    {
        Payload = new Payload();
    }
    [JsonPropertyName("operationName")]
    public string OperationName { get; set; } = "";
    [JsonPropertyName("activityName")]
    public string ActivityName { get; set; } = "";
    [JsonPropertyName("payLoad")]
    public Payload Payload { get; set; }
    [JsonPropertyName("lastState")]
    public ActivityState LastState { get; set; } = ActivityState.unknown;
    [JsonPropertyName("activityHistory")]
    public List<ActivityRecord> ActivityHistory { get; set; } = new List<ActivityRecord>();
    [JsonIgnore()]
    public bool IsDisrupted => Disruptions.Length > 0; 
    [JsonPropertyName("errors")]
    public string Errors { get; set; }  = "";
    [JsonPropertyName("instanceId")]
    public string InstanceId { get;  set; }
    [JsonPropertyName("disruptions")]
    public string[] Disruptions { get; set; } = [];
    [JsonPropertyName("nextDisruption")]
    public string NextDisruption { get; private set; } 
    public static Product FromContext(TaskOrchestrationContext context)
    {
        return context.GetInput<Product>();
    }
    /// <summary>
    /// Pops the next disruption (or an empty string) off the disruptions stack
    /// into the NextDisruption variable. Need to call once per cycle.
    /// </summary>
    /// <returns></returns>
    public void PopDisruption()
    {
        // return the next disruption, removing it from the disruptions list
        if (Disruptions.Length == 0)
            NextDisruption = "";
        else
        {
            NextDisruption = Disruptions[0];
            string[] temp = new string[Disruptions.Length - 1];
            string result = Disruptions[0];
            for (int i = 0; i < temp.Length; i++)
            {
                temp[i] = Disruptions[i + 1];
            }
            Disruptions = temp;
        }
    }
}