using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Degreed.SafeTest;
using static TestActivities;
using static Orchestrations.BaseOrchestration;
using System.Net.Mime;
using System.Diagnostics.Tracing;
using Microsoft.AspNetCore.CookiePolicy;
using static Orchestrations.OrchestrationAlpha;
using static Orchestrations.OrchestrationBravo;
using static Orchestrations.OrchestrationCharlie;
using Azure.Core;

namespace Orchestrations;

public static class SafeOrchestration
{
    // CONSTANTS FOR NORMAL RETRY POLICY
    private static Int32 _number_of_tries_ = 10;
    private static TimeSpan _initial_delay_ = TimeSpan.FromSeconds(60);
    private static double _backoff_coefficient_ = 1.414;
    private static TimeSpan? _max_delay_ = TimeSpan.FromMinutes(10);
    private static TimeSpan? _timeout_ = TimeSpan.FromHours(1);

    private static JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true
    };

    // // //
    // This launches the orchestration.
    //
    [Function(nameof(RunMainOrchestrator))]
    public static async Task<string> RunMainOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context
    )
    {
        ILogger logger = context.CreateReplaySafeLogger(nameof(RunMainOrchestrator));

        Product product = new Product();
        if (!context.IsReplaying)
        {
            logger.LogInformation("*** Initializing Product");
            product = Product.FromContext(context);
            product.ActivityName = nameof(StepAlpha);
        }

        product.Payload.Id = System.Diagnostics.Process.GetCurrentProcess().Id;
        string id = context.InstanceId; //$"{product.Payload.Id}@{DateTime.UtcNow:u}#";

        // we can have different policies for different weights of task.
        RetryPolicy policy = new RetryPolicy(
            _number_of_tries_,
            _initial_delay_,
            _backoff_coefficient_,
            _max_delay_,
            _timeout_
        );
        var options = new TaskOptions(TaskRetryOptions.FromRetryPolicy(policy));
        int index = 3;
        /// /// /// /// /// /// /// /// /// /// /// ///
        ///
        context.SetCustomStatus($"{product.LastState}{index:00}");
        product = await context.CallSubOrchestratorAsync<Product>(
            nameof(RunOrchestrationAlpha),
            product,
            options.WithInstanceId($"{id}Alpha)")
        );
        index += 3;
        context.SetCustomStatus($"{product.LastState}{index:00}");
        if (product.LastState != ActivityState.Redundant)
        {
            product = await context.CallSubOrchestratorAsync<Product>(
                nameof(RunOrchestrationBravo),
                product,
                options.WithInstanceId($"{id}Bravo)")
            );
        }
        index += 3;
        context.SetCustomStatus($"{product.LastState}{index:00}");
        if (product.LastState != ActivityState.Redundant)
        {
            product = await context.CallSubOrchestratorAsync<Product>(
                nameof(RunOrchestrationCharlie),
                product,
                options.WithInstanceId($"{id}Charlie)")
            );
        }
        index++;
        context.SetCustomStatus($"{product.LastState}{index:00}");
        if (product.LastState != ActivityState.Redundant)
        {
            product = await context.CallActivityAsync<Product>(
                nameof(FinishAsync),
                product,
                options.WithInstanceId($"{id}Final)")
            );
        }
        context.SetCustomStatus($"{product.LastState}{index++:00}");
        return JsonSerializer.Serialize(product.ActivityHistory, _jsonOptions);

        ///
        /// /// /// /// /// /// /// /// /// /// /// ///
    }

    [Function("OrchestrationZulu_HttpStart")]
    public static async Task<HttpResponseData> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext
    )
    {
        ILogger logger = executionContext.GetLogger("OrchestrationZulu_HttpStart");
        // Function input comes from the request content (json)
        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var inputData = JsonSerializer.Deserialize<InputData>(requestBody);

        var product = new Product();
        product.LastState = ActivityState.Ready;
        product.Payload.Name = inputData.Name;
        product.Payload.InstanceId = inputData.Identity;

        StartOrchestrationOptions options = new StartOrchestrationOptions
        {
            InstanceId =
                $"Main-{inputData.Name}-{inputData.Identity}-{DateTime.UtcNow:yy-MM-ddThh:hh:ss:fff}"
        };

        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(RunMainOrchestrator),
            product,
            options,
            CancellationToken.None
        );

        logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

        // Returns an HTTP 202 response with an instance management payload.
        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }
}
