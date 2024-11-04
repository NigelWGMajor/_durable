using Microsoft.Azure.Functions.Worker;
using Degreed.SafeTest;
using System.Diagnostics;
using static Activities.BaseActivities;
using System.Threading.Tasks;
using DurableTask.Core;
using Microsoft.Azure.Functions;

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

    # region Processing wrappers

    // // RESPONSIBILITIES:
    // 1. Process the product asynchronously
    // 2. Triage any errors that occur
    // 3. Set product.LastState to
    //      successful: ActivityState.Completed
    //      fatal error: ActivityState.Failed
    //      recoverable error: or ActivityState.Stalled
    // 4. Update the Errors property as needed
    // 5. Return the processed product

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

    # endregion // processing wrappers

    # region Durable Activities

    // These are the activities that are deterministic and thread safe
    // as required by the Azure Functions Worker SDK.
    // They are responsible for calling the actual processing activities
    // and are called by the sub-orchestrators.

    // // RESPONSIBILITIES:
    // 1. Define the Function name for the calling sub-orchestrator
    // 2. Call the actual processing activity asynchronously
    // 3. Return the processed product

    [Function(nameof(ActivityAlpha))]
    public static async Task<Product> ActivityAlpha(
        [ActivityTrigger] Product product,
        FunctionContext context
    )
    {
        try
        {
            return await new SafeActivity(ExecuteAlpha, product).ProcessAsync();
        }
        catch (Exception ex)
        {
            if (ex is FlowManagerInfraException)
            {
                product.LastState = ActivityState.Deferred;
                product.Errors += ex.Message;
                return product; // pass back to sub to delay and continue as new
            }
            else if (ex is FlowManagerRecoverableException)
            {
                throw; // defer to the sub-orchestrator retry policy
            }
            else
            {
                product.LastState = ActivityState.Failed;
                product.Errors += ex.Message;
                return product; // pass back to sub-orchestrator to fail there
            }
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
            return await new SafeActivity(ExecuteBravo, product).ProcessAsync();
        }
        catch (Exception ex)
        {
            if (ex is FlowManagerInfraException)
            {
                product.LastState = ActivityState.Deferred;
                product.Errors += ex.Message;
                return product; // pass back to sub to delay and continue as new
            }
            else if (ex is FlowManagerRecoverableException)
            {
                throw; // defer to the sub-orchestrator retry policy
            }
            else
            {
                product.LastState = ActivityState.Failed;
                product.Errors += ex.Message;
                return product; // pass back to sub-orchestrator to fail there
            }
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
            return await new SafeActivity(ExecuteCharlie, product).ProcessAsync();
        }
        catch (Exception ex)
        {
            if (ex is FlowManagerInfraException)
            {
                product.LastState = ActivityState.Deferred;
                product.Errors += ex.Message;
                return product; // pass back to sub to delay and continue as new
            }
            else if (ex is FlowManagerRecoverableException)
            {
                throw; // defer to the sub-orchestrator retry policy
            }
            else
            {
                product.LastState = ActivityState.Failed;
                product.Errors += ex.Message;
                return product; // pass back to sub-orchestrator to fail there
            }
        }
    }
    #endregion // Durable Activities
}
