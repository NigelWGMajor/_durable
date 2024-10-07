using Degreed.SafeTest;
using Models;
using Microsoft.DurableTask;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Functions.Worker;


namespace Activities;

/// <summary>
/// This class extends any (sub) orchestration to integrate Flow Management
/// in a simple manner.
/// Essentially it wraps the function calls to allow suppression of the
/// calls and error detection.
/// Any orchestration class that needs this should derive from this class.
/// </summary>
public static class BaseActivities
{
    internal static TaskOptions GetOptions(
        bool longRunning = false,
        bool highMemory = false,
        bool highDataOrFile = false,
        bool isDisrupted = false
    )
    {
        Int32 numberOfRetries;
        TimeSpan initialDelay;
        double backoffCoefficient;
        TimeSpan? maxDelay,
            timeout;

        if (isDisrupted)
        {
            // reduced for emulation and testing
            numberOfRetries = 3;
            initialDelay = TimeSpan.FromMinutes(2);
            backoffCoefficient = 1.0;
            maxDelay = TimeSpan.FromHours(3);
            timeout = TimeSpan.FromMinutes(5);
            Settings.MaximumActivityTime = TimeSpan.FromMinutes(5);
            Settings.StickCap = 1;
            Settings.WaitTime = TimeSpan.FromMinutes(2);
        }
        else
        {
            // defaults
            numberOfRetries = 5;
            initialDelay = TimeSpan.FromMinutes(5);
            backoffCoefficient = 2.0;
            maxDelay = TimeSpan.FromHours(3);
            timeout = TimeSpan.FromHours(2);

            Settings.MaximumActivityTime = TimeSpan.FromHours(12);
            Settings.StickCap = 2;
            Settings.WaitTime = TimeSpan.FromMinutes(2); //!  need to adjust to 10
            // overrides are cumulative
            if (highDataOrFile)
            { // increased latency, lengthen recovery period
                initialDelay = TimeSpan.FromMinutes(10);
                timeout = TimeSpan.FromHours(8);
            }
            if (longRunning)
            { // allow to run longer and retry more
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
            throw new FlowManagerRetryableException("MetadataStore connection not provided");
    }

    internal static bool MatchesDisruption(string s, Disruptions d)
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
        ActivityRecord current = new ActivityRecord();
        try
        {
            current = await _store.ReadActivityStateAsync(uniqueKey);
            if (product.IsDisrupted)
            {
                product.PopDisruption();
                if (MatchesDisruption(product.NextDisruption, Disruptions.Wait))
                {
                    throw new FlowManagerRetryableException(
                        "Pre: Metadata store not available (emulated)."
                    );
                }
                else if (MatchesDisruption(product.NextDisruption, Disruptions.Crash))
                {
                    throw new FlowManagerFatalException("Pre: fatal exception (emulated)."); 
                }
            }
        }
        catch (FlowManagerRetryableException ex)
        {
            // This catches a metadata failure - whether emulated or resulting from the first read.
            // We assume there is no metadata, so any status will need to be communicated via the product.
            product.Errors = ex.Message;
            product.LastState = ActivityState.Deferred;
            // any state other than active will allow the product to start-as-new in the orchestrator.
            // we choose deferred to give the metadata store time to recover.
            return product;
        }
        if (current.State == ActivityState.Finished)
        {
            // This has already been run!
            current.State = ActivityState.Redundant;
        }
        else
        {
            current.MarkStartTime();
            current.ProcessId = $"{product.Payload.UniqueKey}"; //|{product.Payload.Id}";
        }
        // If we had a metadata failure when trying to read, the product should have an error reporting that.
        // If the record never existed, it will come through as unknown and we will have errors in the product.

        switch (current.State)
        {
            case ActivityState.unknown:
                // this is a brand new record, never saved to the database yet
                if (String.IsNullOrEmpty(current.ActivityName))
                {
                    current.ActivityName = product.ActivityName;
                    current.OperationName = product.OperationName;
                }
                current.SequenceNumber = 0;
                current.UniqueKey = uniqueKey;
                if (product.Errors.Length > 0)
                {
                    // it looks like there was a metadata failure at incept.
                    current.Trace($"{product.Errors}");
                        current.SyncRecordAndProduct(product);
                        await _store.WriteActivityStateAsync(current);
                    product.Errors = "";
                    return product;
                }
                else
                {
                    current.State = ActivityState.Ready;
                }
                current.Trace(
                    $"Pre: New Operation {product.OperationName}::{product.ActivityName}"
                );
                break;
            case ActivityState.Deferred:
                // in these cases regard as Ready.
                // current.MarkStartTime();
                current.State = ActivityState.Ready;
                current.Trace($"Pre: Deferred for resources");
                current.Count++;
                break;
            case ActivityState.Ready:
                //current.MarkStartTime();
                current.State = ActivityState.Active;
                current.Count++;
                current.Trace($"Pre: Awaiting execution");
                break;
            case ActivityState.Redundant:
                // the item is blocked from this execution thread.
                // ideally we black-ice this thread...
                product.LastState = ActivityState.Redundant;
                current.Trace($"Pre: Returned as Redundant");
                await _store.WriteActivityStateAsync(current);
                return product;
            case ActivityState.Active:
                // another instance is already active
                // if we have exceeded the maximum activity time, we should regard this as stuck
                if (NowPastLimit(current.TimeStarted, Settings.MaximumActivityTime))
                {
                    current.State = ActivityState.Stuck;
                    current.Trace($"Pre: Stuck because maximum run time exceeded");
                }
                else
                {
                    // otherwise we should mark this instance as redundant
                    current.State = ActivityState.Redundant;
                    var threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                    current.Trace($"Pre: Re-entrant thread {threadId} rejected");
                }
                break;
            case ActivityState.Stuck:
                // this should never occur
                current.Trace(
                    "Pre: Activity State 'Stuck' encountered in preprocessor: unexpected dev error!"
                );
                await _store.WriteActivityStateAsync(current);
                throw new FlowManagerFatalException("Stuck activity detected in PreProcessor!");
            case ActivityState.Stalled: // this is handled by the durable function framework.
                current.Count++;
                current.State = ActivityState.Ready;
                current.Trace($"Pre: Stalled on Retryable failure");
                // }
                break;
            case ActivityState.Completed:
                // this typically means that the previous activity was successful.
                // current.MarkStartTime();
                current.Count = 0;
                current.ActivityName = product.ActivityName;
                current.OperationName = product.OperationName;
                current.State = ActivityState.Ready;
                current.Trace($"Pre: Completed successfully");
                break;
            case ActivityState.Failed:
                current.Trace($"Pre: Failed fatally");
                break;
            case ActivityState.Finished:
                current.Trace($"Pre: Re-entrant call rejected on Finished activity");
                product.LastState = ActivityState.Redundant;
                return product;
        }

        // Resource checking:
        // At present it is not possible to determine the load on a durable function directly.
        // We could however possibly count the number of activities flagged as memory-intensive that are in-flight
        // and defer on that basis.
        if (AreResourcesStressed())
        {
            current.Trace($"pre: Deferring execution for resources");
            current.State = ActivityState.Deferred;
        }
        current.SyncRecordAndProduct(product);
        await _store.WriteActivityStateAsync(current);
        return product;
    }

    internal async static Task<Product> InjectEmulations(Product product) 
    {
        if (product.IsDisrupted)
        {
            if (MatchesDisruption(product.NextDisruption, Disruptions.Pass))
            {
                product.LastState = ActivityState.Completed;
            }
            else if (MatchesDisruption(product.NextDisruption, Disruptions.Fail))
            {
                throw new FlowManagerFatalException("Activity: Fatal Exception (emulated).");
            }
            else if (MatchesDisruption(product.NextDisruption, Disruptions.Stall))
            {
                throw new FlowManagerRetryableException("Activity: Retryable Exception (emulated).");
            }
            else if (MatchesDisruption(product.NextDisruption, Disruptions.Crash))
            {
                throw new Exception("Activity: Exception (emulated).");
            }
            else if (MatchesDisruption(product.NextDisruption, Disruptions.Drag))
            {
                product.Errors = "Activity: Drag at half maximum time (emulated).";
                await Task.Delay(Settings.MaximumActivityTime / 2.0);
                // This should be allowed to complete successfully!
            }
            else if (MatchesDisruption(product.NextDisruption, Disruptions.Stick))
            {
                product.Errors = "Activity: Stick at double maximum time (emulated).";
                await Task.Delay(Settings.MaximumActivityTime * 2);
                // This should be abandoned and a new operation take over
            }
        }
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
                current.Trace("Activity stalled with non-fatal error.");
                await _store.WriteActivityStateAsync(current);
                throw new FlowManagerRetryableException(
                    $"Post: Retryable FlowManager Exception: {product.ActivityHistory}"
                );
            case ActivityState.Failed: // throw up to orchestrator.
                current.Trace("Activity failed with fatal error.");
                await _store.WriteActivityStateAsync(current);
                throw new FlowManagerFatalException(
                    $"Post: Fatal FlowManager Exception: {product.ActivityHistory}"
                );
            default:
                current.Trace("Post: Activity completed successfully.");
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
        if (product.Errors.Length == 0)
            current.Trace("Final: All activities completed without error.");
        else
            current.Trace("Final: All Activities completed, see errors.");
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
