using System.Threading.Tasks;
using Degreed.SafeTest;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
// Add this using directive

/// <summary>
/// This class extends any orchestration to integrate Flow Management 
/// in a simple manner.
/// Essentially it wraps the function calls to allow suppression of the 
/// calls and error detection. 
/// Any orchestration class that needs this should derive from this class. 
/// </summary>
public static class BaseOrchestration<T>
{
    public static async Task<Product> ProcessSafelyAsync(
        string activityName, 
        Product product, 
        TaskOrchestrationContext context)
    {
        await Task.CompletedTask;

        return product;
    }
}
