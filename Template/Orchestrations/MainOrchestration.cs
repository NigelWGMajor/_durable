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
using Microsoft.AspNetCore.Routing.Tree;

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

        int index = 3;
        /// /// /// /// /// /// /// /// /// /// /// ///
        ///
        context.SetCustomStatus($"{product.LastState}{index:00}");
        product = await context.CallSubOrchestratorAsync<Product>(
            nameof(RunOrchestrationAlpha),
            product,
            GetOptions(true).WithInstanceId($"{id}Alpha)")
        );
        index += 3;
        context.SetCustomStatus($"{product.LastState}{index:00}");
        if (product.LastState != ActivityState.Redundant)
        {
            product = await context.CallSubOrchestratorAsync<Product>(
                nameof(RunOrchestrationBravo),
                product,
                GetOptions(true, true, true).WithInstanceId($"{id}Bravo)")
            );
        }
        index += 3;
        context.SetCustomStatus($"{product.LastState}{index:00}");
        if (product.LastState != ActivityState.Redundant)
        {
            product = await context.CallSubOrchestratorAsync<Product>(
                nameof(RunOrchestrationCharlie),
                product,
                GetOptions(false, true, true).WithInstanceId($"{id}Charlie)")
            );
        }
        index++;
        context.SetCustomStatus($"{product.LastState}{index:00}");
        if (product.LastState != ActivityState.Redundant)
        {
            product = await context.CallActivityAsync<Product>(
                nameof(FinishAsync),
                product,
                GetOptions().WithInstanceId($"{id}Final)")
            );
        }
        context.SetCustomStatus($"{product.LastState}{index++:00}");
        Console.WriteLine($"**\r\n*** Ended Main Orchestration as {product.LastState} \r\n**");
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
        product.Payload.UniqueKey = inputData.UniqueKey;
        product.Disruptions = (string[]) inputData.Disruptions.Clone();

        StartOrchestrationOptions options = new StartOrchestrationOptions
        {
            InstanceId =
                $"Main-{inputData.Name}-{inputData.UniqueKey}-{DateTime.UtcNow:yy-MM-ddThh:hh:ss:fff}"
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
