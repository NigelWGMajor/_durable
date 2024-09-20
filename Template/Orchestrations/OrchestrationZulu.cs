using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Degreed.SafeTest;
using static TestActivities;
using static BaseOrchestration;
using System.Net.Mime;

public static class SafeOrchestration
{
    private static JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { WriteIndented = true };
    // // //
    // This launches the orchestration.
    // 
    [Function(nameof(RunOrchestrator))]
    public static async Task<string> RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context
    )
    {
//        ILogger logger = context.CreateReplaySafeLogger(nameof(RunOrchestrator));
        Product product = Product.FromContext(context);
        string output = "";

        product = await ProcessSafelyAsync("StepAlpha", product, context);
        product = await ProcessSafelyAsync("StepBravo", product, context);
        product = await ProcessSafelyAsync("StepCharlie", product, context);
        product = await EndSafelyAsync(product);
        
        output = JsonSerializer.Serialize(product.ActivityHistory, _jsonOptions);
        return output;
    }
    // // //
    // The main external entrypoint requires the InputData in json form as application/json content.
    //
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
        
        // // additional data could be read from the url:
        // var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        // string extraData = query["extraData"];
        // // this could allow us to inject test behaviors.
        
        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(RunOrchestrator),
            inputData
        );

        logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

        // Returns an HTTP 202 response with an instance management payload.
        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }
}