using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.EventGrid;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace Training.FunctionBindingsSamples;

public static class OrcherstatorTrigger
{
    [FunctionName("OrcherstatorTrigger")]
    public static async Task RunStartAsync(
        [ServiceBusTrigger("training-queue", Connection = "SBConnection")] string requestItem, 
        [DurableClient] IDurableOrchestrationClient client,
        ILogger log)
    {
        // Function input comes from the request content.
        string instanceId = await client.StartNewAsync("Orchestrator", null, requestItem);

        log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
    }
    
    [FunctionName("Orcherstator")]
    public static async Task RunAsync(
        [OrchestrationTrigger] IDurableOrchestrationContext context,
        ILogger log)
    {
        var name = context.GetInput<string>();
        
        // Task sendEventTask = context.CallActivityAsync("SendEventActivity", name);
        Task<string> callApiTask = context.CallActivityAsync<string>("CallApiActivity", name);
        
        // await Task.WhenAll(sendEventTask, callApiTask); 
        await callApiTask;
        
        log.LogInformation($"Your gender for {name} is {callApiTask.Result}");
        
        await context.CreateTimer(context.CurrentUtcDateTime.Add(TimeSpan.FromMinutes(1)), CancellationToken.None);

        string newName = await context.CallActivityAsync<string>("GetNewNameActivity", null);

        TimeSpan timeout = TimeSpan.FromSeconds(30);
        DateTime deadline = context.CurrentUtcDateTime.Add(timeout);

        using var cts = new CancellationTokenSource();
        
        Task activityTask = context.CallActivityAsync<string>("CallApiActivity", newName);
        Task timeoutTask = context.CreateTimer(deadline, cts.Token);

        Task winner = await Task.WhenAny(activityTask, timeoutTask);
        if (winner == activityTask)
        {
            cts.Cancel();
            log.LogInformation($"Your gender for {newName} is {callApiTask.Result}");
        }
        else log.LogInformation($"Gender for {newName} takes too long");
        
    }
    
    // [FunctionName("SendEventActivity")]
    // public static async Task RunSendEventActivity([ActivityTrigger] string name, ILogger log)
    // {
    //     log.LogInformation($"Sending event for {name}");
    //
    //     // Create an EventGridClient using the Topic Key credentials
    //     var credentials = new Azure.AzureKeyCredential(topicKey);
    //     var client = new EventGridPublisherClient(new Uri(topicEndpoint), credentials);
    //
    //     // Create an event to send to Event Grid
    //     var eventData = new EventGridEvent(
    //         subject: "NewUser",
    //         eventType: "User.Created",
    //         dataVersion: "1.0",
    //         data: name);
    //
    //     // Send the event
    //     await client.SendEventAsync(eventData);
    //
    //     log.LogInformation($"Event sent for {name}");
    // }
    
    private static readonly HttpClient httpClient = new HttpClient();

    [FunctionName("CallApiActivity")]
    public static async Task<string> RunCallApiActivity([ActivityTrigger] string name, ILogger log)
    {
        log.LogInformation($"Calling external API for {name}");

        try
        {
            // Make the POST request
            HttpResponseMessage response = await httpClient.GetAsync($"https://api.agify.io?name={name}");

            if (response.IsSuccessStatusCode)
            {
                log.LogInformation("API call successful.");
                return await response.Content.ReadAsStringAsync();
            }
            else
            {
                log.LogError($"API call failed. Status Code: {response.StatusCode}");
                return "fail";
            }
        }
        catch (HttpRequestException ex)
        {
            log.LogError($"Error calling the API: {ex.Message}");
            return "fail";
        }
    }

    private class User
    {
        public string Username { get; set; }
        public string Address { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public DateTime Birthday { get; set; }
    }
    
    [FunctionName("GetNewNameActivity")]
    public static async Task<string> RunGetNewNameActivity(ILogger log)
    {
        try
        {
            // Make the POST request
            HttpResponseMessage response = await httpClient.GetAsync("https://api.api-ninjas.com/v1/randomuser");

            if (response.IsSuccessStatusCode)
            {
                log.LogInformation("API call successful.");
                User newUser =  await response.Content.ReadAsAsync<User>();

                return newUser.Name;
            }
            else
            {
                log.LogError($"API call failed. Status Code: {response.StatusCode}");
                return "fail";
            }
        }
        catch (HttpRequestException ex)
        {
            log.LogError($"Error calling the API: {ex.Message}");
            return "fail";
        }
    }
}
