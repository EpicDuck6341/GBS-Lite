using System.Buffers;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using MQTTnet;
using MQTTnet.Protocol;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Elijah.Data.Context;
using Elijah.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace Elijah.Data.Context;


public class ZigbeeClient : IZigbeeClient
{
    // private static DBQueries dbQ = new DBQueries();
    public List<ZigbeeDevice> deviceList = new List<ZigbeeDevice>();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _pendingDeviceDetails = new();
    //.WithTcpServer("172.17.0.1", 1883) niet vergeten, nodig voor mqttconnect
    // .WithClientId("TestClient")
    // .Build();

    public bool IsReady { get; private set; } = false;

    static MqttClientFactory factory = new MqttClientFactory();
    IMqttClient mqttClient = factory.CreateMqttClient();

    private MqttClientOptions clientSettings;

    public async Task ConnectToMqtt()
    {
        
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        var config = configuration.GetSection("MQTTString");
        
        
        clientSettings = new MqttClientOptionsBuilder()
            .WithTcpServer(config["Hostname"], int.Parse(config["Port"]))
            .WithClientId(config["ClientId"])
            .Build();

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

    public async Task SubscribeDevices()
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

    public async Task SubscribeAfterJoin(string address)
    {
        await mqttClient.SubscribeAsync("zigbee2mqtt/" + address);
        dbQ.setSubscribedStatus(true, address);
        Console.WriteLine($"Subscribed to {address}");

        IsReady = true;
        Console.WriteLine("All newly joined EndDevice subscriptions done.");
    }

    public async Task SendReportConfig()
    {
        var changedConfigs = dbQ.GetChangedReportConfigs(dbQ.GetSubscribedAddresses());

        foreach (var config in changedConfigs)
        {
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
            Console.WriteLine($"Sent configure_reporting for {config.address}");
        }
    }

    public async Task SendDeviceOptions()
    {
        var changedOptions = dbQ.GetChangedOptionValues(dbQ.GetSubscribedAddresses());

        foreach (var opt in changedOptions)
        {
            object valueToSend;

            if (int.TryParse(opt.CurrentValue, out int intVal))
                valueToSend = intVal;
            else if (double.TryParse(opt.CurrentValue, out double doubleVal))
                valueToSend = doubleVal;
            else
                valueToSend = opt.CurrentValue;

            var payload = new JsonObject
            {
                [opt.Property] = JsonValue.Create(valueToSend)
            };

            var message = new MqttApplicationMessageBuilder()
                .WithTopic($"zigbee2mqtt/{opt.Address}/set")
                .WithPayload(payload.ToJsonString())
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await mqttClient.PublishAsync(message);
            Console.WriteLine($"Sent option update for {opt.Address}");
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

        await mqttClient.SubscribeAsync("zigbee2mqtt/bridge/event");

        // Logic for handling joined devices remains unchanged...
        // [Keep the implementation as in your original code]
    }

    public async Task GetDeviceDetails(string address, string modelID)
    {
        // Implementation remains as in your original code
    }

    public async Task GetOptionDetails(string address, string model, List<string> readableProps, List<string> description)
    {
        // Implementation remains as in your original code
    }

    public async Task RemoveDevice(string name)
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

    public void StartProcessingMessages()
    {
        mqttClient.ApplicationMessageReceivedAsync += e =>
        {
            string topic = e.ApplicationMessage.Topic;
            string payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload.ToArray());

            if (!topic.Contains("zigbee2mqtt/bridge"))
            {
                string filterTopic = topic.Replace("zigbee2mqtt/", "");
                var node = JsonNode.Parse(payload);
                var filtered = new JsonObject();
                string modelID = dbQ.queryModelID(filterTopic);
                List<String> keyPairs = dbQ.queryDataFilter(modelID);

                foreach (var key in keyPairs)
                {
                    if (node[key] != null)
                        filtered[key] = node[key]!.DeepClone();
                }

                Console.WriteLine($"[{dbQ.queryDeviceName(modelID)},{modelID}]{filtered.ToJsonString()}");
            }

            return Task.CompletedTask;
        };
    }
}
