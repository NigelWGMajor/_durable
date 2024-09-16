using System.Net.Sockets;
using System.Threading.Tasks;
using Degreed.SafeTest;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Configuration;

// Add this using directive

/// <summary>
/// This class extends any orchestration to integrate Flow Management
/// in a simple manner.
/// Essentially it wraps the function calls to allow suppression of the
/// calls and error detection.
/// Any orchestration class that needs this should derive from this class.
/// </summary>
public static class BaseOrchestration
{
    private static DataStore _store;

    static BaseOrchestration()
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
            throw new FlowManagerException("MetadataStore connection not provided");
    }
    public static async Task<Product> ProcessSafelyAsync(
        string activityName, // this is the name of the required activity as seen by the Orchestration framework
        Product product, // this contains the inbound data
        TaskOrchestrationContext context
    )
    {
        // this is the safety wrapper for the activity.
        // This controls whether the activity is even fired,
        // and works with the metadata.
        var keyId = product.Payload.Identity;
        // read the current activity
        var current = await _store.ReadActivityStateAsync(keyId);
        // Choose actions dependent on oncoming state
        // If there is no record yet, the state will be 'unknown'
        switch (current.State)
        {
            case ActivityState.unknown:
            case ActivityState.New: 
                current.TimeStarted = DateTime.UtcNow;
            break;

            case ActivityState.Stuck: 
            break;
            case ActivityState.Active: 
            break;
            case ActivityState.Completed: 
            break;
        }
        // attempt the activity

        // choose actions based on exit state
        switch (current.State)
        {
            case ActivityState.Redundant: 
            break;
            case ActivityState.Deferred: 
            break;
            case ActivityState.Stalled: 
            break;
            case ActivityState.Failed: 
            break;
        }

        await _store.WriteActivityStateAsync(current);
        ;
        return product;
    }
}
