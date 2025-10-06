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
        var tcs = new TaskCompletionSource<bool>();

        mqttClient.ApplicationMessageReceivedAsync += async e =>
        {
            string topic = e.ApplicationMessage.Topic;
            string payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload.ToArray());

            if (topic.Contains("zigbee2mqtt/bridge/devices"))
            {
                var devices = JsonSerializer.Deserialize<List<ZigbeeDevice>>(payload);

                int i = 0;
                string previousType = " ";
                foreach (var d in devices)
                {
                    if (previousType.Equals(d.type)) i++;
                    else i = 0;
                    previousType = d.type;

                    string name = d.type + i;
                    deviceList.Add(new ZigbeeDevice(
                        name,
                        d.type,
                        d.ieee_address,
                        d.model_id,
                        d.description));

                    if (d.type.Equals("EndDevice"))
                    {
                        await mqttClient.SubscribeAsync("zigbee2mqtt/" + d.ieee_address);
                        dbQ.setSubscribedStatus(true,d.ieee_address);
                    }
                }

                tcs.SetResult(true);
            }
        };


        await mqttClient.SubscribeAsync("zigbee2mqtt/bridge/devices");
        Console.WriteLine("Subscribed to bridge/devices");

        IsReady = true;


        await tcs.Task;
        Console.WriteLine("All EndDevice subscriptions done.");
    }

    internal async Task sendReportConfig()
    {
        foreach (var d in deviceList)
        {
            if (d.type.Equals("EndDevice"))
            {
                dbQ.queryReportInterval(d.model_id,"A");
                foreach (var config in dbQ.configList)
                {
                    var configureRequest = new
                    {
                        id = d.ieee_address,
                        device = d.ieee_address,
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
                    Console.WriteLine($"Sent configure_reporting for [{config.cluster},{config.attribute}]");
                }
            }
        }
    }

    // internal async Task allowJoin(int time)//Simply set to 0 if you want to block devices from joining/stop joining period early.
    // {
    //     var transactionId = Guid.NewGuid().ToString();
    //
    //     var payload = new
    //     {
    //         time = time,
    //         transaction = transactionId
    //     };
    //
    //     string payloadToSend = JsonSerializer.Serialize(payload);
    //
    //     var message = new MqttApplicationMessageBuilder()
    //         .WithTopic("zigbee2mqtt/bridge/request/permit_join")
    //         .WithPayload(payloadToSend)
    //         .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
    //         .Build();
    //
    //     await mqttClient.PublishAsync(message);
    // }
    
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
    
    await mqttClient.SubscribeAsync("zigbee2mqtt/bridge/event");

    
    Task Handler(MqttApplicationMessageReceivedEventArgs e)
    {
        if (e.ApplicationMessage.Topic != "zigbee2mqtt/bridge/event")
            return Task.CompletedTask;

        string payloadStr = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
        var json = JsonSerializer.Deserialize<JsonElement>(payloadStr);
        if (json.GetProperty("type").GetString() == "device_interview"&& json.GetProperty("data").GetProperty("status").GetString()=="successful")
        {Console.WriteLine(json);
            var data = json.GetProperty("data");
            // Console.WriteLine(data);
            string address = data.GetProperty("ieee_address").GetString();
            string model = data.GetProperty("definition").GetProperty("model").GetString();
            var exposes = data.GetProperty("definition").GetProperty("exposes");
            dbQ.devicePresent(model,address);
            
            Console.WriteLine($"Device joined:{address}");
            foreach (JsonElement expose in exposes.EnumerateArray())
            {
                // Example: print label and type
                string property = expose.GetProperty("property").GetString();
                string description = expose.GetProperty("description").GetString();

                Console.WriteLine($"  - {property} ({description})");
            }
            
        }

        return Task.CompletedTask;
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


    
    internal async Task removeDevice(string name)//name of the device to remove,names are stored in the database and are self made
    {
        var transactionId = Guid.NewGuid().ToString();
        var address = dbQ.queryDeviceAddress(name);
        var payload = new
        {
            id =address,
            force = true,
            block = false,
            transaction = transactionId
        };
        
        dbQ.setSubscribedStatus(false,address);
        dbQ.setActiveStatus(false,address);

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
        // Only attach this handler once
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