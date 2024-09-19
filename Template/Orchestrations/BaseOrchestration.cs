using System.Net.Sockets;
using System.Threading.Tasks;
using Degreed.SafeTest;
using Models;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc.Diagnostics;
using System.Diagnostics;

// Add this using directive

/// <summary>
/// This class extends any orchestration to integrate Flow Management
/// in a simple manner.
/// Essentially it wraps the function calls to allow suppression of the
/// calls and error detection.
/// Any orchestration class that needs this should derive from this class.
/// </summary>
public static class BaseOrchestration
{
    private static DataStore _store;

    static BaseOrchestration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(System.IO.Directory.GetCurrentDirectory())
            .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();

        var configuration = builder.Build();
        string? temp = configuration["Values:MetadataStore"];
        if (temp != null)
            _store = new DataStore(temp);
        else
            throw new FlowManagerException("MetadataStore connection not provided");
    }

    public static async Task<Product> ProcessSafelyAsync(
        string activityName, // this is the name of the required activity as seen by the Orchestration framework
        Product product, // this contains the inbound data
        TaskOrchestrationContext context
    )
    {
        // this is the safety wrapper for the activity.
        // This controls whether the activity is even fired,
        // and works with the metadata.
        var keyId = product.Payload.Identity;
        // read the current activity
        var current = await _store.ReadActivityStateAsync(keyId);
        // Choose actions dependent on oncoming state
        // // Part 1 of 4: Respond to the Inbound State:
        switch (current.State)
        {
            case ActivityState.unknown:
                // this is a brand new record, never saved to the database yet
                current.ActivityName = activityName;
                current.MarkStartTime();
                current.InstanceNumber = 1;
                current.KeyId = keyId;
                current.State = ActivityState.Ready;
                break;
            case ActivityState.Ready:
            case ActivityState.Deferred:
                // in these cases regard as Ready.
                current.MarkStartTime();
                current.State = ActivityState.Ready;
                break;
            case ActivityState.Redundant:
                // the item is blocked from this execution thread.
                // ideally we black-ice this thread...
                // we may not ever encounter this here, but should just return.
                // the plan is that once this is set we just let the orchestrator
                // run to completion and/or terminate it.
                return product;
            case ActivityState.Active:
                // another instance is already active
                // if we have exceeded the maximum activity time, we should regard this as stuck
                if (NowPastLimit(current.TimeStarted, Settings.MaximumActivityTime))
                {
                    current.State = ActivityState.Stuck;
                }
                else
                {
                    // otherwise we should mark this instance as redundant
                    current.State = ActivityState.Redundant;
                }
                break;
            case ActivityState.Stuck:
                // this should never occur
                break;
            case ActivityState.Stalled:
                current.InstanceNumber++;
                if (current.InstanceNumber > Settings.StallCap)
                {
                    current.MarkEndTime();
                    current.State = ActivityState.Failed;
                }
                else
                {
                    current.MarkStartTime();
                    current.State = ActivityState.Ready;
                }
                break;
            case ActivityState.Completed:
                // this typically means that the previous activity was successful.
                current.ActivityName = activityName;
                current.State = ActivityState.Ready;
                break;
            case ActivityState.Failed:
                // ===
                break;
        }
        // // Part 2 of 4: Apply pre-process rules for Outbound Metadata

        // Resource checking:
        if (AreResourcesStressed())
        {
            // we want to wait for a while...
            if (current.InstanceNumber < Settings.DelayCap)
            {
                current.State = ActivityState.Deferred;
            }
        }

        await _store.WriteActivityStateAsync(current);

        // // part 3 of 4: Attempt Activity and/or returns

        if (current.State == ActivityState.Deferred)
        {
            /// use the Durable Function framework to delay the orchestration without wasting compute...
            await context.CreateTimer(Settings.DelayTime, CancellationToken.None);
            return product;
        }
        if (current.State == ActivityState.Redundant || current.State == ActivityState.Ready)
        {
            return product;
        }
        if (current.State == ActivityState.Active)
        {
            current.MarkStartTime();
            current.UpdateProductState(product);
            try
            {
                // This is part of the Durable Function framework that either stores or retrieves the result depending on the IsReplaying status of the orchestration.
                product = await context.CallActivityAsync<Product>(activityName, product);
            }
            catch (Exception ex)
            {
                // the activity's exceptions will be caught by the Durable Functions within whatever retry limits are specified in the retry policy.
                // When those are all exhausted, we might get something here...
                // If this works, we can throw exceptions to ask the system to retry (i.e. we consider this stalled)
                Debugger.Break(); //!
                current.Notes += $"{ex.Message}\n";
                product.LastState = ActivityState.Stalled;
            }
            if (product.LastState == ActivityState.Completed)
            {
                current.MarkEndTime();
                current.State = ActivityState.Completed;
                current.UpdateProductState(product);
            }
            else if (product.LastState == ActivityState.Failed)
            {
                current.MarkEndTime();
                current.State = ActivityState.Failed;
                current.UpdateProductState(product);
            }
            else if (product.LastState == ActivityState.Stalled)
            {
                current.MarkEndTime();
                current.State = ActivityState.Stalled;
                current.UpdateProductState(product);
            }
        }
        // // Part 4 of 4: Update Metadata

        await _store.WriteActivityStateAsync(current);
        ;

        // //! We might test throwing here when stalled!
        // 
        //
        // We might also experiment with throwing into the Orchestration itself, 
        //
        // and also for redundant calls some kind of cancellation/termination.
        return product;
    }

    public static async Task<Product> EndSafelyAsync(Product product)
    {
        var keyId = product.Payload.Identity;
        var current = await _store.ReadActivityStateAsync(keyId);
        current.UpdateProductState(product);
        current.MarkEndTime();
        current.State = ActivityState.Finished;
        current.UpdateProductState(product);
        await _store.WriteActivityStateAsync(current);
        return product;
    }

    private static bool NowPastLimit(DateTime time, TimeSpan limit)
    {
        var diff = DateTime.UtcNow - time;
        return diff <= limit;
    }

    private static bool AreResourcesStressed()
    {
        // you may add memory and/or cpu stress detectors here
        return false;
    }
}
