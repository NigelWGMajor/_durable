using System.Diagnostics;
using System.Text.Json.Serialization;
using Degreed.SafeTest;
using Microsoft.Azure.Functions.Worker.Extensions.Abstractions;
using Microsoft.DurableTask;
using Microsoft.Identity.Client;
using Microsoft.Net.Http.Headers;
using Models;

[DebuggerStepThrough]
public class Product
{
    public Product()
    {
            
    }
    [JsonPropertyName("UniqueKey")]
    public string UniqueKey { get; set; } = "";
    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";
    [JsonPropertyName("OperationName")]
    public string OperationName { get; set; } = "";
    [JsonPropertyName("ActivityName")]
    public string ActivityName { get; set; } = "";
    [JsonPropertyName("Content")]
    public object? Content { get; set; }
    [JsonPropertyName("LastState")]
    public ActivityState LastState { get; set; } = ActivityState.unknown;
    [JsonPropertyName("ActivityHistory")]
    public List<ActivityRecord> ActivityHistory { get; set; } = new List<ActivityRecord>();
    [JsonIgnore()]
    public bool IsDisrupted => Disruptions.Length > 0 || NextDisruption.Length > 0;
    [JsonPropertyName("Errors")]
    public string Errors { get; set; } = "";
    [JsonPropertyName("InstanceId")]
    public string InstanceId { get; set; } = "";
    [JsonPropertyName("Disruptions")]
    public string[] Disruptions { get; set; } = [];
    [JsonPropertyName("NextDisruption")]
    public string NextDisruption { get; set; } = "";
    [JsonPropertyName("Output")]
    public string Output { get; set; } = "";
    public static Product FromContext(TaskOrchestrationContext context)
    {
        return context == null ? 
            new Product() 
            : context.GetInput<Product>() ?? new Product();
    }
    [JsonPropertyName("NextTimeout")]
    public TimeSpan NextTimeout { get; set; } = TimeSpan.Zero;
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
