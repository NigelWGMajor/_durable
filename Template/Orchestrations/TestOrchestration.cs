using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Degreed.SafeTest;
using static TestActivities;
using static Activities.BaseActivities;
using static Orchestrations.SubOrchestrationAlpha;
using static Orchestrations.SubOrchestrationBravo;
using static Orchestrations.SubOrchestrationCharlie;
using DurableTask.Core.Exceptions;

namespace Orchestrations;

public static class TestOrchestration
{
    private static JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true
    };

    #region Test Orchestration

    // A Test Orchestration using three predefined sub-orchestrations.

    // // RESPONSIBILITIES:
    // 1. Set up the logger
    // 2. Initialize the product from the input data in the context
    // 3. For each sub-orchestration:
    //      c. call the sub-orchestration asynchronously
    // 4. Call the final activity
    // 5. Set the custom status

    const string _orc_a_name_ = nameof(OrchestrationAlpha);
    const string _orc_b_name_ = nameof(OrchestrationBravo);
    const string _orc_c_name_ = nameof(OrchestrationCharlie);
    const string _infra_settings_name_ = "Infra";
    const string _infra_test_settings_name_ = "InfraTest";

    public static async Task<TaskOptions> GetLocalRetryOptionsAsync(
        string activityName,
        Product product
    )
    {
        string settingsName = product.IsDisrupted
            ? _infra_test_settings_name_
            : _infra_settings_name_;
        return await GetRetryOptionsAsync(settingsName, product);
    }

    [Function(nameof(RunTestOrchestrator))]
    public static async Task<string> RunTestOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context
    )
    {
        ILogger logger = context.CreateReplaySafeLogger(nameof(RunTestOrchestrator));

        Product product = new Product();
        if (!context.IsReplaying)
        {
            logger.LogInformation("*** Initializing Product");
            product = Product.FromContext(context);
            product.ActivityName = nameof(ActivityAlpha);
        }
        string id = context.InstanceId;

        context.SetCustomStatus($"{product.LastState}");
        product = await context.CallSubOrchestratorAsync<Product>(
            _orc_a_name_,
            product,
            await GetLocalRetryOptionsAsync(_orc_a_name_, product)
        );

        context.SetCustomStatus($"A: {product.LastState}");
        product = await context.CallSubOrchestratorAsync<Product>(
            _orc_b_name_,
            product,
            await GetLocalRetryOptionsAsync(_orc_b_name_, product)
        );

        context.SetCustomStatus($"B: {product.LastState}");
        product = await context.CallSubOrchestratorAsync<Product>(
            _orc_c_name_,
            product,
            await GetLocalRetryOptionsAsync(_orc_c_name_, product)
        );
        context.SetCustomStatus($"C: {product.LastState}");
        product = await context.CallActivityAsync<Product>(
            _finish_processor_name_,
            product,
            await GetRetryOptionsAsync(_finish_processor_name_, product)
        );
        context.SetCustomStatus($"D: {product.LastState}");

        logger.LogInformation($"**\r\n*** Ended Main Orchestration as {product.LastState} \r\n**");
        return JsonSerializer.Serialize(product.ActivityHistory, _jsonOptions);
    }

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
        var inputData = JsonSerializer.Deserialize<InputData>(requestBody);

        var product = new Product();
        product.LastState = ActivityState.Ready;
        product.Payload.Name = inputData?.Name ?? "";
        product.OperationName = inputData?.Name ?? "";
        product.Payload.UniqueKey = inputData?.UniqueKey ?? "";
        product.Disruptions = inputData?.Disruptions ?? new string[0];
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
