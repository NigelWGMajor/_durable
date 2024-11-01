using Degreed.SafeTest;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Models;
using static Activities.BaseActivities;
using static TestActivities;

namespace Orchestrations;

public static class SubOrchestrationCharlie // rename this and the file to match the orchestration name
{
    // constants to tune retry policy:
    private const bool longRunning = false;
    private const bool highMemory = false;
    private const bool highDataOrFile = false;
    private static string _operation_name_ = nameof(ActivityCharlie); // rename this to match the activity name

    private const string _orchestration_name_ = nameof(OrchestrationCharlie); // rename this appropriately

    [Function(_orchestration_name_)]
    public static async Task<Product> OrchestrationCharlie( // Rename this function to match the operation name
        [OrchestrationTrigger] TaskOrchestrationContext context
    )
    {
        ILogger logger = context.CreateReplaySafeLogger(_orchestration_name_);
        Product product = context.GetInput<Product>() ?? new Product();
        product.ActivityName = _operation_name_;
        product.InstanceId = context.InstanceId;
        // product = await context.CallActivityAsync<Product>(
        //     _pre_processor_name_,
        //     product,
        //     (await GetRetryOptionsAsync(_pre_processor_name_, product))
        //         .WithInstanceId($"{context.InstanceId})-pre")
        // );
        // if (product.LastState == ActivityState.Deferred)
        // {
        //     var current = await _store.ReadActivityStateAsync(product.Payload.UniqueKey);
        //     await context.CreateTimer(Settings.WaitTime, CancellationToken.None);
        //     await _store.WriteActivityStateAsync(current);
        //     product.LastState = ActivityState.unknown;
        //     context.ContinueAsNew(product);
        //     return product;
        // }
        // else if (product.LastState == ActivityState.Redundant)
        // {
        //     return product;
        // }
        // else if (product.LastState != ActivityState.Active)
        // {
        //     context.ContinueAsNew(product);
        //     return product;
        // }
        // if (
        //     product.LastState != ActivityState.Redundant && product.ActivityName == _operation_name_
        // )
        // {
        product = await context.CallActivityAsync<Product>(
            _operation_name_,
            product,
            (await GetRetryOptionsAsync(_operation_name_, product)).WithInstanceId(
                $"{context.InstanceId})-activity"
            )
        );

        //     product = await context.CallActivityAsync<Product>(
        //         _post_processor_name_,
        //         product,
        //         (await GetRetryOptionsAsync(_post_processor_name_, product))
        //             .WithInstanceId($"{context.InstanceId})-post")
        //     );
        //     if (product.LastState == ActivityState.Failed)
        //     {
        //         throw new FlowManagerFatalException(product.Errors);
        //     }
        //     return product;
        // }
        // else
        // {
        return product;
        //}
    }
}
