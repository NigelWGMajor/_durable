using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Degreed.SafeTest;
using static TestActivities;
using static Activities.ActivityHelper;
using static Orchestrations.SubOrchestrationAlpha;
using static Orchestrations.SubOrchestrationBravo;
using static Orchestrations.SubOrchestrationCharlie;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace Orchestrations;

public static class TestOrchestration
{
    private static JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true
    };

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
    const string _infra_settings_name_ = "Infra";
    const string _infra_test_settings_name_ = "InfraTest";

    public static async Task<RetryOptions> GetLocalRetryOptionsAsync(
        string activityName,
        Product product
    )
    {
        string settingsName = product.IsDisrupted
            ? _infra_test_settings_name_
            : _infra_settings_name_;
        return await GetRetryOptionsAsync(settingsName, product);
    }

    [FunctionName(nameof(RunTestOrchestrator))]
    public static async Task<string> RunTestOrchestrator(
        [OrchestrationTrigger] IDurableOrchestrationContext context,
        ILogger logger
    )
    {
        ILogger replaySafeLogger = context.CreateReplaySafeLogger(logger);

        Product product = new Product("");
        if (!context.IsReplaying)
        {
            logger.LogInformation("*** Initializing Product");
            product = context.GetInput<Product>() ?? new Product("");
            product.ActivityName = _first_activity_name_;
        }
        string id = context.InstanceId;

        context.SetCustomStatus($"{product.LastState}");
        product = await context.CallSubOrchestratorWithRetryAsync<Product>(
            _orc_a_name_,
            await GetLocalRetryOptionsAsync(_orc_a_name_, product),
            product
        );

        context.SetCustomStatus($"A: {product.LastState}");
        product = await context.CallSubOrchestratorWithRetryAsync<Product>(
            _orc_b_name_,
            await GetLocalRetryOptionsAsync(_orc_b_name_, product),
            product
        );

        context.SetCustomStatus($"B: {product.LastState}");
        product = await context.CallSubOrchestratorWithRetryAsync<Product>(
            _orc_c_name_,
            await GetLocalRetryOptionsAsync(_orc_c_name_, product),
            product
        );
        context.SetCustomStatus($"C: {product.LastState}");
        product = await context.CallActivityWithRetryAsync<Product>(
            _finish_processor_name_,
            await GetRetryOptionsAsync(_finish_processor_name_, product),
            product
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

    [FunctionName("TestOrchestration_HttpStart")]
    public static async Task<IActionResult> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req,
        [DurableClient] IDurableOrchestrationClient client,
        ILogger logger
    )
    {
        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var inputData = System.Text.Json.JsonSerializer.Deserialize<InputPayload>(requestBody);
        var product = new Product(requestBody);
        product.LastState = ActivityState.Ready;
        product.Name = inputData?.Name ?? "";
        product.Name = inputData?.Name ?? "";
        product.UniqueKey = inputData?.UniqueKey ?? "";
        product.Disruptions = inputData?.Disruptions ?? new string[0];
        product.HostServer = OrchestrationHelper.IdentifyServer();

        string instanceId =
            $"Main-{inputData?.Name}-{inputData?.UniqueKey}-{DateTime.UtcNow:yy-MM-ddThh:hh:ss:fff}";

        //        product.InstanceId = options.InstanceId;
        instanceId = await client.StartNewAsync(nameof(RunTestOrchestrator), instanceId, product);
        logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

        var response = new
        {
            instanceId,
            statusQueryGetUri = client.CreateHttpManagementPayload(instanceId).StatusQueryGetUri
        };
        var checkStatusResponse = client.CreateCheckStatusResponse(req, instanceId) as ObjectResult;
        if (checkStatusResponse == null)
        {
            return new BadRequestResult();
        }
        else
        {
            return new AcceptedResult(
                checkStatusResponse.Value.ToString(), response
            );
        }
    }
    #endregion // HTTP Endpoint for Test Orchestration
}
