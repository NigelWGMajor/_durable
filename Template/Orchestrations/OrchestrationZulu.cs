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
using Microsoft.AspNetCore.CookiePolicy;

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

        product.Payload.Id = System.Diagnostics.Process.GetCurrentProcess().Id;

        // if (product.MayContinue)
        // {
                       product = await context.CallActivityAsync<Product>(nameof(PreProcessAsync), product);
            if (product.LastState == ActivityState.Deferred)
                await context.CreateTimer(TimeSpan.FromSeconds(1), CancellationToken.None);
            else if (product.LastState != ActivityState.Active)
            {
                context.ContinueAsNew(product);
                return product.LastState.ToString();
            }
            if (product.LastState != ActivityState.Redundant && product.ActivityName == nameof(StepAlpha))
            {
                product = await context.CallActivityAsync<Product>(product.ActivityName, product);
                product = await context.CallActivityAsync<Product>(
                    nameof(PostProcessAsync),
                    product
                );
            }
            else
            {
                return "re-entrancy blocked";
            }
        // }

        // if (product.MayContinue)
        // {
            product.ActivityName = nameof(StepBravo);
            product = await context.CallActivityAsync<Product>(nameof(PreProcessAsync), product);
            if (product.LastState == ActivityState.Deferred)
                await context.CreateTimer(TimeSpan.FromSeconds(1), CancellationToken.None);
            else if (product.LastState != ActivityState.Active)
            {
                context.ContinueAsNew(product);
                return product.LastState.ToString();
            }
            if (product.LastState != ActivityState.Redundant && product.ActivityName == nameof(StepBravo))
            {
                product = await context.CallActivityAsync<Product>(product.ActivityName, product);
                product = await context.CallActivityAsync<Product>(
                    nameof(PostProcessAsync),
                    product
                );
            }
            else
            {
                return "re-entrancy blocked";
            }
        // }

        // if (product.MayContinue)
        // {
            product.ActivityName = nameof(StepCharlie);
            product = await context.CallActivityAsync<Product>(nameof(PreProcessAsync), product);
            if (product.LastState == ActivityState.Deferred)
                await context.CreateTimer(TimeSpan.FromSeconds(1), CancellationToken.None);
            else if (product.LastState != ActivityState.Active)
            {
                context.ContinueAsNew(product);
                return JsonSerializer.Serialize(product.ActivityHistory, _jsonOptions);
            }
            if (product.LastState != ActivityState.Redundant && product.ActivityName == nameof(StepCharlie))
            {
                product = await context.CallActivityAsync<Product>(product.ActivityName, product);
                product = await context.CallActivityAsync<Product>(
                    nameof(PostProcessAsync),
                    product
                );
                return JsonSerializer.Serialize(product.ActivityHistory, _jsonOptions);
            }
            else
            {
                return "re-entrancy blocked";
            }
        // }
       //return JsonSerializer.Serialize(product.ActivityHistory, _jsonOptions);
    }

    // private static async Task<Product> ProcessProductAsync(
    //     string activityName,
    //     TaskOrchestrationContext context,
    //     Product product
    // )
    // {
    //     product.ActivityName = activityName;
    //     product = await context.CallActivityAsync<Product>(nameof(PreProcessAsync), product);
    //     if (product.LastState == ActivityState.Deferred)
    //         await context.CreateTimer(TimeSpan.FromSeconds(1), CancellationToken.None);
    //     else if (product.LastState != ActivityState.Active)
    //     {
    //         context.ContinueAsNew(product);
    //         return product;
    //     }
    //     if (product.LastState != ActivityState.Redundant)
    //     {
    //         product = await context.CallActivityAsync<Product>(activityName, product);
    //         product = await context.CallActivityAsync<Product>(nameof(PostProcessAsync), product);
    //         return product;
    //     }
    //     else
    //     {
    //         return product;
    //     }
    // }

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
