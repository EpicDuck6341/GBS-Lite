using MQTTnet;

namespace Zigbee2MQTTClient
{
    class Program
    {
        static ZigbeeClient zbClient = new ZigbeeClient();
        static DBQueries dbQ = new DBQueries();
        //SNZB-03P en 0xd44867fffe2a920a
        
        //Main to run the code
        static async Task Main(string[] args)
        {
            
            dbQ.devicePresent("SNZB-03P","testAddress");
            // await zbClient.ConnectToMqtt();
            // await Task.Delay(1000);
            // await zbClient.AllowJoinAndListen(200);
            // await zbClient.allowJoin(10);
            // await Task.Delay(10000);
            // await zbClient.removeDevice("PIR");
            // await zbClient.allowJoin(0);
            // // await Task.Delay(12000);
            // await zbClient.SubscribeDevices();
            // await Task.Delay(1000);
            // await zbClient.sendReportConfig();
            // await Task.Delay(1000);
            // zbClient.StartProcessingMessages();

            // await Task.Delay(-1);
        }
            
        }

    
}