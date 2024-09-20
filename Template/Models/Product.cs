using Degreed.SafeTest;
using Microsoft.DurableTask;
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
        var product = new Product();
        
        var inputData = context.GetInput<InputData>();
        if (inputData == null) // this has been integration tested.
            throw new FlowManagerException("Input data was not provided to the http start command");
        product.LastState = ActivityState.Ready;
        product.Payload.Name = inputData.Name;
        product.Payload.Identity = inputData.Identity;
        return product;
    }
}