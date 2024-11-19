using Degreed.SafeTest;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Models;
using static Activities.ActivityHelper;
using static TestActivities;

namespace Orchestrations;

public static class SubOrchestrationCharlie // rename this and the file to match the orchestration name
{
    private const string _operation_name_ = nameof(ActivityCharlie); // rename this to match the activity name

    private const string _orchestration_name_ = nameof(OrchestrationCharlie); // rename this appropriately

    [FunctionName(_orchestration_name_)]
    public static async Task<Product> OrchestrationCharlie( // Rename this function to match the operation name
        [OrchestrationTrigger] IDurableOrchestrationContext context,
        ILogger logger
    )
    {
        ILogger retrySafeLogger = context.CreateReplaySafeLogger(logger);
        Product product = context.GetInput<Product>() ?? new Product("");
        product.ActivityName = _operation_name_;
        try
        {
           var executionTask = context.CallActivityWithRetryAsync<Product>(
               _operation_name_,
               await GetRetryOptionsAsync(_operation_name_, product),
               product
           );
            product = await executionTask;
            if (product.LastState == ActivityState.Stuck)
            {
                throw new FlowManagerRecoverableException($"The activity {_operation_name_} is stuck.");
            }
            else if (product.LastState == ActivityState.Deferred)
            {
                var x = await GetRetryOptionsAsync("InfraTest", product); //! change for prod
                var t = x?.FirstRetryInterval;
                TimeSpan delay;
                if (t.HasValue)
                    delay = t.Value;
                else
                    delay = TimeSpan.FromMinutes(2);
                await context.CreateTimer(OrchestrationHelper.GetFireTime(delay), CancellationToken.None);
                context.ContinueAsNew(product);
                return product;
            }
            if (product.LastState == ActivityState.Failed)
            {
                return product;
            }
            else if (product.LastState == ActivityState.Deferred)
            {
                var x = await GetRetryOptionsAsync("InfraTest", product); //! change for prod
                var t = x?.FirstRetryInterval;
                TimeSpan delay;
                if (t.HasValue)
                    delay = t.Value;
                else
                    delay = TimeSpan.FromMinutes(2);
                await context.CreateTimer(OrchestrationHelper.GetFireTime(delay), CancellationToken.None);
                context.ContinueAsNew(product);
                return product;
            }
            return product;
        }
        catch (Exception)
        {
            throw;
        }
    }
}
