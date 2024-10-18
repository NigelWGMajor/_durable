using Microsoft.Azure.Functions.Worker;
using Degreed.SafeTest;
using System.Diagnostics;
using static Activities.BaseActivities;
using System.Threading.Tasks;

// TODO: Refactor this class to be non-static and deterministic:
/* NOTE: These test activities differ only in the calls that are made. */
/* Because each activity is of the form Action(Product, Product), */
/* it is possible to refactor this class into a non-static class */
/* using a deterministic constructor to inject the activity name */
/* and the function delegate: such a class would need to be totally */
/* deterministic and thread safe. */
public static class TestActivities
{
    // These are representative of the activities that do the actual processing.
    // They are not deterministic because they are async and have side effects.
    // They are not thread safe because they are not deterministic.
    // The activities are responsible for swallowing their own errors and populating
    // the product.Errors property, then setting the product.LastState property to
    // ActivityState.Failed if the error is fatal or
    // ActivityState.Stalled if the error is recoverable.
    //
    private static async Task<Product> ExecuteAlpha(Product product)
    {
        // PRODUCT PROCESSING //////////////////////////////////////////////////////////////////
        await Task.Delay(_tiny_delay);
        product.Output += "Alpha was here. ";
        product.LastState = ActivityState.Completed;
        // PRODUCT NOW PROCESSED. //////////////////////////////////////////////////////////////
        return product;
    }

    private static async Task<Product> ExecuteBravo(Product product)
    {
        // PRODUCT PROCESSING //////////////////////////////////////////////////////////////////
        await Task.Delay(_tiny_delay);
        product.Output += "Bravo was here. ";
        product.LastState = ActivityState.Completed;
        // PRODUCT NOW PROCESSED. //////////////////////////////////////////////////////////////
        return product;
    }

    private static async Task<Product> ExecuteCharlie(Product product)
    {
        // PRODUCT PROCESSING //////////////////////////////////////////////////////////////////
        await Task.Delay(_tiny_delay);
        product.Output += "Charlie was here. ";
        product.LastState = ActivityState.Completed;
        // PRODUCT NOW PROCESSED. //////////////////////////////////////////////////////////////
        return product;
    }

    // [DebuggerStepperBoundary]
    [Function(nameof(ActivityAlpha))]
    public static async Task<Product> ActivityAlpha(
        [ActivityTrigger] Product product,
        FunctionContext context
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
                    fakeStuck = true;
                }
                product.LastState = ActivityState.Active;
            }
            if (product.LastState == ActivityState.Active)
            {
                // current.AddTrace($"Executing {nameof(ActivityAlpha)}");
                // current.TimestampRecord_UpdateProductStateHistory(product);
                // await _store.WriteActivityStateAsync(current);
                var executionTask = Task.Run(() => ExecuteAlpha(product)); // points to private function above // points to private function above
                var timeoutTask = Task.Delay(_test_delay); // set to appropriate timeout
                var effectiveTask = await Task.WhenAny(executionTask, timeoutTask);
                if (effectiveTask == timeoutTask || fakeStuck)
                {
                    product.LastState = ActivityState.Stuck;
                    throw new FlowManagerRecoverableException(
                        "Activity exceeded the time allowed."
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
            //current.State = ActivityState.Stalled;
            current.AddTrace($"Recoverable error: {ex.Message}");
            await _store.WriteActivityStateAsync(current);
            throw;
        }
    }

    // [DebuggerStepperBoundary]
    [Function(nameof(ActivityBravo))]
    public static async Task<Product> ActivityBravo(
        [ActivityTrigger] Product product,
        FunctionContext context
    )
    {
        try
        {
            var current = await _store.ReadActivityStateAsync(product.Payload.UniqueKey);
            product = await InjectEmulations(product);
            if (product.LastState == ActivityState.Failed)
            {
                throw new FlowManagerFatalException(product.Errors);
            }
            else if (product.LastState == ActivityState.Stalled)
            {
                if (current.RetryCount == 0)
                { // a stall was injected.
                    throw new FlowManagerRecoverableException(product.Errors);
                }
                else
                { // this needs to be be retried because it was previously stalled.
                    product.LastState = ActivityState.Active;
                }
            }
            else if (product.LastState == ActivityState.Stuck)
            {
                if (current.RetryCount == 0)
                { // a stall was injected.
                    throw new FlowManagerRecoverableException(product.Errors);
                }
                else
                { // this needs to be be retried because it was previously stalled.
                    product.LastState = ActivityState.Active;
                }
            }
            if (product.LastState == ActivityState.Active)
            {
                // current.AddTrace($"Executing {nameof(ActivityBravo)}");
                // current.TimestampRecord_UpdateProductStateHistory(product);
                // await _store.WriteActivityStateAsync(current);
                var executionTask = Task.Run(() => ExecuteBravo(product)); // points to private function above
                var timeoutTask = Task.Delay(_big_delay); // set to appropriate timeout
                var effectiveTask = await Task.WhenAny(executionTask, timeoutTask);
                if (effectiveTask == timeoutTask)
                {
                    throw new FlowManagerRecoverableException(
                        "Activity exceeded the time allowed."
                    );
                }
                else 
                {
                    return await executionTask;
                }
                
            }
            return product;
        }
        catch (FlowManagerFatalException ex)
        { // this will be thrown by the suborchestrator
            product.LastState = ActivityState.Failed;
            product.Errors = $"Fatal error: {ex.Message}";
            return product;
        }
        catch (FlowManagerRecoverableException ex)
        { // this will bubble up to the sub-orchestrator for auto retry
            var current = await _store.ReadActivityStateAsync(product.Payload.UniqueKey);
            current.State = ActivityState.Active;
            current.AddTrace($"Recoverable error: {ex.Message}");
            current.RetryCount++;
            current.TimestampRecord_UpdateProductStateHistory(product);
            await _store.WriteActivityStateAsync(current);
            throw;
        }
    }

    //  [DebuggerStepperBoundary]
    [Function(nameof(ActivityCharlie))]
    public static async Task<Product> ActivityCharlie(
        [ActivityTrigger] Product product,
        FunctionContext context
    )
    {
        try
        {
            var current = await _store.ReadActivityStateAsync(product.Payload.UniqueKey);
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
                product.LastState = ActivityState.Active;
            }
            if (product.LastState == ActivityState.Active)
            {
                // current.AddTrace($"Executing {nameof(ActivityCharlie)}");
                // current.TimestampRecord_UpdateProductStateHistory(product);
                // await _store.WriteActivityStateAsync(current);
                var executionTask = Task.Run(() => ExecuteCharlie(product)); // points to private function above // points to private function above
                var timeoutTask = Task.Delay(_big_delay); // set to appropriate timeout
                var effectiveTask = await Task.WhenAny(executionTask, timeoutTask);
                if (effectiveTask == timeoutTask)
                {
                    throw new FlowManagerRecoverableException(
                        "Activity exceeded the time allowed."
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
            current.State = ActivityState.Stalled;
            current.AddTrace($"Recoverable error: {ex.Message}");
            await _store.WriteActivityStateAsync(current);
            throw;
        }
    }
}
