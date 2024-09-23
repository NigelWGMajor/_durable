using Microsoft.Azure.Functions.Worker;
using Degreed.SafeTest;
using System.Diagnostics;

[DebuggerStepThrough]
public static class TestActivities
{
    // [Function(nameof(SayHello))]
    // // this was the original sample.
    // public static string SayHello([ActivityTrigger] string name, FunctionContext executionContext)
    // {
    //     ILogger logger = executionContext.GetLogger("SayHello");
    //     logger.LogInformation("Saying hello to {name}.", name);
    //     return $"Hello {name}!";
    // }

    [Function(nameof(StepAlpha))]
    public static async Task<Product> StepAlpha([ActivityTrigger] Product product, FunctionContext context)
    {
        try
        {
            // PRODUCT PROCESSING HERE:

            await Task.Delay(TimeSpan.FromSeconds(30));

            // PRODUCT NOW PROCESSED.
            product.LastState = ActivityState.Completed;
            return product;
        }
        catch (FlowManagerFatalException ex)
        {
            product.LastState = ActivityState.Failed;
            product.Errors.Add(ex.Message);
            return product;
        }
        catch (FlowManagerRetryableException ex)
        {
            product.LastState = ActivityState.Stalled;
            product.Errors.Add(ex.Message);
            return product;
        }
    }

    [Function(nameof(StepBravo))]
    public static async Task<Product> StepBravo([ActivityTrigger] Product product, FunctionContext context)
    {
        try
        {
            // PRODUCT PROCESSING HERE:

            await Task.Delay(TimeSpan.FromSeconds(30));

            // PRODUCT NOW PROCESSED.
            product.LastState = ActivityState.Completed;
            return product;
        }
        catch (FlowManagerFatalException ex)
        {
            product.LastState = ActivityState.Failed;
            product.Errors.Add(ex.Message);
            return product;
        }
        catch (FlowManagerRetryableException ex)
        {
            product.LastState = ActivityState.Stalled;
            product.Errors.Add(ex.Message);
            return product;
        }
    }

    [Function(nameof(StepCharlie))]
    public static async Task<Product> StepCharlie([ActivityTrigger] Product product, FunctionContext context)
    {
        try
        {
            // PRODUCT PROCESSING HERE:

            await Task.Delay(TimeSpan.FromSeconds(30));

            // PRODUCT NOW PROCESSED.
            product.LastState = ActivityState.Completed;
            return product;
        }
        catch (FlowManagerFatalException ex)
        {
            product.LastState = ActivityState.Failed;
            product.Errors.Add(ex.Message);
            return product;
        }
        catch (FlowManagerRetryableException ex)
        {
            product.LastState = ActivityState.Stalled;
            product.Errors.Add(ex.Message);
            return product;
        }
    }
}
