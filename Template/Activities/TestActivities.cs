using Microsoft.Azure.Functions.Worker;



public static class TestActivities
{
    [Function(nameof(SayHello))]
    // this was the original sample.
    public static string SayHello([ActivityTrigger] string name, FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger("SayHello");
        logger.LogInformation("Saying hello to {name}.", name);
        return $"Hello {name}!";
    }
    
    [Function(nameof(StepAlpha))]
    public static Product StepAlpha([ActivityTrigger] Product product, FunctionContext context)
    {
        return product;
    }

    [Function(nameof(StepBravo))]
    public static Product StepBravo([ActivityTrigger] Product product, FunctionContext context)
    {
        return product;
    }

    [Function(nameof(StepCharlie))]
    public static Product StepCharlie([ActivityTrigger] Product product, FunctionContext context)
    {
        return product;
    }
}