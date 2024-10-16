using Degreed.SafeTest;
using Models;
using Microsoft.DurableTask;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Functions.Worker;
using System.Diagnostics;

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
    /// <summary>
    /// The options generated for tuning retries are decided here based on some generic inputs.
    /// If the operation is Disrupted (i.e. emulated disruptions have been injected for testing)
    /// then a set of values is provided to expedite integration testing. Otherwise defaults
    /// are established, and some or all of these may be overridden by the presence of flags specifying
    /// that the activity is Long Running, has High Memory Usage or has intensive data or file IO.
    /// </summary>
    /// <param name="longRunning"></param>
    /// <param name="highMemory"></param>
    /// <param name="highDataOrFile"></param>
    /// <param name="isDisrupted"></param>
    /// <returns></returns>
    //[DebuggerStepThrough]
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
            Settings.WaitTime = TimeSpan.FromMinutes(10); //!  need to adjust to 10
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

    internal static DataStore _store;

    [DebuggerStepThrough]
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

    [DebuggerStepThrough]
    internal static bool MatchesDisruption(string s, Disruption d)
    {
        return (s.ToLower() == d.ToString().ToLower());
    }

    //[DebuggerStepperBoundary]
    [Function(nameof(PreProcessAsync))]
    public static async Task<Product> PreProcessAsync(
        [ActivityTrigger]
        //string activityName, // this is the name of the required activity as seen by the Orchestration framework
        Product product, // this contains the inbound data
        FunctionContext context
    )
    {
        string iid = context.InvocationId.Substring(0, 8);
        var uniqueKey = product.Payload.UniqueKey;
        if (uniqueKey.Length == 0)
        { // this can occur when an exception is thrown in the orchestrator
            // we want to just ignore this completely.
            product.LastState = ActivityState.Redundant;
            return product;
        }
        // read the current activity
        ActivityRecord current = new ActivityRecord();
        try
        {
            current = await _store.ReadActivityStateAsync(uniqueKey);
            ; // PREPROCESS
            if (product.IsDisrupted)
            {
                if (product.ActivityName != current.ActivityName)
                {
                    // This is a new activity, so we should rotate any disruptions for this activity
                    product.PopDisruption();
                }
                else if (
                    product.LastState == ActivityState.Active
                    && MatchesDisruption(product.NextDisruption, Disruption.Stall)
                )
                {
                    // When auto-retry is used (a Stall disruption) no product is returned so we need to ignore the
                    // current disruption if it is a stall
                    if (current.RetryCount == 1)
                        product.PopDisruption();
                }
                if (MatchesDisruption(product.NextDisruption, Disruption.Wait))
                {
                    current.AddReason("Metadata store not available (emulated Wait).");
                    throw new FlowManagerRetryableException(
                        "Pre:  Metadata store not available (emulated)."
                    );
                }
            }
        }
        catch (FlowManagerRetryableException ex)
        {
            // This catches a metadata failure - whether emulated or resulting from the first read.
            // We assume there is no metadata, so any status will need to be communicated via the product.
            product.Errors = ex.Message;
            product.LastState = ActivityState.Deferred;
            return product;
        }
        catch (FlowManagerFatalException ex)
        {
            product.Errors = ex.Message;
            product.LastState = ActivityState.Failed;
            return product;
        }
        if (
            current.State == ActivityState.Successful || current.State == ActivityState.Unsuccessful
        )
        {
            // This has already been run!
            product.LastState = ActivityState.Redundant;
            current.AddTrace($"Pre:  Returned as Redundant");
            await _store.WriteActivityStateAsync(current);
            return product;
        }
        else
        {
            current.MarkStartTime();
            current.ProcessId = $"{product.Payload.UniqueKey}"; //|{product.Payload.Id}";
        }
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
                // If we had a metadata failure when trying to read, the product should have an error reporting that.
                // If the record never existed, it will come through as unknown and we will have errors recorded in the product itself.
                if (product.Errors.Length > 0)
                {
                    // it looks like there was a metadata failure at incept.
                    current.AddTrace($"{product.Errors} detected during initial processing.");
                    current.AddReason($"{product.Errors}");
                    current.TimestampRecord_UpdateProductStateHistory(product);
                    current.State = product.LastState;
                    await _store.WriteActivityStateAsync(current);
                    product.Errors = "";
                    return product;
                }
                else
                {
                    current.State = ActivityState.Ready;
                }
                current.AddTrace(
                    $"Pre:  New Operation {product.OperationName}::{product.ActivityName}"
                );
                break;
            case ActivityState.Deferred:
                current.State = ActivityState.Ready; // now we can try again
                current.AddTrace($"Pre:  Activity {current.ActivityName} deferred for resources");
                break;
            case ActivityState.Ready:
                current.State = ActivityState.Active; // opens the gate for execution
                current.AddTrace($"Pre:  Activity {current.ActivityName} awaiting execution");
                break;
            case ActivityState.Redundant:
                // the item is blocked from this execution thread. It will be black-iced through the orchestrations.
                return product;
            case ActivityState.Active:
                // another instance is already active
                // if we have exceeded the maximum activity time, we should regard this as stuck
                if (NowPastLimit(current.TimeStarted, Settings.MaximumActivityTime))
                {
                    current.State = ActivityState.Stuck;
                    current.AddTrace($"Pre:  Stuck because maximum run time exceeded");
                }
                else
                {
                    // otherwise we should mark this instance as redundant
                    product.LastState = ActivityState.Redundant;
                    var threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                    current.AddTrace($"Pre:  Re-entrant thread {threadId} rejected");
                    await _store.WriteActivityStateAsync(current);
                    return product;
                }
                break;
            case ActivityState.Stuck:
                // this should never occur
                current.AddTrace(
                    "Pre:  Activity State 'Stuck' encountered in preprocessor: unexpected dev error!"
                );
                await _store.WriteActivityStateAsync(current);
                throw new FlowManagerFatalException("Stuck activity detected in PreProcessor");
            case ActivityState.Completed:
                // this typically means that the previous activity was successful.
                current.RetryCount = 0;
                current.ActivityName = product.ActivityName;
                current.OperationName = product.OperationName;
                current.State = ActivityState.Ready;
                current.AddTrace($"Pre:  Previous activity completed successfully");
                break;
            case ActivityState.Failed:
                current.AddTrace($"Pre:  Previous activity failed fatally");
                break;
            case ActivityState.Successful:
                current.AddTrace(
                    $"Pre:  Re-entrant call rejected on successfully finished operation"
                );
                product.LastState = ActivityState.Redundant;
                return product;
            case ActivityState.Unsuccessful:
                current.AddTrace(
                    $"Pre:  Re-entrant call rejected on unsuccessfully completed operation"
                );
                product.LastState = ActivityState.Redundant;
                return product;
        }
        if (AreResourcesStressed())
        {
            current.AddTrace($"Pre:  Deferring execution to conserve resources");
            current.State = ActivityState.Deferred;
        }
        current.TimestampRecord_UpdateProductStateHistory(product);
        await _store.WriteActivityStateAsync(current);
        return product;
    }

   // [DebuggerStepperBoundary]
    [Function(nameof(PostProcessAsync))]
    public static async Task<Product> PostProcessAsync(
        [ActivityTrigger] Product product,
        FunctionContext context
    )
    {
        string iid = context.InvocationId.Substring(0, 8);
        var uniqueKey = product.Payload.UniqueKey;
        var current = await _store.ReadActivityStateAsync(uniqueKey);
        ; // POSTPROCESS
        current.MarkEndTime();
        current.State = product.LastState;
        current.TimestampRecord_UpdateProductStateHistory(product);
        switch (current.State)
        {
            case ActivityState.Stalled:
                // the product will never be returned.
                // A non-zero RetryCount indicates that the activity has been thrown back to the framework to retry or fail.
                current.RetryCount++;
                // On return, the state will be read from the current. This should be be the same as when the activity was originally attempted.
                current.State = ActivityState.Active;
                current.AddTrace($"Action {current.ActivityName} stalled with non-fatal error.");
                current.TimestampRecord_UpdateProductStateHistory(product);
                await _store.WriteActivityStateAsync(current);
                throw new FlowManagerRetryableException(product.Errors);
            case ActivityState.Failed: // Will be thrown up to orchestrator.
                current.AddTrace(
                    $"Action {current.ActivityName} failed with fatal error after {current.RetryCount} {(current.RetryCount == 1 ? "retry" : "retries")}."
                );
                current.State = ActivityState.Unsuccessful;
                await _store.WriteActivityStateAsync(current);
                return product;
            default: // Completed
                current.AddTrace(
                    $"Post: Activity {current.ActivityName} completed successfully after {current.RetryCount} {(current.RetryCount == 1 ? "retry" : "retries")}."
                );
                current.RetryCount = 0;
                await _store.WriteActivityStateAsync(current);
                return product;
        }
    }

    //[DebuggerStepperBoundary]
    [Function(nameof(FinishAsync))]
    public static async Task<Product> FinishAsync(
        [ActivityTrigger] Product product,
        FunctionContext context
    )
    {
        var uniqueKey = product.Payload.UniqueKey;
        string iid = context.InvocationId.Substring(0, 8);
        var current = await _store.ReadActivityStateAsync(uniqueKey);
        ; // FINISH
        current.TimestampRecord_UpdateProductStateHistory(product);
        if (
            current.State == ActivityState.Completed && product.ActivityName == current.ActivityName
        )
        {
            current.State = ActivityState.Successful;
            current.AddTrace("Final: Successfully completed");
        }
        else
        {
            current.State = ActivityState.Unsuccessful;
            current.AddTrace("Final: Failed to complete");
        }
        await _store.WriteActivityStateAsync(current);
        current.TimestampRecord_UpdateProductStateHistory(product);
        return product;
    }

    //[DebuggerStepThrough]
    internal async static Task<Product> InjectEmulations(Product product)
    {
        if (product.IsDisrupted)
        {
            if (MatchesDisruption(product.NextDisruption, Disruption.Pass))
            {
                product.LastState = ActivityState.Completed;
            }
            else if (MatchesDisruption(product.NextDisruption, Disruption.Fail))
            {
                product.LastState = ActivityState.Failed;
            }
            else if (MatchesDisruption(product.NextDisruption, Disruption.Stall))
            {
                product.LastState = ActivityState.Stalled;
                product.Errors = "Activity: Stalled (emulated).";
                // this is needed because otherwise stall will be infinite....
                product.PopDisruption();
            }
            else if (MatchesDisruption(product.NextDisruption, Disruption.Crash))
            {
                product.LastState = ActivityState.Failed;
            }
            else if (MatchesDisruption(product.NextDisruption, Disruption.Drag))
            {
                product.Errors = "Activity: Drag at half maximum time (emulated).";
                await Task.Delay(Settings.MaximumActivityTime / 2.0);
                // This should be allowed to complete successfully!
            }
            else if (MatchesDisruption(product.NextDisruption, Disruption.Stick))
            {
                product.Errors = "Activity: Stick at double maximum time (emulated).";
                await Task.Delay(Settings.MaximumActivityTime * 2);
                // This should be abandoned and a new operation take over
            }
        }
        return product;
    }

    [Function(nameof(PostProcessAsync))]
    public static async Task<Product> PostProcessAsync(
        [ActivityTrigger] Product product,
        FunctionContext context
    )
    {
        string iid = context.InvocationId.Substring(0, 8);
        var uniqueKey = product.Payload.UniqueKey;
        var current = await _store.ReadActivityStateAsync(uniqueKey);
        current.MarkEndTime();
        current.State = product.LastState;
        current.SyncRecordAndProduct(product);
        switch (current.State)
        {
           case ActivityState.Stalled:
                throw new FlowManagerRetryableException("Thrown in postprocess");
                current.AddTrace($"Activity {current.ActivityName} stalled with non-fatal error.");
                product.LastState = ActivityState.Stalled;
                current.SyncRecordAndProduct(product);
                    current.State = ActivityState.Stalled; // so that it will retry.
                    await _store.WriteActivityStateAsync(current);
                    return product;
                
                // otherwise set up to retry
            case ActivityState.PostStalled: // defer to prevailing Durable Function retry policy
                current.AddTrace($"Activity {current.ActivityName} stalled with non-fatal error.");
                current.AddReason(product.Errors);
                current.Count++;
                current.SyncRecordAndProduct(product);
                current.State = ActivityState.Ready; // so that it will retry.
                await _store.WriteActivityStateAsync(current);
                throw new FlowManagerRetryableException(
                    $"Post: Retryable FlowManager Exception: {product.ActivityHistory}"
                );
            case ActivityState.Failed: // throw up to orchestrator.
                current.AddTrace($"Activity {current.ActivityName} failed with fatal error.");
                current.State = ActivityState.Unsuccessful;
                await _store.WriteActivityStateAsync(current);
                return product;
            default:
                current.AddTrace($"Post: Activity {current.ActivityName} completed successfully.");
                await _store.WriteActivityStateAsync(current);
                return product;
        }
    }

    [Function(nameof(FinishAsync))]
    public static async Task<Product> FinishAsync(
        [ActivityTrigger] Product product,
        FunctionContext context
    )
    {
        var uniqueKey = product.Payload.UniqueKey;
        string iid = context.InvocationId.Substring(0, 8);
        var current = await _store.ReadActivityStateAsync(uniqueKey);
        current.SyncRecordAndProduct(product);
        current.MarkEndTime();
        if (current.State == ActivityState.Completed)
        {
            current.State = ActivityState.Successful;
            current.AddTrace("Final: Successfully completed");
        }
        else
        {
            current.State = ActivityState.Unsuccessful;
            current.AddTrace("Final: Failed to complete");
        }
        await _store.WriteActivityStateAsync(current);
        current.SyncRecordAndProduct(product);
        return product;
    }

    [DebuggerStepThrough]
    private static bool NowPastLimit(DateTime time, TimeSpan limit)
    {
        var diff = DateTime.UtcNow - time;
        return diff >= limit;
    }

    [DebuggerStepThrough]
    private static bool AreResourcesStressed()
    {
        // you may add memory and/or cpu stress detectors here
        return false;
    }
}
