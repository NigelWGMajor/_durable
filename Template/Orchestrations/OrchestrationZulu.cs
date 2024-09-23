using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Degreed.SafeTest;
using static TestActivities;
using static BaseActivities;
using System.Net.Mime;
using System.Diagnostics.Tracing;

public static class SafeOrchestration
{
    private static JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true
    };

    // // //
    // This launches the orchestration.
    //
    [Function(nameof(RunOrchestrator))]
    public static async Task<string> RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context
    )
    {
        ILogger logger = context.CreateReplaySafeLogger(nameof(RunOrchestrator));
        Product product = new Product();
        if (!context.IsReplaying)
        {
            logger.LogInformation("*** Initializing Product");
            product = Product.FromContext(context);
            product.ActivityName = nameof(StepAlpha);
        }
        product.Payload.InstanceId = context.InstanceId;
        product.Payload.Id = System.Diagnostics.Process.GetCurrentProcess().Id;
        logger.LogInformation($"*** Preprocessing Product {product.LastState}");
        product = await context.CallActivityAsync<Product>(nameof(PreProcessAsync), product);
        logger.LogInformation($"*** Returned Product {product.LastState}");
        if (product.LastState == ActivityState.Redundant)
            return product.LastState.ToString();
        if (product.LastState == ActivityState.Deferred)
            await context.CreateTimer(TimeSpan.FromSeconds(1 ), CancellationToken.None);
        else if (product.LastState != ActivityState.Active)
        {
            context.ContinueAsNew(product);
             logger.LogInformation($"*** Restarting Anew as Product {product.LastState}");
            return product.LastState.ToString();
        }
        product = await context.CallActivityAsync<Product>(nameof(StepAlpha), product);
        product = await context.CallActivityAsync<Product>(nameof(PostProcessAsync), product);

        // product.ActivityName = nameof(StepBravo);
        // do
        // {
        //     product = await context.CallActivityAsync<Product>(nameof(PreProcessAsync), product);
        //     if (product.LastState == ActivityState.Redundant)
        //         return product.LastState.ToString();
        // } while (product.LastState != ActivityState.Active);

        // product = await context.CallActivityAsync<Product>(nameof(StepBravo), product);
        // product = await context.CallActivityAsync<Product>(nameof(PostProcessAsync), product);

        // product.ActivityName = nameof(StepCharlie);
        // do
        // {
        //     product = await context.CallActivityAsync<Product>(nameof(PreProcessAsync), product);
        //     if (product.LastState == ActivityState.Redundant)
        //         return product.LastState.ToString();
        // } while (product.LastState != ActivityState.Active);
        // product = await context.CallActivityAsync<Product>(nameof(StepCharlie), product);
        // product = await context.CallActivityAsync<Product>(nameof(PostProcessAsync), product);
        // // finalize:
        // product = await context.CallActivityAsync<Product>(nameof(FinishAsync), product);

        string output = JsonSerializer.Serialize(product.ActivityHistory, _jsonOptions);

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

        var product = new Product();
        product.LastState = ActivityState.Ready;
        product.Payload.Name = inputData.Name;
        product.Payload.InstanceId = inputData.Identity;
        
        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(RunOrchestrator),
            product
        );

        logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

        // Returns an HTTP 202 response with an instance management payload.
        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }
}
