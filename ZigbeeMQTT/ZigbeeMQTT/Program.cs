using System.Text.Json;
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
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            Console.CancelKeyPress += OnCancelKeyPress;
            await zbClient.ConnectToMqtt();
            // await Task.Delay(3000);
            // await zbClient.sendESPConfig(5);
            // await Task.Delay(1000);
            // await zbClient.sendESPConfig(6);
            // await Task.Delay(1000);
            // await zbClient.sendESPConfig(7);
            // await Task.Delay(1000);
            // await zbClient.sendESPConfig(8);
            // await Task.Delay(1000);
            // await zbClient.sendESPConfig(10);
            // await Task.Delay(1000);
            // await Task.Delay(1000);
            // await zbClient.SendDeviceOptions();
            // zbClient.removeDevice("0xa4c138024a75ffff");
            // await Task.Delay(1000);
            // await zbClient.AllowJoinAndListen(15);
            await zbClient.SubscribeDevices();
            await Task.Delay(1000);
            // // await zbClient.SendReportConfig();
            // await Task.Delay(1000);
            // await zbClient.ESPConnect();
            // await Task.Delay(1000);
            // Console.WriteLine("Starting message procession");
            zbClient.StartProcessingMessages();
            
            await Task.Delay(-1);
        }
        
        static void OnProcessExit(object sender, EventArgs e)
        {
            Console.WriteLine("Program is shutting down... running final function.");
            MyCleanupFunction();
        }

        // Triggered on Ctrl+C
        static void OnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Console.WriteLine("Ctrl+C pressed. Running cleanup...");
            MyCleanupFunction();
            e.Cancel = true; // optional: prevent immediate exit until cleanup finishes
        }

        static void MyCleanupFunction()
        {
            dbQ.UnsubOnExit(); // sets all subscribed columns to false
            Console.WriteLine("Cleanup function executed!");
        }
            
        }

    
}