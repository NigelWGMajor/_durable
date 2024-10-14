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

namespace Orchestrations;

public static class SafeOrchestration
{
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
    /**/{
        /**/ILogger logger = context.CreateReplaySafeLogger(nameof(RunMainOrchestrator));
        /**/
        /**/Product product = new Product();
        /**/if (!context.IsReplaying)
        /**/{
            /**/logger.LogInformation("*** Initializing Product");
            /**/product = Product.FromContext(context);
            product.ActivityName = nameof(ActivityAlpha);
            /**/
            /**/
        }
        if (MatchesDisruption(product.NextDisruption, Models.Disruption.Crash))
            throw new Exception("Emulated Crash in main orchestrator");
        /**/string id = context.InstanceId;
        /**/int index = 3;
        /**/context.SetCustomStatus($"{product.LastState}{index:00}");
        /**/product = await context.CallSubOrchestratorAsync<Product>(
            nameof(OrchestrationAlpha),
            /**/product,
            GetOptions(true).WithInstanceId($"{id}Alpha)")
        /**/);
        if (MatchesDisruption(product.NextDisruption, Models.Disruption.Crash))
            throw new Exception("Emulated Crash in main orchestrator");
        /**/index += 3;
        /**/context.SetCustomStatus($"{product.LastState}{index:00}");
        /**/if (product.LastState != ActivityState.Redundant)
        /**/{
            /**/product = await context.CallSubOrchestratorAsync<Product>(
                nameof(OrchestrationBravo),
                /**/product,
                GetOptions(true, true, true).WithInstanceId($"{id}Bravo)")
            /**/);
            /**/
            /**/
        }
        if (MatchesDisruption(product.NextDisruption, Models.Disruption.Crash))
            throw new Exception("Emulated Crash in main orchestrator");
        /**/index += 3;
        /**/context.SetCustomStatus($"{product.LastState}{index:00}");
        /**/if (product.LastState != ActivityState.Redundant)
        /**/{
            /**/product = await context.CallSubOrchestratorAsync<Product>(
                nameof(OrchestrationCharlie),
                /**/product,
                GetOptions(false, true, true, true).WithInstanceId($"{id}Charlie)")
            /**/);
            /**/
        }
        /**/index++;
        /**/context.SetCustomStatus($"{product.LastState}{index:00}");
        /**/if (product.LastState != ActivityState.Redundant)
        /**/{
            /**/product = await context.CallActivityAsync<Product>(
                /**/nameof(FinishAsync),
                /**/product,
                /**/GetOptions().WithInstanceId($"{id}Final)")
            /**/);
            /**/
            /**/
        }
        /**/context.SetCustomStatus($"{product.LastState}{index++:00}");
        /**/Console.WriteLine($"**\r\n*** Ended Main Orchestration as {product.LastState} \r\n**");
        /**/return JsonSerializer.Serialize(product.ActivityHistory, _jsonOptions);
    }

    [Function("OrchestrationZulu_HttpStart")]
    public static async Task<HttpResponseData> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext
    )
    {
        /**/ILogger logger = executionContext.GetLogger("OrchestrationZulu_HttpStart");
        /**/string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        /**/var inputData = JsonSerializer.Deserialize<InputData>(requestBody);

        /**/var product = new Product();
        /**/product.LastState = ActivityState.Ready;
        /**/product.Payload.Name = inputData?.Name ?? "";
        /**/product.OperationName = inputData?.Name ?? "";
        /**/product.Payload.UniqueKey = inputData?.UniqueKey ?? "";
        /**/product.Disruptions = inputData?.Disruptions ?? new string[0];
        /**/StartOrchestrationOptions options = new StartOrchestrationOptions
        /**/{
            InstanceId =
                $"Main-{inputData.Name}-{inputData.UniqueKey}-{DateTime.UtcNow:yy-MM-ddThh:hh:ss:fff}"
            /**/
        };
        /**/string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            /**/nameof(RunMainOrchestrator),
            /**/product,
            /**/options,
            /**/CancellationToken.None
        /**/);

        /**/logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

        // Returns an HTTP 202 response with an instance management payload.
        /**/return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }
}
