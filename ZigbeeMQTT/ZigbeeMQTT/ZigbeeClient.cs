using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using MQTTnet.Protocol;


namespace Zigbee2MQTTClient;

using MQTTnet;
using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public class ZigbeeClient
{
    private static DBQueries dbQ = new DBQueries();
    public List<ZigbeeDevice> deviceList = new List<ZigbeeDevice>();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _pendingDeviceDetails = new();


    public bool IsReady { get; private set; } = false;

    static MqttClientFactory factory = new MqttClientFactory();
    IMqttClient mqttClient = factory.CreateMqttClient();

    MqttClientOptions clientSettings = new MqttClientOptionsBuilder()
        .WithTcpServer("172.17.0.1", 1883)
        .WithClientId("TestClient")
        .Build();

    internal async Task ConnectToMqtt()
    {
        try
        {
            await mqttClient.ConnectAsync(clientSettings, CancellationToken.None);
            Console.WriteLine("Connected to MQTT broker.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connection failed: {ex.Message}");
        }
    }


    internal async Task SubscribeDevices()
    {
        var unsubbed = dbQ.GetUnsubscribedAddresses();
        if (unsubbed == null)
        {
            Console.WriteLine("No unsubscribed devices");
            return;
        }
        foreach (var unsub in unsubbed)
        {
            await mqttClient.SubscribeAsync("zigbee2mqtt/" + unsub);
            dbQ.setSubscribedStatus(true, unsub);
            Console.WriteLine($"Subscribed to {unsub}");
        }

        IsReady = true;
        Console.WriteLine("All EndDevice subscriptions done.");
    }


    internal async Task SendReportConfig()
    {
        // Get all changed report configs
        var changedConfigs = dbQ.GetChangedReportConfigs(dbQ.GetSubscribedAddresses());
        
        foreach (var config in changedConfigs)
        {
            Console.WriteLine("test");
            var configureRequest = new
            {
                id = config.address,
                device = config.address,
                endpoint = config.endpoint,
                cluster = config.cluster,
                attribute = config.attribute,
                minimum_report_interval = config.minimum_report_interval,
                maximum_report_interval = config.maximum_report_interval,
                reportable_change = config.reportable_change
            };

            string payloadToSend = JsonSerializer.Serialize(configureRequest);

            var message = new MqttApplicationMessageBuilder()
                .WithTopic("zigbee2mqtt/bridge/request/device/configure_reporting")
                .WithPayload(payloadToSend)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await mqttClient.PublishAsync(message);
            Console.WriteLine($"Sent configure_reporting:\n" +
                              $"  Address: {config.address}\n" +
                              $"  ModelID: {config.modelID}\n" +
                              $"  Cluster: {config.cluster}\n" +
                              $"  Attribute: {config.attribute}\n" +
                              $"  Endpoint: {config.endpoint}\n" +
                              $"  Minimum Report Interval: {config.minimum_report_interval}\n" +
                              $"  Maximum Report Interval: {config.maximum_report_interval}\n" +
                              $"  Reportable Change: {config.reportable_change}\n");

            
        }
    }



   public async Task AllowJoinAndListen(int seconds)
{
    var transactionId = Guid.NewGuid().ToString();
    var payload = new { time = seconds, transaction = transactionId };
    string payloadToSend = JsonSerializer.Serialize(payload);

    var openMessage = new MqttApplicationMessageBuilder()
        .WithTopic("zigbee2mqtt/bridge/request/permit_join")
        .WithPayload(payloadToSend)
        .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
        .Build();

    // Subscribe to bridge events
    await mqttClient.SubscribeAsync("zigbee2mqtt/bridge/event");

    async Task Handler(MqttApplicationMessageReceivedEventArgs e)
    {
        if (e.ApplicationMessage.Topic != "zigbee2mqtt/bridge/event")
            return;

        string payloadStr = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
        var json = JsonSerializer.Deserialize<JsonElement>(payloadStr);

        if (json.GetProperty("type").GetString() == "device_interview" &&
            json.GetProperty("data").GetProperty("status").GetString() == "successful")
        {
            var data = json.GetProperty("data");
            string address = data.GetProperty("ieee_address").GetString();
            string model = data.GetProperty("definition").GetProperty("model").GetString();
            Console.WriteLine($"Device joined: {address}");

            // // Database handling
            // if (dbQ.devicePresent(model, address))
            // {
            //     dbQ.setActiveStatus(true, address);
            //     return;
            // }
            //
            // if (dbQ.modelPresent(model, address))
            // {
            //     dbQ.copyModelTemplate(model, address);
            //     return;
            // }
            //
            // var exposes = data.GetProperty("definition").GetProperty("exposes");
            // dbQ.newDeviceEntry(model, address, address);
            // dbQ.newDVTemplateEntry(model, address);

             GetDeviceDetails(address, model);
             
             // foreach (JsonElement expose in exposes.EnumerateArray())
             // {
             //     string property = expose.GetProperty("property").GetString();
             //     dbQ.newFilterEntry(model, property, true);
             //     string description = expose.GetProperty("description").GetString();
             //     // Console.WriteLine($"  - {property} ({description})");
             // }
        }

        return;
    }

    mqttClient.ApplicationMessageReceivedAsync += Handler;

    await mqttClient.PublishAsync(openMessage);
    await Task.Delay(seconds * 1000);

    var closePayload = JsonSerializer.Serialize(new { time = 0, transaction = Guid.NewGuid().ToString() });
    var closeMessage = new MqttApplicationMessageBuilder()
        .WithTopic("zigbee2mqtt/bridge/request/permit_join")
        .WithPayload(closePayload)
        .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
        .Build();

    await mqttClient.PublishAsync(closeMessage);
    mqttClient.ApplicationMessageReceivedAsync -= Handler;
}


    internal async Task GetDeviceDetails(string address, string modelID)
{
    
    var tcs = new TaskCompletionSource<bool>();

    // Define the handler as a variable so we can unsubscribe later
    Func<MqttApplicationMessageReceivedEventArgs, Task> handler = null!;

    handler = async e =>
    {
       
        string topic = e.ApplicationMessage.Topic;
        string payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload.ToArray());
        if (topic.Contains("zigbee2mqtt/bridge/devices"))
        {
            using JsonDocument doc = JsonDocument.Parse(payload);
            string targetAddress = address;

            foreach (JsonElement device in doc.RootElement.EnumerateArray())
            {
                string ieee = device.GetProperty("ieee_address").GetString();
                if (ieee != targetAddress) continue;

                Console.WriteLine($"Device: {ieee}");

                if (!device.TryGetProperty("endpoints", out JsonElement endpoints))
                    continue;
                
                var oopie = endpoints.EnumerateArray();
                
                foreach (JsonProperty ep in endpoints.EnumerateObject())
                {
                    Console.WriteLine($" Endpoint: {ep.Name}");

                    if (!ep.Value.TryGetProperty("configured_reportings", out JsonElement reportings))
                        continue;
                    
                    foreach (JsonElement rep in reportings.EnumerateArray())
                    {
                        Console.WriteLine(
                            $"  Reporting: cluster={rep.GetProperty("cluster").GetString()}, attribute={rep.GetProperty("attribute").GetString()}");

                        dbQ.newConfigRepEntry(
                            "configuredreportings",
                            address,
                            modelID,
                            rep.GetProperty("cluster").GetString(),
                            rep.GetProperty("attribute").GetString(),
                            rep.GetProperty("maximum_report_interval").GetInt32().ToString(),
                            rep.GetProperty("minimum_report_interval").GetInt32().ToString(),
                            rep.GetProperty("reportable_change").ToString(),
                            ep.Name
                        );

                        dbQ.newConfigRepEntry(
                            "reporttemplate",
                            address,
                            modelID,
                            rep.GetProperty("cluster").GetString(),
                            rep.GetProperty("attribute").GetString(),
                            rep.GetProperty("maximum_report_interval").GetInt32().ToString(),
                            rep.GetProperty("minimum_report_interval").GetInt32().ToString(),
                            rep.GetProperty("reportable_change").ToString(),
                            ep.Name
                        );
                    }
                }
            }

            // Unsubscribe to prevent multiple calls next time
            mqttClient.ApplicationMessageReceivedAsync -= handler;
            tcs.TrySetResult(true);
        }
    };

    mqttClient.ApplicationMessageReceivedAsync += handler;
    
    await mqttClient.SubscribeAsync("zigbee2mqtt/bridge/devices");
    await tcs.Task;
}

    
   


    internal async Task
        removeDevice(string name) //name of the device to remove,names are stored in the database and are self made
    {
        var transactionId = Guid.NewGuid().ToString();
        var address = dbQ.queryDeviceAddress(name);
        var payload = new
        {
            id = address,
            force = true,
            block = false,
            transaction = transactionId
        };

        dbQ.setSubscribedStatus(false, address);
        dbQ.setActiveStatus(false, address);

        string payloadToSend = JsonSerializer.Serialize(payload);

        var message = new MqttApplicationMessageBuilder()
            .WithTopic("zigbee2mqtt/bridge/request/device/remove")
            .WithPayload(payloadToSend)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await mqttClient.PublishAsync(message);
    }

    internal void StartProcessingMessages()
    {
        mqttClient.ApplicationMessageReceivedAsync += e =>
        {
            string topic = e.ApplicationMessage.Topic;
            string payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload.ToArray());

            if (!topic.Contains("zigbee2mqtt/bridge"))
            {
                string filterTopic = topic.Replace("zigbee2mqtt/", "");
                var node = JsonNode.Parse(Encoding.UTF8.GetString(e.ApplicationMessage.Payload.ToArray()));
                var filtered = new JsonObject();
                string modelID = dbQ.queryModelID(filterTopic);
                List<String> keyPairs = dbQ.queryDataFilter(modelID);

                foreach (var key in keyPairs)
                {
                    if (node[key] != null)
                    {
                        filtered[key] = node[key]!.DeepClone();
                    }
                }

                Console.WriteLine($"[{dbQ.queryDeviceName(modelID)},{modelID}]{filtered.ToJsonString()}");
                // Console.WriteLine($"[{topic}] {payload}");
            }

            return Task.CompletedTask;
        };
    }
    

}

