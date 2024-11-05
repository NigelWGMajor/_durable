using Degreed.SafeTest;
using Models;
using Microsoft.DurableTask;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Functions.Worker;
using System.Diagnostics;
using Microsoft.Azure.Functions.Worker.Http;
using System.Security.Principal;

namespace Activities;

/// <summary>
/// This class extends any (sub) orchestration to integrate Flow Management
/// in a simple manner.
/// Essentially it wraps the function calls to allow suppression of the
/// calls and error detection.
/// Any orchestration class that needs this should derive from this class.
/// </summary>
public static class BaseActivities
{
    // use to prevent timeout:
    internal static TimeSpan _tiny_delay = TimeSpan.FromSeconds(10);
    internal static TimeSpan _test_delay = TimeSpan.FromMinutes(1);

    // used to induce timeout:
    internal static TimeSpan _big_delay = TimeSpan.FromMinutes(2);
    internal static TimeSpan _short_delay = TimeSpan.FromHours(1);
    internal static TimeSpan _long_delay = TimeSpan.FromHours(12);

   // internal const string _pre_processor_name_ = nameof(PreProcessAsync);
   // internal const string _post_processor_name_ = nameof(PostProcessAsync);
    internal const string _finish_processor_name_ = nameof(FinishAsync);
    //internal const string _sub_orchestration_name_ = "default";
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
    static BaseActivities()
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
    internal static bool MatchesDisruption(string s, Disruption d)
    {
        return (s.ToLower() == d.ToString().ToLower());
    }

    [DebuggerStepThrough]
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
        var uniqueKey = product.Payload.UniqueKey;
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
        //current.SequenceNumber++;
        current.TimestampRecord_UpdateProductStateHistory(product);
        current.AddTrace($"Output: {product.Output}");
        await _store.WriteActivityStateAsync(current);
        return product;
    }
}
