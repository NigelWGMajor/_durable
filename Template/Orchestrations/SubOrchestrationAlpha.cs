using Degreed.SafeTest;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Models;
using static Activities.BaseActivities;
using static TestActivities;

namespace Orchestrations;

public static class SubOrchestrationAlpha // rename this and the file to match the orchestration name
{
    // constants to tune retry policy:
    private const bool longRunning = false;
    private const bool highMemory = false;
    private const bool highDataOrFile = false;
    private static string _operation_name_ = nameof(ActivityAlpha); // rename this to match the activity name

    private const string _orchestration_name_ = nameof(OrchestrationAlpha); // rename this appropriately

    [Function(_orchestration_name_)]
    public static async Task<Product> OrchestrationAlpha( // Rename this function to match the operation name
        [OrchestrationTrigger] TaskOrchestrationContext context
    )
    {
        ILogger logger = context.CreateReplaySafeLogger(_orchestration_name_);
        Product product = context.GetInput<Product>() ?? new Product();
        product.ActivityName = _operation_name_;
        product.InstanceId = context.InstanceId;
        product = await context.CallActivityAsync<Product>(
            nameof(PreProcessAsync),
            product,
            GetOptions(isDisrupted: product.IsDisrupted)
                .WithInstanceId($"{context.InstanceId})-pre")
        );
        if (product.LastState == ActivityState.Deferred)
        {
            var current = await _store.ReadActivityStateAsync(product.Payload.UniqueKey);
            current.AddTrace($"Applying deferred wait of {Settings.WaitTime}");
            await context.CreateTimer(Settings.WaitTime, CancellationToken.None);
            current.AddTrace($"Wait complete.");
            await _store.WriteActivityStateAsync(current);
            product.LastState = ActivityState.unknown;
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
        // else
        // {
        //     var current = await _store.ReadActivityStateAsync(product.Payload.UniqueKey);
        //     if (NowPastLimit(current.TimeStarted, Settings.MaximumActivityTime))
        //     {
        //         current.State = ActivityState.Stuck;
        //         current.AddTrace($"Pre:  Stuck");
        //         current.AddReason("Activity has exceeded maximum time.");
        //         current.TimestampRecord_UpdateProductStateHistory(product);
        //         await _store.WriteActivityStateAsync(current);
        //         // The current activity cannot be cancelled.
        //         // the current SubOrchestrator can be terminated, but this will not cancel the activity.

        //     }
        // }
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
                GetOptions(isDisrupted: product.IsDisrupted)
                    .WithInstanceId($"{context.InstanceId})-post")
            );
            if (product.LastState == ActivityState.Failed)
            {
                throw new FlowManagerFatalException(product.Errors);
            }
            return product;
        }
        else
        {
            return product;
        }
    }
}
