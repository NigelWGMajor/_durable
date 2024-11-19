using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;


// var host = new HostBuilder()
//     .ConfigureFunctionsWebApplication()
//     //.ConfigureServices(services => {
//         //services.AddApplicationInsightsTelemetryWorkerService();
//         //services.ConfigureFunctionsApplicationInsights();
//     //})
//     .Build();

// host.Run();
var host = new HostBuilder()
    .ConfigureWebJobs(webJobsBuilder =>
    {
        webJobsBuilder
            .AddAzureStorageCoreServices()
            //.AddAzureStorage()
            .AddTimers()
            .AddHttp();
    })
    //.ConfigureLogging((context, loggingBuilder) =>
    //{
        //loggingBuilder.AddConsole();
    //})
    .ConfigureServices(services =>
    {
        // Add any additional services here
    })
    .Build();

host.Run();