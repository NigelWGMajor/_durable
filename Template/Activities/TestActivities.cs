using Microsoft.Azure.Functions.Worker;
using Degreed.SafeTest;
using System.Diagnostics;
using static Activities.BaseActivities;
using Models;
using DurableTask.Core.Exceptions;
using Activities;
using System.Security.Cryptography.X509Certificates;

//[DebuggerStepThrough]
public static class TestActivities
{
    


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
            // PRODUCT PROCESSING
            if (product.LastState == ActivityState.Active)
            {
            await Task.Delay(TimeSpan.FromSeconds(5));
            // PRODUCT NOW PROCESSED.
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
            // product.LastState = ActivityState.Stalled;
            // product.Errors = ex.Message;
            // return product;
            var current = await _store.ReadActivityStateAsync(product.Payload.UniqueKey);
            current.State = ActivityState.Stalled;
            current.AddReason(ex.Message);
            await _store.WriteActivityStateAsync(current);
            throw;
        }
    }

    [Function(nameof(ActivityBravo))]
    public static async Task<Product> ActivityBravo(
        [ActivityTrigger] Product product,
        FunctionContext context
    )
    {
        try
        {
            if (product.LastState == ActivityState.PostStalled)
            {
                product.PopDisruption();
                product.LastState = ActivityState.Active;
            }
            
            product = await InjectEmulations(product);
            if (product.LastState == ActivityState.Failed)
            {
                throw new FlowManagerFatalException(product.Errors);
            }
            if (product.LastState == ActivityState.Stalled)
            {   // a disruptino has been injected to 
                throw new FlowManagerRetryableException(product.Errors);
            }
            else if (product.LastState == ActivityState.Active)
            {
            // PRODUCT PROCESSING!
            await Task.Delay(TimeSpan.FromSeconds(5));
            // PRODUCT NOW PROCESSED.
            product.LastState = ActivityState.Completed;
                return product;
            }
            if (MatchesDisruption(product.NextDisruption, Disruption.Stall))
            {
                throw new FlowManagerRetryableException(product.Errors);
            }
            else if (product.LastState == ActivityState.Stalled)
            {
                throw new FlowManagerRetryableException(product.Errors);
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
            current.State = ActivityState.PostStalled;
            current.AddReason(ex.Message);
            current.AddTrace("Activity Bravo throws retryable exception");
            await _store.WriteActivityStateAsync(current);
            throw;
            //product.LastState = ActivityState.PostStalled;
            //return product;
        }
    }

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
            // PRODUCT PROCESSING
            if (product.LastState == ActivityState.Active)
            {
            await Task.Delay(TimeSpan.FromSeconds(5));
            // PRODUCT NOW PROCESSED.
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
