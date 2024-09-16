using Xunit;
using System.Net.Http;
using System.Text.Json;

using System.Text;

namespace TemplateIntegrationTests;

public class OrchestrationTests
{
    // Prerequisites:

    // START:
    //   Azurite
    //   Docker sqlserver with required initialization
    // LAUNCH:
    //   Template project in debug mode

    [Fact]
    public async Task Orchestration_should_launch_with_parameters()
    {
        HttpClient _client = new HttpClient()
        {   // this is added to allow for code single-stepping
            Timeout = TimeSpan.FromMinutes(10) 
        };
        string url = "http://localhost:7071/api/OrchestrationZulu_HttpStart";
        // Create your JSON model
        var model = new
        {
            Name = "John",
            Identity = "30",
            TestStates = new[]
            {
                ActivityState.Completed,
                ActivityState.Completed,
                ActivityState.Completed
            }
        };
        // Serialize the model to JSON
        string json = JsonSerializer.Serialize(model);
        // Create the HTTP content with JSON data
        HttpContent content = new StringContent(json, Encoding.UTF8, "application/json");
        // Send the POST request and get the response
        HttpResponseMessage response = await _client.PostAsync(url, content);
        // Check if the request was successful
        if (response.IsSuccessStatusCode)
        {
            // Handle the successful response
            string responseJson = await response.Content.ReadAsStringAsync();
            // Process the response JSON
        }
        else
        {
            // Handle the error response
            string errorMessage = await response.Content.ReadAsStringAsync();
            // Process the error message
        }
    }
}
