using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using static TestActivities;
using static Activities.ActivityHelper;
using static Orchestrations.SubOrchestrationAlpha;
using static Orchestrations.SubOrchestrationBravo;
using static Orchestrations.SubOrchestrationCharlie;
using DurableTask.Core.Exceptions;
using Models;

namespace Orchestrations;

public static class TestOrchestration
{
    #region Test Orchestration

    // A Test Orchestration using three predefined sub-orchestrations.

    // RESPONSIBILITIES:
    // make constants for each Sub-Orchestration name
    // make a constant for the first activity name
    // if you are using a custom product change the type references in
    // the orchestrations to match the actual product type
    // and adjust the Start code at the bottom to match the actual product type

    const string _orc_a_name_ = nameof(OrchestrationAlpha);
    const string _orc_b_name_ = nameof(OrchestrationBravo);
    const string _orc_c_name_ = nameof(OrchestrationCharlie);
    const string _first_activity_name_ = nameof(ActivityAlpha);

    // Retry settings

    private static TaskOptions _infraOptions = new TaskOptions(
        retry: new TaskRetryOptions(
#if DEBUG
            new RetryPolicy(
                maxNumberOfAttempts: 2,
                firstRetryInterval: TimeSpan.FromHours(0.01),
                backoffCoefficient: 1.0,
                maxRetryInterval: TimeSpan.FromHours(0.25),
                retryTimeout: TimeSpan.FromHours(0.25)
#else
            new RetryPolicy(
                maxNumberOfAttempts: 10,
                firstRetryInterval: TimeSpan.FromHours(0.25),
                backoffCoefficient: 1.0,
                maxRetryInterval: TimeSpan.FromHours(0.25),
                retryTimeout: TimeSpan.FromHours(12)
#endif
            )
        )
    );

    // The main orchestration that calls the three sub-orchestrations.
    [Function(nameof(RunTestOrchestrator))]
    public static async Task<string> RunTestOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context
    )
    {
        ILogger logger = context.CreateReplaySafeLogger(nameof(RunTestOrchestrator));

        Product product = new Product("");
        if (!context.IsReplaying)
        {
            logger.LogInformation("*** Initializing Product");
            product = context.GetInput<Product>() ?? new Product("");
            product.ActivityName = _first_activity_name_;
        }
        string id = context.InstanceId;
        // sub-orchestration A
        context.SetCustomStatus($"{product.LastState}");
        product = await context.CallSubOrchestratorAsync<Product>(
            _orc_a_name_,
            product,
            _infraOptions
        );
        // sub-orchestration B
        context.SetCustomStatus($"A: {product.LastState}");
        product = await context.CallSubOrchestratorAsync<Product>(
            _orc_b_name_,
            product,
            _infraOptions
        );
        // sub-orchestration C
        context.SetCustomStatus($"B: {product.LastState}");
        product = await context.CallSubOrchestratorAsync<Product>(
            _orc_c_name_,
            product,
            _infraOptions
        );
        // Final processing
        context.SetCustomStatus($"C: {product.LastState}");
        product = await context.CallActivityAsync<Product>(
            _finish_processor_name_,
            product,
            _infraOptions
        );
        context.SetCustomStatus($"D: {product.LastState}");

        logger.LogInformation($"**\r\n*** Ended Main Orchestration as {product.LastState} \r\n**");
        return JsonSerializer.Serialize(product.ActivityHistory, _jsonOptions);
    }

    private static JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true
    };

    #endregion // Main Orchestration

    #region HTTP Endpoint for Test Orchestration

    // An HTTP endpoint to start the Test Orchestration.

    // // RESPONSIBILITIES:
    // 1. Set up the logger
    // 2. Deserialize the input data from the request body
    // 3. Create a new product from the input data
    // 4. Schedule the Test Orchestration with the product data
    // 5. Log the instance ID
    // 6. Return an HTTP 202 response

    [Function("TestOrchestration_HttpStart")]
    public static async Task<HttpResponseData> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext
    )
    {
        ILogger logger = executionContext.GetLogger("TestOrchestration_HttpStart");
        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var inputData = JsonSerializer.Deserialize<InputPayload>(requestBody);
        var product = new Product(requestBody);
        product.LastState = ActivityState.Ready;
        product.Name = inputData?.Name ?? "";
        product.Name = inputData?.Name ?? "";
        product.UniqueKey = inputData?.UniqueKey ?? "";
        product.Disruptions = inputData?.Disruptions ?? new string[0];
        product.HostServer = OrchestrationHelper.IdentifyServer();
        StartOrchestrationOptions options = new StartOrchestrationOptions
        {
            InstanceId =
                $"Main-{inputData?.Name}-{inputData?.UniqueKey}-{DateTime.UtcNow:yy-MM-ddThh:hh:ss:fff}"
        };
        product.InstanceId = options.InstanceId;
        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(RunTestOrchestrator),
            product,
            options,
            CancellationToken.None
        );
        logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);
        var response = req.CreateResponse(System.Net.HttpStatusCode.Accepted);
        await response.WriteAsJsonAsync(new { instanceId });
        return response;
    }
    #endregion // HTTP Endpoint for Test Orchestration
}
