using Degreed.SafeTest;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Models;
using static Activities.BaseActivities;
using static TestActivities;

namespace Orchestrations;

public static class SubOrchestrationAlpha                                   // rename this and the file to match the orchestration name 
{
    // constants to tune retry policy:
    private const bool longRunning = false;
    private const bool highMemory = false;
    private const bool highDataOrFile = false;
    private static string _operation_name_ = nameof(ActivityAlpha);         // rename this to match the activity name
	
    private const string _orchestration_name_ = nameof(OrchestrationAlpha); // rename this appropriately
        
    [Function(_orchestration_name_)]
    public static async Task<Product> OrchestrationAlpha(                   // Rename this function to match the operation name
        [OrchestrationTrigger] TaskOrchestrationContext context
    )
    {

        ILogger logger = context.CreateReplaySafeLogger(_orchestration_name_);
        Product product; // = new Product();

//        try
  //      {
            product = context.GetInput<Product>() ?? new Product();
            product.ActivityName = _operation_name_;
            product.InstanceId = context.InstanceId;
            //  product.Payload.Id = System.Diagnostics.Process.GetCurrentProcess().Id;

            product = await context.CallActivityAsync<Product>(
                nameof(PreProcessAsync),
                product,
                GetOptions(isDisrupted: product.IsDisrupted).WithInstanceId($"{context.InstanceId})-pre")
            );
            if (product.LastState == ActivityState.Deferred)
            {
                await context.CreateTimer(Settings.WaitTime, CancellationToken.None);
                product.LastState = ActivityState.unknown;
                logger.LogInformation("*** Deferred timer released ***"); //!
                context.ContinueAsNew(product);
                return product;
            }
            else if (product.LastState == ActivityState.Redundant)
            {
                return product;
        }
        else if (product.LastState == ActivityState.PostStalled)
        {   // force the framework to retry
            throw new FlowManagerRetryableException(product.Errors);
            }
            else if (product.LastState != ActivityState.Active)
            {
                context.ContinueAsNew(product);
                return product;
            }
            if (
                product.LastState != ActivityState.Redundant && product.ActivityName == _operation_name_
            )
            {
                product = await context.CallActivityAsync<Product>(
                    _operation_name_,
                    product,
                    GetOptions(longRunning, highMemory, highDataOrFile, product.IsDisrupted)
                        .WithInstanceId($"{context.InstanceId})-activity")
                );
                product = await context.CallActivityAsync<Product>(
                    nameof(PostProcessAsync),
                    product,
                    GetOptions(isDisrupted: product.IsDisrupted).WithInstanceId($"{context.InstanceId})-post")
                );
                if (product.LastState == ActivityState.Failed)
                {
                    throw new FlowManagerFatalException(product.Errors);
                }
            else if (product.LastState == ActivityState.Stalled)
            {
                 /// <summary>
                 ///  set up for a retry without executing again
                 /// </summary>
                 product.LastState = ActivityState.PostStalled;
                 context.ContinueAsNew(product);
                 return product;
            }
            return product;
        }
        else
        {
            return product;
        }
//    }
  //      catch (Exception ex)
    //    {
      //      throw;
        //} 
    }
}
