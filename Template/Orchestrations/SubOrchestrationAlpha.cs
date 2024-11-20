using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using static TestActivities;
using Models;

namespace Orchestrations;

public static class SubOrchestrationAlpha // rename this and the file to match the orchestration name
{
    private const string _activity_name_ = nameof(ActivityAlpha); // rename this to match the activity name
    private const string _orchestration_name_ = nameof(OrchestrationAlpha); // rename this appropriately

    // Retry settings

    private static TaskOptions _activityOptions = new TaskOptions(
        retry: new TaskRetryOptions(
            new RetryPolicy(
                maxNumberOfAttempts: 8,
                firstRetryInterval: TimeSpan.FromHours(0.1),
                backoffCoefficient: 1.4142,
                maxRetryInterval: TimeSpan.FromHours(2.5),
                retryTimeout: TimeSpan.FromHours(12)
            )
        )
    );

    private static TimeSpan _infraDelay = TimeSpan.FromHours(0.2);

    [Function(_orchestration_name_)]
    public static async Task<Product> OrchestrationAlpha( // Rename this function to match the operation name
        [OrchestrationTrigger] TaskOrchestrationContext context
    )
    {
        ILogger logger = context.CreateReplaySafeLogger(_orchestration_name_);
        Product product = context.GetInput<Product>() ?? new Product("");
        product.ActivityName = _activity_name_;
        try
        {
            var executionTask = context.CallActivityAsync<Product>(
                _activity_name_,
                product,
                _activityOptions
            );
            product = await executionTask;
            if (product.LastState == ActivityState.Stuck)
            {
                throw new FlowManagerRecoverableException($"The activity {_activity_name_} is stuck.");
            }
            else if (product.LastState == ActivityState.Deferred)
            {
                TimeSpan delay = _infraDelay; 
                await context.CreateTimer(delay, CancellationToken.None);
                context.ContinueAsNew(product);
                return product;
            }
            else if (product.LastState == ActivityState.Failed)
            {
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
