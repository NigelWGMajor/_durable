using System.Net.Sockets;
using System.Threading.Tasks;
using Degreed.SafeTest;
using Models;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc.Diagnostics;
using System.Diagnostics;
using Microsoft.Azure.Functions.Worker;
using System.Runtime.InteropServices;

namespace Orchestrations;

/// <summary>
/// This class extends any (sub) orchestration to integrate Flow Management
/// in a simple manner.
/// Essentially it wraps the function calls to allow suppression of the
/// calls and error detection.
/// Any orchestration class that needs this should derive from this class.
/// </summary>
public static class BaseOrchestration
{
    private static DataStore? _store;

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
            throw new FlowManagerRetryableException("MetadataStore connection not provided");
    }
/// <summary>
/// This checks and updates metadata prior to calling an activity.
/// If the product.LastState == Active on return, the orchestrator should 
/// launch the activity. 
/// </summary>
    [Function(nameof(PreProcessAsync))] 
    public static async Task<Product> PreProcessAsync(
        [ActivityTrigger]
        //string activityName, // this is the name of the required activity as seen by the Orchestration framework
        Product product, // this contains the inbound data
        FunctionContext context
    )
    {
        // this is the safety wrapper for the activity.
        // This controls whether the activity is even fired,
        // and works with the metadata.
        var keyId = product.Payload.InstanceId;
        // read the current activity
        var current = await _store.ReadActivityStateAsync(keyId);
        // Choose actions dependent on oncoming state
        // // Part 1 of 4: Respond to the Inbound State:
        current.ProcessId = $"{product.Payload.InstanceId}|{product.Payload.Id}";
        switch (current.State)
        {
            case ActivityState.unknown:
                // this is a brand new record, never saved to the database yet
                if (String.IsNullOrEmpty(current.ActivityName))
                {
                    current.ActivityName = product.ActivityName;
                }
                current.MarkStartTime();
                current.InstanceNumber = 0;
                current.KeyId = keyId;
                current.State = ActivityState.Ready;
                current.Notes = "Initial record";
                break;
            case ActivityState.Deferred:
                // in these cases regard as Ready.
                current.MarkStartTime();
                current.State = ActivityState.Ready;
                current.Notes = "Deferred for possible resource depletion";
                current.Count++;
                break;
            case ActivityState.Ready:
                 
                current.State = ActivityState.Active;
                current.Notes = "Pending Execution";
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
                    current.Notes = "maximum activity run time exceeded";
                }
                else
                {
                    // otherwise we should mark this instance as redundant
                    current.State = ActivityState.Redundant;
                    current.Notes = "Re-entrant behavior detected";
                }
                break;
            case ActivityState.Stuck:
                // this should never occur
                break;
            case ActivityState.Stalled:
                current.Count++;
                if (current.InstanceNumber > Settings.StallCap)
                {
                    current.MarkEndTime();
                    current.State = ActivityState.Failed;
                    current.Notes = "Maximum retry count exceeded";

                }
                else
                {
                    current.MarkStartTime();
                    current.State = ActivityState.Ready;
                    current.Notes = "Retrying after retryable failure";
                }
                break;
            case ActivityState.Completed:
                // this typically means that the previous activity was successful.
                current.ActivityName = product.ActivityName;
                current.State = ActivityState.Ready;
                current.Notes = "Completed successfully";
                break;
            case ActivityState.Failed:
                // ===
                current.Notes = "Failed fatally";
                break;
        }

        // Resource checking:
        if (AreResourcesStressed())
        {
            if (current.InstanceNumber < Settings.DelayCap)
            {
                current.State = ActivityState.Deferred;
            }
        }
        current.SyncRecordAndProduct(product);        
        await _store.WriteActivityStateAsync(current);
        return product;
    }
    [Function(nameof(PostProcessAsync))] 
    public static async Task<Product> PostProcessAsync([ActivityTrigger] Product product)
    {
          
        var keyId = product.Payload.InstanceId;
        var current = await _store.ReadActivityStateAsync(keyId);
        current.MarkEndTime();
        current.State = product.LastState;
        current.SyncRecordAndProduct(product);
        await _store.WriteActivityStateAsync(current);
        switch (current.State)
        {
            case ActivityState.Stalled: // defer to prevailing Durable Function retry policy
                throw new FlowManagerRetryableException($"Retryable FlowManager Exception: {product.ActivityHistory}");
            case ActivityState.Failed: // throw up to orchestrator.
                throw new FlowManagerFatalException($"Fatal FlowManager Exception: {product.ActivityHistory}");
            default:
            return product;

        }
    }
    [Function(nameof(FinishAsync))] 
    public static async Task<Product> FinishAsync([ActivityTrigger] Product product)
    {
        var keyId = product.Payload.InstanceId;
        var current = await _store.ReadActivityStateAsync(keyId);
        current.SyncRecordAndProduct(product);
        current.MarkEndTime();
        current.State = ActivityState.Finished;
        current.SyncRecordAndProduct(product);
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
