using Degreed.SafeTest;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using static Activities.BaseActivities;
using static TestActivities;

namespace Orchestrations;

public static class OrchestrationBravo
{
    // constants to tune retry policy:
    private const bool longRunning = false;
    private const bool highMemory = false;
    private const bool highDataOrFile = false;
    private static string _operation_name_ = nameof(StepBravo);

    [Function(nameof(RunOrchestrationBravo))]
    public static async Task<Product> RunOrchestrationBravo(
        [OrchestrationTrigger] TaskOrchestrationContext context
    )
    {
        ILogger logger = context.CreateReplaySafeLogger(nameof(RunOrchestrationBravo));
        Product product = new Product();
        product.InstanceId = context.InstanceId;
        if (!context.IsReplaying)
        {
            logger.LogInformation("*** Initializing Product");
            product = context.GetInput<Product>() ?? new Product();
            product.ActivityName = _operation_name_;
        }

        //product.Payload.Id = System.Diagnostics.Process.GetCurrentProcess().Id;

        product = await context.CallActivityAsync<Product>(
            nameof(PreProcessAsync),
            product,
            GetOptions(isDisrupted: product.IsDisrupted).WithInstanceId($"{context.InstanceId})-pre")
        );
        if (product.LastState == ActivityState.Deferred)
            await context.CreateTimer(TimeSpan.FromSeconds(1), CancellationToken.None);
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
