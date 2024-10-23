using Degreed.SafeTest;
using Models;
using Microsoft.DurableTask;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Functions.Worker;
using System.Diagnostics;
using Microsoft.Azure.Functions.Worker.Http;

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
    // use to prevent timeout:
    internal static TimeSpan _tiny_delay = TimeSpan.FromSeconds(10);
    internal static TimeSpan _test_delay = TimeSpan.FromMinutes(1);

    // used to induce timeout:
    internal static TimeSpan _big_delay = TimeSpan.FromMinutes(2);
    internal static TimeSpan _short_delay = TimeSpan.FromHours(1);
    internal static TimeSpan _long_delay = TimeSpan.FromHours(12);

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
    [DebuggerStepThrough]
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
            numberOfRetries = 5;
            initialDelay = TimeSpan.FromMinutes(1);
            backoffCoefficient = 1.0;
            maxDelay = TimeSpan.FromHours(3);
            timeout = TimeSpan.FromHours(1);
            Settings.MaximumActivityTime = TimeSpan.FromMinutes(20);
            Settings.StickCap = 5;
            Settings.WaitTime = TimeSpan.FromMinutes(1.5);
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
            throw new FlowManagerRecoverableException("MetadataStore connection not provided");
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
            if (current.State == ActivityState.Stuck || current.State == ActivityState.Stalled)
            {
                // If the current state is Stalled or Stuck, the Product does not have the latest state, because it was never returned.
                // This is because the activity was thrown back to the orchestrator to retry or fail.
                // In this case, the last disruption is still stacked. If we have encountered multiple stick or stall injected disruptions,
                // we will have more than one to unstack.
                for (int i = 0; i < current.RetryCount; i++)
                {
                    product.PopDisruption();
                }
            }
            if (product.IsDisrupted)
            {
                if (product.ActivityName != current.ActivityName)
                {
                    // This is a new activity, so we should rotate any disruptions for this activity
                    product.PopDisruption();
                }
            }
            if (MatchesDisruption(product.NextDisruption, Disruption.Wait))
            { // This emulates unavailable metadata so should occur here in the preprocessing loop.
                current.AddTrace("Metadata store not available (emulated)");
                throw new FlowManagerRecoverableException(
                    "Pre:  Metadata store not available (emulated)."
                );
            }
        }
        catch (FlowManagerRecoverableException ex)
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
            current.AddTrace($"Returned as Redundant");
            await _store.WriteActivityStateAsync(current);
            return product;
        }
        else
        {
            //! current.MarkStartTime();
            current.ProcessId = $"{Environment.CurrentManagedThreadId}"; //|{product.Payload.Id}";
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
                    current.SequenceNumber = -1;
                    current.AddTrace(
                        $"(Initial) {product.Errors} detected during initial processing."
                    );
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
                current.AddTrace($"(New) {product.OperationName}");
                break;
            case ActivityState.Deferred:
                current.State = ActivityState.Ready; // now we can try again
                current.SequenceNumber++; //!
                current.AddTrace($"Ready after deferred");
                break;
            case ActivityState.Ready:
                current.State = ActivityState.Active; // opens the gate for execution
                current.AddTrace($"{current.ActivityName} activity awaiting execution");
                break;
            case ActivityState.Redundant:
                // the item is blocked from this execution thread. It will be black-iced through the orchestrations.
                return product;
            case ActivityState.Active:
                product.LastState = ActivityState.Redundant;
                var threadId = Environment.CurrentManagedThreadId;
                // we are comparing the thread ids - not sure if they are the same or not...
                current.AddTrace($"Re-entrant thread {threadId}-{current.ProcessId} returned as redundant");
                current.State = ActivityState.Active;
                current.SequenceNumber++; //!
                await _store.WriteActivityStateAsync(current);
                return product;
            // }
            // break;
            case ActivityState.Stuck:
                // this should never occur
                current.AddTrace("Activity State 'Stuck' in preprocessor: unexpected dev error!");
                await _store.WriteActivityStateAsync(current);
                throw new FlowManagerFatalException("Stuck activity detected in PreProcessor");
            case ActivityState.Completed:
                // this typically means that the previous activity was successful.
                current.RetryCount = 0;
                current.ActivityName = product.ActivityName;
                current.OperationName = product.OperationName;
                current.State = ActivityState.Ready;
                current.AddTrace($"Ready for next activity");
                break;
            case ActivityState.Failed:
                // current.AddTrace($"Previous activity failed fatally");
                break;
            case ActivityState.Successful:
                current.AddTrace($"Re-entrant call on already successful operation rejected");
                current.SequenceNumber++; //!
                product.LastState = ActivityState.Redundant;
                return product;
            case ActivityState.Unsuccessful:
                current.AddTrace($"Re-entrant call on already unsuccessful operation rejected");
                current.SequenceNumber++; //!
                product.LastState = ActivityState.Redundant;
                return product;
        }
        current.TimestampRecord_UpdateProductStateHistory(product);
        await _store.WriteActivityStateAsync(current);
        if (AreResourcesStressed(product))
        {
            current.AddTrace($"Deferring execution to conserve resources");
            current.State = ActivityState.Deferred;
            await _store.WriteActivityStateAsync(current);
        }
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
        current.State = product.LastState;
        current.TimestampRecord_UpdateProductStateHistory(product);
        switch (current.State)
        {
            case ActivityState.Stuck:
                // the product will never be returned.
                // A non-zero RetryCount indicates that the activity has been thrown back to the framework to retry or fail.
                current.RetryCount++;
                current.AddTrace(
                    $"{current.ActivityName} activity timed out after {current.RetryCount} {(current.RetryCount == 1 ? "retry" : "retries")}."
                );
                // current.TimestampRecord_UpdateProductStateHistory(product);
                await _store.WriteActivityStateAsync(current);
                throw new FlowManagerRecoverableException(
                    $"Activity {current.ActivityName} timed out."
                );
            case ActivityState.Stalled:
                // the product will never be returned.
                // A non-zero RetryCount indicates that the activity has been thrown back to the framework to retry or fail.
                current.RetryCount++;
                // On return, the state will be read from the current. This should be be the same as when the activity was originally attempted.
                current.State = ActivityState.Active;
                current.SequenceNumber--; //!
                current.AddTrace(
                    $"{current.ActivityName} activity stalled with non-fatal error after {current.RetryCount} {(current.RetryCount == 1 ? "retry" : "retries")}."
                );
                current.SequenceNumber++; //!
                current.TimestampRecord_UpdateProductStateHistory(product);
                await _store.WriteActivityStateAsync(current);
                throw new FlowManagerRecoverableException(product.Errors);
            case ActivityState.Failed: // Will be thrown up to orchestrator.
                current.SequenceNumber--; //! 
                current.AddTrace(
                    $"{current.ActivityName} activity failed with fatal error after {current.RetryCount} {(current.RetryCount == 1 ? "retry" : "retries")}."
                );
                current.SequenceNumber++; //!
                current.State = ActivityState.Unsuccessful;
                await _store.WriteActivityStateAsync(current);
                return product;
            default: // Completed
                current.SequenceNumber--; //!
                current.AddTrace(
                    $"{current.ActivityName} activity completed successfully after {current.RetryCount} {(current.RetryCount == 1 ? "retry" : "retries")}."
                );
                current.SequenceNumber++; //!
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
       // current.TimestampRecord_UpdateProductStateHistory(product);
        if (
            current.State == ActivityState.Completed && product.ActivityName == current.ActivityName
        )
        {
            current.State = ActivityState.Successful;
            current.AddTrace("(Final) All activities successfully completed");
        }
        else
        {
            current.State = ActivityState.Unsuccessful;
            current.AddTrace("(Final) Completed unsuccessfully");
        }
        //current.SequenceNumber++;
        current.TimestampRecord_UpdateProductStateHistory(product);
        current.AddTrace($"Output: {product.Output}");
        await _store.WriteActivityStateAsync(current);
        return product;
    }

    //[DebuggerStepThrough]
    internal async static Task<Product> InjectEmulations(Product product)
    {
        var current = await _store.ReadActivityStateAsync(product.Payload.UniqueKey);
        if (current.State == ActivityState.Stuck || current.State == ActivityState.Stalled)
        { // Either of these will mean that the product was never returned,
            // because an exception was thrown, so the last disruption is still stacked.
            // or if this has been retried, we need to pop the extra disruptions off the stack.
            for (int i = 0; i < current.RetryCount; i++)
            {
                product.PopDisruption();
            }
        }
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
                product.Errors = "Stalled activity (emulated).";
            }
            else if (MatchesDisruption(product.NextDisruption, Disruption.Crash))
            {
                product.LastState = ActivityState.Failed;
            }
            else if (MatchesDisruption(product.NextDisruption, Disruption.Drag))
            {
                product.Errors = "(Long-running activity) Dragging (emulated).";
                await Task.Delay(Settings.MaximumActivityTime / 2.0);
                // This should be allowed to complete successfully!
            }
            else if (MatchesDisruption(product.NextDisruption, Disruption.Stick))
            {
                product.LastState = ActivityState.Stuck;
                product.Errors = "(Timing out activity) Stuck (emulated).";
            }
        }
        return product;
    }

    [DebuggerStepThrough]
    private static bool NowPastLimit(DateTime time, TimeSpan limit)
    {
        var diff = DateTime.UtcNow - time;
        return diff >= limit;
    }

    //[DebuggerStepThrough]
    private static bool AreResourcesStressed(Product product)
    {
        if (MatchesDisruption(product.NextDisruption, Disruption.Choke))
        {
            product.PopDisruption();
            return true;
        }
        // you may add memory and/or cpu stress detectors here
        return false;
    }
    
    internal static async Task<Product> Process(
        Func<Product, Task<Product>> activity,
        Product product,
        TimeSpan timeout
    )
    {
        try
        {
            var current = await _store.ReadActivityStateAsync(product.Payload.UniqueKey);
            bool fakeStuck = false;
            product = await InjectEmulations(product);
            if (product.LastState == ActivityState.Failed)
            {
                throw new FlowManagerFatalException(product.Errors);
            }
            else if (product.LastState == ActivityState.Stalled)
            {
                throw new FlowManagerRecoverableException(product.Errors);
            }
            if (product.LastState == ActivityState.Stuck)
            {
                if (MatchesDisruption(product.NextDisruption, Models.Disruption.Stick))
                {
                    fakeStuck = (current.RetryCount == 0);
                }
                product.LastState = ActivityState.Active;
            }
            if (product.LastState == ActivityState.Active)
            {
                var executionTask = Task.Run(() => activity(product)); 
                var timeoutTask = Task.Delay(timeout);
                var effectiveTask = await Task.WhenAny(executionTask, timeoutTask);
                if (effectiveTask == timeoutTask || fakeStuck)
                {
                    product.LastState = ActivityState.Stuck;
                    throw new FlowManagerRecoverableException(
                        $"Activity exceeded the time allowed{(fakeStuck ? " (emulated)" : "")}."
                    );
                }
                return await executionTask;
            }
            return product;
        }
        catch (FlowManagerFatalException ex)
        {
            product.LastState = ActivityState.Failed;
            product.Errors = $"Fatal error: {ex.Message}";
            return product;
        }
        catch (FlowManagerRecoverableException ex)
        {
            var current = await _store.ReadActivityStateAsync(product.Payload.UniqueKey);
            if (product.LastState == ActivityState.Stuck)
            {
                current.State = ActivityState.Stuck;
            }
            else
            {
                current.State = ActivityState.Stalled;
            }
            current.AddTrace($"Recoverable error: {ex.Message}");
            current.RetryCount++;
            current.TimestampRecord_UpdateProductStateHistory(product);
            await _store.WriteActivityStateAsync(current);
            throw;
        }
    }
}
