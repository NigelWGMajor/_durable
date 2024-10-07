using Microsoft.Azure.Functions.Worker;
using Degreed.SafeTest;
using System.Diagnostics;

//[DebuggerStepThrough]
public static class TestActivities
{
    [Function(nameof(StepAlpha))]
    public static async Task<Product> StepAlpha(
        [ActivityTrigger] Product product,
        FunctionContext context
    )
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
            product.Errors = ex.Message;
            return product;
        }
        catch (FlowManagerRetryableException ex)
        {
            product.LastState = ActivityState.Stalled;
            product.Errors = ex.Message;
            return product;
        }
    }

    [Function(nameof(StepBravo))]
    public static async Task<Product> StepBravo(
        [ActivityTrigger] Product product,
        FunctionContext context
    )
    {
        try
        {
            // PRODUCT PROCESSING HERE:

            await Task.Delay(TimeSpan.FromSeconds(20));

            // PRODUCT NOW PROCESSED.
            product.LastState = ActivityState.Completed;
            return product;
        }
        catch (FlowManagerFatalException ex)
        {
            product.LastState = ActivityState.Failed;
            product.Errors = ex.Message;
            return product;
        }
        catch (FlowManagerRetryableException ex)
        {
            product.LastState = ActivityState.Stalled;
            product.Errors = ex.Message;
            return product;
        }
    }

    [Function(nameof(StepCharlie))]
    public static async Task<Product> StepCharlie(
        [ActivityTrigger] Product product,
        FunctionContext context
    )
    {
        try
        {
            // PRODUCT PROCESSING HERE:

            await Task.Delay(TimeSpan.FromSeconds(10));

            // PRODUCT NOW PROCESSED.
            product.LastState = ActivityState.Completed;
            return product;
        }
        catch (FlowManagerFatalException ex)
        {
            product.LastState = ActivityState.Failed;
            product.Errors = ex.Message;
            return product;
        }
        catch (FlowManagerRetryableException ex)
        {
            product.LastState = ActivityState.Stalled;
            product.Errors = ex.Message;
            return product;
        }
    }
}
