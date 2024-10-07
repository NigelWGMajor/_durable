using Degreed.SafeTest;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Models;
using static Activities.BaseActivities;
using static TestActivities;

namespace Orchestrations;

public static class OrchestrationAlpha
{
    // constants to tune retry policy:
    private const bool longRunning = false;
    private const bool highMemory = false;
    private const bool highDataOrFile = false;
    private static string _operation_name_ = nameof(StepAlpha);

    [Function(nameof(RunOrchestrationAlpha))]
    public static async Task<Product> RunOrchestrationAlpha(
        [OrchestrationTrigger] TaskOrchestrationContext context
    )
    {
        ILogger logger = context.CreateReplaySafeLogger(nameof(RunOrchestrationAlpha));
        Product product; // = new Product();

       // if (!context.IsReplaying)
       // {
            product = context.GetInput<Product>() ?? new Product();
            product.ActivityName = _operation_name_;
       // }
        product.InstanceId = context.InstanceId;
        //  product.Payload.Id = System.Diagnostics.Process.GetCurrentProcess().Id;

        product = await context.CallActivityAsync<Product>(
            nameof(PreProcessAsync),
            product,
            GetOptions(isDisrupted: product.IsDisrupted).WithInstanceId($"{context.InstanceId})-pre")
        );
        if (product.LastState == ActivityState.Deferred)
        {
            await context.CreateTimer(Settings.WaitTime, CancellationToken.None);
            product.LastState = ActivityState.unknown;
            logger.LogInformation("*** Deferred timer released ***"); //!
            context.ContinueAsNew(product);
            return product;
        }
        else if (product.LastState == ActivityState.Redundant)
        {
            return product;
        }
        else if (product.LastState != ActivityState.Active)
        {
            context.ContinueAsNew(product);
            return product;
        }
        if (
            product.LastState != ActivityState.Redundant && product.ActivityName == _operation_name_
        )
        {
            product = await context.CallActivityAsync<Product>(
                _operation_name_,
                product,
                GetOptions(longRunning, highMemory, highDataOrFile, product.IsDisrupted)
                    .WithInstanceId($"{context.InstanceId})-activity")
            );
            product = await context.CallActivityAsync<Product>(
                nameof(PostProcessAsync),
                product,
                GetOptions(isDisrupted: product.IsDisrupted).WithInstanceId($"{context.InstanceId})-post")
            );
            return product;
        }
        else
        {
            return product;
        }
    }
}
