using System.Diagnostics;
using System.Text.Json.Serialization;
using Degreed.SafeTest;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
//using Microsoft.Azure.Functions.Worker.Extensions.Abstractions;
//using Microsoft.DurableTask;
//using Microsoft.Identity.Client;
//using Microsoft.Net.Http.Headers;
//using Models;

[DebuggerStepThrough]
public class Product
{
    public Product()
    {
    }
    public Product(string inputData)
    {
        Payload = inputData.Replace('\"', '\'');
    }
    [JsonPropertyName("UniqueKey")]
    public string UniqueKey { get; set; } = "";
    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";
    [JsonPropertyName("ActivityName")]
    public string ActivityName { get; set; } = "";
    [JsonPropertyName("PayLoad")]
    /// <summary>
    /// This holds a modified version of the json string that is passed to the orchestrator.
    /// Double quotes have been replaced with single quotes: 
    /// Ypu may get or set the Payload from Json directly using the PayloadJson 
    /// property, which applies this substitution transparently.
    /// <value></value>
    public string Payload { get; set; } = "";
    [JsonIgnore]
    public string PayloadJson 
    {
        get 
        { 
            return Payload.Replace('\'', '\"'); 
        }
        set
        {
            Payload = value.Replace('\"', '\'');
        }
    }
    [JsonPropertyName("LastState")]
    public ActivityState LastState { get; set; } = ActivityState.unknown;
    [JsonPropertyName("ActivityHistory")]
    public List<ActivityRecord> ActivityHistory { get; set; } = new List<ActivityRecord>();
    [JsonIgnore()]
    public bool IsDisrupted => Disruptions.Length > 0 || NextDisruption.Length > 0; 
    [JsonPropertyName("Errors")]
    public string Errors { get; set; }  = "";
    [JsonPropertyName("InstanceId")]
    public string InstanceId { get; set; } = "";
    [JsonPropertyName("Disruptions")]
    public string[] Disruptions { get; set; } = [];
    [JsonPropertyName("NextDisruption")]//
    public string NextDisruption { get; set; } = "";
    [JsonPropertyName("Output")]
    public string Output { get; set; } = "";
    public static Product FromContext(IDurableOrchestrationContext context)
    {
        return context == null ? 
            new Product("") 
            : context.GetInput<Product>() ?? new Product("");
    }
    [JsonPropertyName("NextTimeout")]
    public TimeSpan NextTimeout { get; set; } = TimeSpan.Zero;
    [JsonPropertyName("IsRedundant")]
    public bool IsRedundant { get; set; } = false;
    [JsonPropertyName("HostServer")]
    public string HostServer { get; set; } = "";
}