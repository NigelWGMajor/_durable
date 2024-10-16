using Microsoft.Azure.Functions.Worker;
using Degreed.SafeTest;
using System.Diagnostics;
using static Activities.BaseActivities;

// TODO: Refactor this class to be non-static and deterministic:
/* NOTE: These test activities differ only in the calls that are made. */
/* Because each activity is of the form Action(Product, Product) */
/* it is possible to refactor this class into a non-static class */
/* using a deterministic constructor to inject the activity name */
/* and the function delegate: such a class would need to be totally */
/* deterministic and thread safe. */
public static class TestActivities
{
    // [DebuggerStepperBoundary]
    [Function(nameof(ActivityAlpha))]
    public static async Task<Product> ActivityAlpha(
        [ActivityTrigger] Product product,
        FunctionContext context
    )
    {
        try
        {
            product = await InjectEmulations(product);
            if (product.LastState == ActivityState.Failed)
            {
                throw new FlowManagerFatalException(product.Errors);
            }
            else if (product.LastState == ActivityState.Stalled)
            {
                throw new FlowManagerRetryableException(product.Errors);
            }
            else if (product.LastState == ActivityState.Active)
            {
                // PRODUCT PROCESSING ///////////////////////////////////////////////////////////////////////
                await Task.Delay(TimeSpan.FromSeconds(5));
                // PRODUCT NOW PROCESSED. /////////////////////////////////////////////////////////////////
                product.LastState = ActivityState.Completed;
            }
            return product;
        }
        catch (FlowManagerFatalException ex)
        {
            product.LastState = ActivityState.Failed;
            product.Errors = ex.Message;
            return product;
        }
        catch (FlowManagerRetryableException ex)
        {
            var current = await _store.ReadActivityStateAsync(product.Payload.UniqueKey);
            current.State = ActivityState.Stalled;
            current.AddReason(ex.Message);
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
                {   // a stall was injected.
                    throw new FlowManagerRetryableException(product.Errors);
                }
                else
                {   // this needs to be be retried because it was previously stalled.
                    product.LastState = ActivityState.Active;
                }
            }
            if (product.LastState == ActivityState.Active)
            {
                // PRODUCT PROCESSING ///////////////////////////////////////////////////////////////////////
                await Task.Delay(TimeSpan.FromSeconds(5));
                // PRODUCT NOW PROCESSED. /////////////////////////////////////////////////////////////////
                product.LastState = ActivityState.Completed;
            }
            return product;
        }
        catch (FlowManagerFatalException ex)
        { // this will be thrown by the suborchestrator
            product.LastState = ActivityState.Failed;
            product.Errors = ex.Message;
            return product;
        }
        catch (FlowManagerRetryableException ex)
        { // this will bubble up to the sub-orchestrator for auto retry
            var current = await _store.ReadActivityStateAsync(product.Payload.UniqueKey);
            current.State = ActivityState.Active;
            current.AddReason(ex.Message);
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
            product = await InjectEmulations(product);
            if (product.LastState == ActivityState.Failed)
            {
                throw new FlowManagerFatalException(product.Errors);
            }
            else if (product.LastState == ActivityState.Stalled)
            {
                throw new FlowManagerRetryableException(product.Errors);
            }
            else if (product.LastState == ActivityState.Active)
            {
                // PRODUCT PROCESSING ///////////////////////////////////////////////////////////////////////
                await Task.Delay(TimeSpan.FromSeconds(5));
                /// PRODUCT NOW PROCESSED. /////////////////////////////////////////////////////////////////
                product.LastState = ActivityState.Completed;
            }
            return product;
        }
        catch (FlowManagerFatalException ex)
        {
            product.LastState = ActivityState.Failed;
            product.Errors = ex.Message;
            return product;
        }
        catch (FlowManagerRetryableException ex)
        {
            var current = await _store.ReadActivityStateAsync(product.Payload.UniqueKey);
            current.State = ActivityState.Stalled;
            current.AddReason(ex.Message);
            await _store.WriteActivityStateAsync(current);
            throw;
        }
    }
}
