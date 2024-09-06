using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Training.FunctionBindingsSamples;

public static class EntryPoint
{
    [FunctionName("EntryPoint")]
    public static async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
        [ServiceBus("training-queue", Connection = "SBConnection")] ICollection<dynamic> sbOutput,
        ILogger log)
    {
        string[] names = req.Query["names"].ToArray();

        return new OkResult();
        
    }

}