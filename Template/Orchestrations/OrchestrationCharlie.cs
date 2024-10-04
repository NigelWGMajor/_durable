using Degreed.SafeTest;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Orchestrations;
using static Orchestrations.BaseOrchestration;
using static TestActivities;

namespace Orchestrations;

public static class OrchestrationCharlie
{
    // constants to tune retry policy:
    private const bool longRunning = false;
    private const bool highMemory = false;
    private const bool highDataOrFile = false;
    private static string _operation_name_ = nameof(StepCharlie);

    [Function(nameof(RunOrchestrationCharlie))]
    public static async Task<Product> RunOrchestrationCharlie(
        [OrchestrationTrigger] TaskOrchestrationContext context
    )
    {
        ILogger logger = context.CreateReplaySafeLogger(nameof(RunOrchestrationCharlie));
        Product product = new Product();
        bool isDisrupted = product.Disruptions.Length > 0;
        product.InstanceId = context.InstanceId;

        if (!context.IsReplaying)
        {
            product = context.GetInput<Product>() ?? new Product();
            product.ActivityName = _operation_name_;
        }

        //product.Payload.Id = System.Diagnostics.Process.GetCurrentProcess().Id;

        product = await context.CallActivityAsync<Product>(
            nameof(PreProcessAsync),
            product,
            GetOptions(isDisrupted: isDisrupted).WithInstanceId($"{context.InstanceId})-pre")
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
                GetOptions(longRunning, highMemory, highDataOrFile, isDisrupted)
                    .WithInstanceId($"{context.InstanceId})-activity")
            );
            product = await context.CallActivityAsync<Product>(
                nameof(PostProcessAsync),
                product,
                GetOptions(isDisrupted: isDisrupted).WithInstanceId($"{context.InstanceId})-post")
            );
            return product;
        }
        else
        {
            return product;
        }
    }
}
