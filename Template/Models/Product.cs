using System.Diagnostics;
using Degreed.SafeTest;
using Microsoft.DurableTask;

[DebuggerStepThrough]
public class Product
{
    public Product()
    {
        Payload = new Payload();
    }
    public string ActivityName { get; set; } = "";
    public Payload Payload { get; set; } 
    public ActivityState LastState { get; set; } = ActivityState.unknown;
    public List<ActivityRecord> ActivityHistory { get; set; } = new List<ActivityRecord>();
    public static Product FromContext(TaskOrchestrationContext context)
    {
        return context.GetInput<Product>();
    }
    public bool MayContinue => LastState != ActivityState.Redundant;
    public List<string> Errors = new List<string>();
}