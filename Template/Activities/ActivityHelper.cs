using Degreed.SafeTest;
using Models;
using Microsoft.DurableTask;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Functions.Worker;
using System.Diagnostics;

namespace Activities;
public static class ActivityHelper
{
    internal static TimeSpan _tiny_delay = TimeSpan.FromSeconds(10);

    internal const string _finish_processor_name_ = nameof(FinishAsync);
 
    [DebuggerStepThrough]
    internal static async Task<TaskOptions> GetRetryOptionsAsync(string activityName, Product product)
    {
        if (product.IsDisrupted)
        {
            activityName = "Test";
        }
        ActivitySettings settings = await _store.ReadActivitySettingsAsync(activityName);
        product.NextTimeout = TimeSpan.FromHours(settings.ActivityTimeout.GetValueOrDefault());
        RetryPolicy policy = new RetryPolicy(
            settings.NumberOfRetries.GetValueOrDefault(),
            TimeSpan.FromHours(settings.InitialDelay.GetValueOrDefault()),
            settings.BackOffCoefficient.GetValueOrDefault(),
            TimeSpan.FromHours(settings.MaximumDelay.GetValueOrDefault()),
            TimeSpan.FromHours(settings.RetryTimeout.GetValueOrDefault())
        );
        return new TaskOptions(TaskRetryOptions.FromRetryPolicy(policy));
    }
    internal static DataStore _store;

    [DebuggerStepThrough]
    static ActivityHelper()
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
    /// <summary>
    /// After each of the activities has completed, the overall state will be Completed or Failed.
    /// The Finishing method converts this to a Successful or Unsuccessful status, based on the state
    /// of the final activity.
    /// </summary>
    /// <param name="product"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    [Function(nameof(FinishAsync))]
    public static async Task<Product> FinishAsync(
        [ActivityTrigger] Product product,
        FunctionContext context
    )
    {
        if (product.IsRedundant)
        {
            return product;
        }
        var uniqueKey = product.UniqueKey;
        string iid = context.InvocationId.Substring(0, 8);
        var current = await _store.ReadActivityStateAsync(uniqueKey);
        ; // FINISH
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
        current.TimestampRecord_UpdateProductStateHistory(product);
        current.AddTrace($"Output: {product.Output}");
        await _store.WriteActivityStateAsync(current);
        return product;
    }
}
