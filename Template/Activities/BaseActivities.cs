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


// Add this using directive

/// <summary>
/// This class extends any orchestration to integrate Flow Management
/// in a simple manner.
/// Essentially it wraps the function calls to allow suppression of the
/// calls and error detection.
/// Any orchestration class that needs this should derive from this class.
/// </summary>
public static class BaseActivities
{
    private static DataStore? _store;

    static BaseActivities()
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
        var keyId = product.Payload.Identity;
        // read the current activity
        var current = await _store.ReadActivityStateAsync(keyId);
        // Choose actions dependent on oncoming state
        // // Part 1 of 4: Respond to the Inbound State:
        switch (current.State)
        {
            case ActivityState.unknown:
                // this is a brand new record, never saved to the database yet
                current.ActivityName = product.ActivityName;
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
                current.ActivityName = product.ActivityName;
                current.State = ActivityState.Ready;
                break;
            case ActivityState.Failed:
                // ===
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
        if (current.State == ActivityState.Ready)
        {
            current.State = ActivityState.Active;
        }
        
        await _store.WriteActivityStateAsync(current);

        current.UpdateProductState(product);        
        return product;
    }
    [Function(nameof(PostProcessAsync))] 
    public static async Task<Product> PostProcessAsync([ActivityTrigger] Product product)
    {
          
        var keyId = product.Payload.Identity;
        var current = await _store.ReadActivityStateAsync(keyId);
        current.MarkEndTime();
        current.State = product.LastState;
        await _store.WriteActivityStateAsync(current);
        current.UpdateProductState(product);
        return product;
    }
    [Function(nameof(FinishAsync))] 
    public static async Task<Product> FinishAsync([ActivityTrigger] Product product)
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
