using MQTTnet;

namespace Zigbee2MQTTClient
{
    class Program
    {
        static ZigbeeClient zbClient = new ZigbeeClient();
        static DBQueries dbQ = new DBQueries();
        //SNZB-03P
        
        //Main to run the code
        static async Task Main(string[] args)
        {
            await zbClient.ConnectToMqtt();
            await Task.Delay(1000);
            await zbClient.SubscribeDevices();
            await Task.Delay(1000);
            await zbClient.sendReportConfig();
            await Task.Delay(1000);
            zbClient.StartProcessingMessages();
            
            await Task.Delay(-1);
        }
            
        }

    
}