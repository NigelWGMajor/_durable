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
using System.Linq;

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

    internal static TaskOptions GetOptions(
        bool longRunning = false,
        bool highMemory = false,
        bool highDataOrFile = false,
        bool isDisrupted = false
    )
    {
        Int32 numberOfRetries = 5;
        TimeSpan initialDelay = TimeSpan.FromMinutes(5);
        double backoffCoefficient = 2.0;
        TimeSpan? maxDelay = TimeSpan.FromHours(3);
        TimeSpan? timeout = TimeSpan.FromHours(2);

        if (isDisrupted)
        {
            numberOfRetries = 5;
            initialDelay = TimeSpan.FromMinutes(2);
            backoffCoefficient = 1.0;
            maxDelay = TimeSpan.FromHours(3);
            timeout = TimeSpan.FromMinutes(10);
        }

        if (highDataOrFile)
        { // increased latency, lengthen recovery period
            initialDelay = TimeSpan.FromMinutes(10);
            timeout = TimeSpan.FromHours(8);
        }
        if (longRunning)
        { // allow to runlonger and retry more
            numberOfRetries = 10;
            initialDelay = TimeSpan.FromMinutes(8);
            backoffCoefficient = 1.4141214;
            timeout = TimeSpan.FromHours(12);
        }
        if (highMemory)
        { // greater chance of resource depletion, allow longer delays for recovery, more retries
            numberOfRetries = 10;
            initialDelay = TimeSpan.FromMinutes(10);
            backoffCoefficient = 1.4141214;
            timeout = TimeSpan.FromHours(6);
        }
        RetryPolicy policy = new RetryPolicy(
            numberOfRetries,
            initialDelay,
            backoffCoefficient,
            maxDelay,
            timeout
        );
        return new TaskOptions(TaskRetryOptions.FromRetryPolicy(policy));
    }
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
    private static bool MatchesDisruption(string s, Disruptions d)
    {
        return (s.ToLower() == d.ToString().ToLower());
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
        var uniqueKey = product.Payload.UniqueKey;

        // read the current activity

        var current = await _store.ReadActivityStateAsync(uniqueKey);
        if (current.State == ActivityState.Finished)
        {
            // This has already been run!
            current.State = ActivityState.Redundant;
        }
        else
        {
            current.MarkStartTime();
            current.ProcessId = $"{product.Payload.UniqueKey}|{product.Payload.Id}";
        }
        // we may need to emulate a metadata failure.
        product.PopDisruption(); 
        if (MatchesDisruption(product.NextDisruption, Disruptions.Wait))
        {   // inject a wait cycle using the durable framework
            throw new FlowManagerRetryableException("Metadata store: Not available (emulated).");
        }
        switch (current.State)
        {
            case ActivityState.unknown:
                // this is a brand new record, never saved to the database yet
                if (String.IsNullOrEmpty(current.ActivityName))
                {
                    current.ActivityName = product.ActivityName;
                }
                current.SequenceNumber = 0;
                current.UniqueKey = uniqueKey;
                current.State = ActivityState.Ready;
                current.Notes = "Initial record";
                break;
            case ActivityState.Deferred:
                // in these cases regard as Ready.
                // current.MarkStartTime();
                current.State = ActivityState.Ready;
                current.Notes = "Deferred for possible resource depletion";
                current.Count++;
                break;
            case ActivityState.Ready:
                //current.MarkStartTime();
                current.State = ActivityState.Active;
                current.Count++;
                current.Notes = "Pending Execution";
                break;
            case ActivityState.Redundant:
                // the item is blocked from this execution thread.
                // ideally we black-ice this thread...
                product.LastState = ActivityState.Redundant;
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
                throw new FlowManagerFatalException("Stuck activity");
            case ActivityState.Stalled: // this is handled by the durable function framework.
                current.Count++;
                current.State = ActivityState.Ready;
                current.Notes = "Retrying after retryable failure";
                // }
                break;
            case ActivityState.Completed:
                // this typically means that the previous activity was successful.
                // current.MarkStartTime();
                current.ActivityName = product.ActivityName;
                current.State = ActivityState.Ready;
                current.Notes = "Completed successfully";
                break;
            case ActivityState.Failed:
                // ===
                current.Notes = "Failed fatally";
                break;
            case ActivityState.Finished:
                product.LastState = ActivityState.Redundant;
                return product;
        }

        // Resource checking:
        if (AreResourcesStressed())
        {
            if (current.SequenceNumber < Settings.ChokeCap)
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
        var uniqueKey = product.Payload.UniqueKey;
        var current = await _store.ReadActivityStateAsync(uniqueKey);
        current.MarkEndTime();
        current.State = product.LastState;
        current.SyncRecordAndProduct(product);
        switch (current.State)
        {
            case ActivityState.Stalled: // defer to prevailing Durable Function retry policy
                current.Notes = "activity stalled with non-fatal error.";
                await _store.WriteActivityStateAsync(current);
                throw new FlowManagerRetryableException(
                    $"Retryable FlowManager Exception: {product.ActivityHistory}"
                );
            case ActivityState.Failed: // throw up to orchestrator.
                current.Notes = "activity failed with fatal error.";
                await _store.WriteActivityStateAsync(current);
                throw new FlowManagerFatalException(
                    $"Fatal FlowManager Exception: {product.ActivityHistory}"
                );
            default:
                current.Notes = "activity completed successfully.";
                await _store.WriteActivityStateAsync(current);
                return product;
        }
    }

    [Function(nameof(FinishAsync))]
    public static async Task<Product> FinishAsync([ActivityTrigger] Product product)
    {
        var uniqueKey = product.Payload.UniqueKey;
        var current = await _store.ReadActivityStateAsync(uniqueKey);
        current.SyncRecordAndProduct(product);
        current.MarkEndTime();
        current.State = ActivityState.Finished;
        if (product.Errors.Count == 0)
            current.Notes = "All activities completed without error.";
        else
            current.Notes = "All Activities completed, see errors.";
        await _store.WriteActivityStateAsync(current);
        current.SyncRecordAndProduct(product);
        return product;
    }

    private static bool NowPastLimit(DateTime time, TimeSpan limit)
    {
        var diff = DateTime.UtcNow - time;
        return diff >= limit;
    }

    private static bool AreResourcesStressed()
    {
        // you may add memory and/or cpu stress detectors here
        return false;
    }
}
