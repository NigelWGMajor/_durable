using Degreed.SafeTest;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Options;
using Orchestrations;
using static Orchestrations.BaseOrchestration;
using static TestActivities;

namespace Orchestrations;

public static class OrchestrationAlpha
{
    private static string _operation_name_ = nameof(StepAlpha);

    [Function(nameof(RunOrchestrationAlpha))]
    public static async Task<Product> RunOrchestrationAlpha(
        [OrchestrationTrigger] TaskOrchestrationContext context
    )
    {
        ILogger logger = context.CreateReplaySafeLogger(nameof(RunOrchestrationAlpha));
        Product product = new Product();
        product.InstanceId =  context.InstanceId;
        TaskOptions options = new TaskOptions();
         
        if (!context.IsReplaying)
        {
            //logger.LogInformation("*** Initializing Product");
            product = context.GetInput<Product>() ?? new Product();
            product.ActivityName = _operation_name_;
        }

        product.Payload.Id = System.Diagnostics.Process.GetCurrentProcess().Id;

        product = await context.CallActivityAsync<Product>(nameof(PreProcessAsync), product, options.WithInstanceId($"{context.InstanceId})-pre"));
        if (product.LastState == ActivityState.Deferred)
            await context.CreateTimer(TimeSpan.FromSeconds(1), CancellationToken.None);
        else if (product.LastState == ActivityState.Redundant)
        {
          //  await context.CreateTimer(TimeSpan.FromHours(1), CancellationToken.None);
            return product;
        }
        else if (product.LastState != ActivityState.Active)
        {
            context.ContinueAsNew(product);
            return product;
        }
        if (
            product.LastState != ActivityState.Redundant
            && product.ActivityName == _operation_name_
        )
        {
            product = await context.CallActivityAsync<Product>(_operation_name_, product, options.WithInstanceId($"{context.InstanceId})-activity"));
            product = await context.CallActivityAsync<Product>(nameof(PostProcessAsync), product, options.WithInstanceId($"{context.InstanceId})-post"));
            return product;
        }
        else
        {
            return product;        
        }
    }
}
