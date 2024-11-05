using Degreed.SafeTest;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using static Activities.BaseActivities;
using static TestActivities;

namespace Orchestrations;

public static class SubOrchestrationAlpha // rename this and the file to match the orchestration name
{
    // constants to tune retry policy:
    private const bool longRunning = false;
    private const bool highMemory = false;
    private const bool highDataOrFile = false;
    private static string _operation_name_ = nameof(ActivityAlpha); // rename this to match the activity name

    private const string _orchestration_name_ = nameof(OrchestrationAlpha); // rename this appropriately

    [Function(_orchestration_name_)]
    public static async Task<Product> OrchestrationAlpha( // Rename this function to match the operation name
        [OrchestrationTrigger] TaskOrchestrationContext context
    )
    {
        ILogger logger = context.CreateReplaySafeLogger(_orchestration_name_);
        Product product = context.GetInput<Product>() ?? new Product();
        product.ActivityName = _operation_name_;
        try
        {
           // double timeout = 0.0001;  //GetTimeout(_operation_name_, product);
           // var cts = new CancellationTokenSource();
           var executionTask = context.CallActivityAsync<Product>(
               _operation_name_,
               product,
               await GetRetryOptionsAsync(_operation_name_, product)
           );
           // var timeoutTask = context.CreateTimer(context.CurrentUtcDateTime.AddHours(timeout), cts.Token);
            //var effectiveTask = await executionTask;
           // if (effectiveTask == timeoutTask)
           // {
           //     throw new FlowManagerRecoverableException($"The activity {_operation_name_} timed out.");
            //}
            //cts.Cancel();
            product = await executionTask;
            if (product.LastState == ActivityState.Stuck)
            {
                throw new FlowManagerRecoverableException($"The activity {_operation_name_} is stuck.");
            }
            else if (product.LastState == ActivityState.Deferred)
            {
                var x = await GetRetryOptionsAsync("InfraTest", product); //! change for prod
                var t = x?.Retry?.Policy?.FirstRetryInterval;
                TimeSpan delay;
                if (t.HasValue)
                    delay = t.Value;
                else
                    delay = TimeSpan.FromMinutes(2);
                await context.CreateTimer(delay, CancellationToken.None);
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
                var t = x?.Retry?.Policy?.FirstRetryInterval;
                TimeSpan delay;
                if (t.HasValue)
                    delay = t.Value;
                else
                    delay = TimeSpan.FromMinutes(2);
                await context.CreateTimer(delay, CancellationToken.None);
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
