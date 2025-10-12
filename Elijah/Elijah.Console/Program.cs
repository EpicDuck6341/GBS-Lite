using Elijah.Logic.Abstract;
using Elijah.Logic.Injection;
using LogManager;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Elijah.Data.Context;

IConfiguration configuration = new ConfigurationBuilder()
             .SetBasePath(Directory.GetCurrentDirectory())
             .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
             .Build();

//Create servicecollection
IServiceCollection services = new ServiceCollection();

//Add lib dependencies to collection
ServiceMapper.ConfigureServices(services, configuration);

//Generate a provider object from all registered services
ServiceProvider serviceProvider = services.BuildServiceProvider();
IZigbeeClient zigbeeClient = serviceProvider.GetService<IZigbeeClient>();
ZigbeeClient.ConnecttoMqtt();

//Fire application using the provider containing all dependencies
await StartRequestedFunction(args, serviceProvider.GetService<IService>());


static async Task StartRequestedFunction(string[] args, IService facilicomTemplateService)
{
    DateTime start = DateTime.Now;

    //for testing:
    args = ["functionname"];

    if (args != null && args.Any())
        try
        {
            switch (args.FirstOrDefault()?.ToLower())
            {
                case "functionname":
                    Log.Info("Starting FunctionName");
                    await  facilicomTemplateService.FunctionName();
                    Log.Info("FunctionName successfully ended");
                    break;
                default:
                    NoParamFound();
                    return;
            }
        }
        catch (Exception ex)
        {
            Log.SendError($"Fout bij uitvoeren van {args.FirstOrDefault()} functie.", ex);
        }
    else
    {
        Log.Error("FOUT! geen parameter aangeleverd");
        throw new Exception("No Params");
    }
    Log.Info($"Duration: {DateTime.Now - start}");
}

static void NoParamFound()
{
    Log.Warning("FOUT! geen correcte parameter aangeleverd");
    throw new Exception("No correct Param");
}