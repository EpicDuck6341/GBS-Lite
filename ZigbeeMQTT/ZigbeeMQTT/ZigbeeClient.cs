using System.Collections.Concurrent;
using System.IO.Ports;
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
    static SerialPort _serialPort;


    public bool IsReady { get; private set; } = false;

    static MqttClientFactory factory = new MqttClientFactory();
    IMqttClient mqttClient = factory.CreateMqttClient();

    MqttClientOptions clientSettings = new MqttClientOptionsBuilder()
        .WithTcpServer("172.17.0.1", 1883)
        .WithClientId("TestClient")
        .Build();

    internal async Task ESPConnect()
    {
        _serialPort = new SerialPort("/dev/ttyUSB1", 115200);
        _serialPort.ReadTimeout = 2000;
        _serialPort.WriteTimeout = 2000;

        _serialPort.Open();
        Console.WriteLine("Serial port opened. Waiting for ESP to reset...");
        await Task.Delay(4000);

        string response = "";
        int attempts = 0;

        while (!response.Contains("ESP_READY") && attempts < 20)
        {
            try
            {
                Console.WriteLine(attempts);
                response += _serialPort.ReadExisting();
                await Task.Delay(200);
                attempts++;
            }
            catch (TimeoutException)
            {
            }
        }

        if (response.Contains("ESP_READY"))
        {
            Console.WriteLine("ESP_READY received!");
            _serialPort.WriteLine("test"); // \r\n automatically added
        }
        else
        {
            Console.WriteLine("Failed to receive ESP_READY");
        }
    }

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

    internal async Task SubscribeAfterJoin(string address)
    {
        await mqttClient.SubscribeAsync("zigbee2mqtt/" + address);
        dbQ.setSubscribedStatus(true, address);
        Console.WriteLine($"Subscribed to {address}");


        IsReady = true;
        Console.WriteLine("All newly joined EndDevice subscriptions done.");
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


    internal async Task SendDeviceOptions()
    {
        // Fetch all changed options (where adjusted = true)
        var changedOptions = dbQ.GetChangedOptionValues(dbQ.GetSubscribedAddresses());

        foreach (var opt in changedOptions)
        {
            object valueToSend;

            // Try to parse as int
            if (int.TryParse(opt.CurrentValue, out int intVal))
            {
                valueToSend = intVal;
            }
            // If not int, try to parse as double
            else if (double.TryParse(opt.CurrentValue, out double doubleVal))
            {
                valueToSend = doubleVal;
            }
            else
            {
                // fallback to string
                valueToSend = opt.CurrentValue;
            }

            var payload = new JsonObject
            {
                [opt.Property] = JsonValue.Create(valueToSend)
            };

            string payloadToSend = payload.ToJsonString();

            var message = new MqttApplicationMessageBuilder()
                .WithTopic($"zigbee2mqtt/{opt.Address}/set")
                .WithPayload(payloadToSend)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await mqttClient.PublishAsync(message);

            Console.WriteLine($"Sent option update:\n" +
                              $"  Address: {opt.Address}\n" +
                              $"  Property: {opt.Property}\n" +
                              $"  Value: {valueToSend}\n" +
                              $"  Topic: zigbee2mqtt/{opt.Address}/set");
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

        Queue<(string address, string model)> joinedDevice = new();
        Queue<(string address, string model, List<String> propTarget, List<String> descs)> targetData = new();
        List<String> props = new();
        List<String> descList = new();

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

                // Database handling
                if (dbQ.devicePresent(model, address))
                {
                    dbQ.setActiveStatus(true, address);
                    return;
                }

                if (dbQ.modelPresent(model, address))
                {
                    dbQ.copyModelTemplate(model, address);
                    return;
                }

                var exposes = data.GetProperty("definition").GetProperty("exposes");
                var options = data.GetProperty("definition").GetProperty("options");
                dbQ.newDeviceEntry(model, address, address);
                dbQ.newDVTemplateEntry(model, address);

                joinedDevice.Enqueue((address, model));

                foreach (JsonElement expose in exposes.EnumerateArray())
                {
                    string property = expose.GetProperty("property").GetString();
                    dbQ.newFilterEntry(model, property, true);
                    string description = expose.GetProperty("description").GetString();
                    Console.WriteLine($"  - {property} ({description})");


                    var accessEx = expose.GetProperty("access").GetInt16();
                    if (accessEx == 7 || accessEx == 2)
                    {
                        if (accessEx == 7)
                        {
                            descList.Add(expose.GetProperty("description").GetString());
                            props.Add(property);
                        }
                    }
                }

                foreach (JsonElement option in options.EnumerateArray())
                {
                    var accesOp = option.GetProperty("access").GetInt16();
                    string property = option.GetProperty("property").GetString();
                    if (accesOp == 2 || accesOp == 7)
                    {
                        Console.WriteLine(" test5");
                        if (accesOp == 7)
                        {
                            descList.Add(option.GetProperty("description").GetString());
                            props.Add(property);
                        }
                    }
                }

                targetData.Enqueue((address, model, props, descList));
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

        while (joinedDevice.Count > 0)
        {
            var (addr, model) = joinedDevice.Dequeue();
            await GetDeviceDetails(addr, model);
            await SubscribeAfterJoin(addr);
        }

        while (targetData.Count > 0)
        {
            var (addr, mdl, propTarget, description) = targetData.Dequeue();
            await GetOptionDetails(addr, mdl, propTarget, description);
        }

        mqttClient.ApplicationMessageReceivedAsync -= Handler;
    }


    internal async Task GetDeviceDetails(string address, string modelID)
    {
        var tcs = new TaskCompletionSource<bool>();
        Func<MqttApplicationMessageReceivedEventArgs, Task> handler = null!;

        handler = async e =>
        {
            string topic = e.ApplicationMessage.Topic;
            string payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload.ToArray());

            if (topic == "zigbee2mqtt/bridge/devices")
            {
                using JsonDocument doc = JsonDocument.Parse(payload);
                string targetAddress = address;

                foreach (JsonElement device in doc.RootElement.EnumerateArray())
                {
                    string ieee = device.GetProperty("ieee_address").GetString();
                    if (ieee != targetAddress) continue;

                    Console.WriteLine($"Device found: {ieee} (target: {targetAddress})");

                    if (!device.TryGetProperty("endpoints", out JsonElement endpoints))
                    {
                        Console.WriteLine("No endpoints property found.");
                        continue;
                    }

                    foreach (JsonProperty ep in endpoints.EnumerateObject())
                    {
                        Console.WriteLine($"Endpoint {ep.Name}");

                        if (!ep.Value.TryGetProperty("configured_reportings", out JsonElement reportings))
                        {
                            Console.WriteLine(" No configured_reportings property.");
                            continue;
                        }


                        int retries = 0;
                        while (reportings.GetArrayLength() == 0 && retries < 5)
                        {
                            Console.WriteLine($"No reportings yet (retry {retries + 1}/5)...");
                            await Task.Delay(5000);


                            var requestPayload =
                                JsonSerializer.Serialize(new { transaction = Guid.NewGuid().ToString() });
                            var requestMessage = new MqttApplicationMessageBuilder()
                                .WithTopic("zigbee2mqtt/bridge/request/devices")
                                .WithPayload(requestPayload)
                                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                                .Build();

                            await mqttClient.PublishAsync(requestMessage);
                            retries++;
                            return;
                        }

                        Console.WriteLine($"Found {reportings.GetArrayLength()} reportings.");

                        foreach (JsonElement rep in reportings.EnumerateArray())
                        {
                            Console.WriteLine(
                                $"      • Reporting: cluster={rep.GetProperty("cluster").GetString()}, attribute={rep.GetProperty("attribute").GetString()}");

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

                mqttClient.ApplicationMessageReceivedAsync -= handler;
                tcs.TrySetResult(true);
            }
        };

        mqttClient.ApplicationMessageReceivedAsync += handler;
        await mqttClient.SubscribeAsync("zigbee2mqtt/bridge/devices");

        // Initial request for all devices
        var initialPayload = JsonSerializer.Serialize(new { transaction = Guid.NewGuid().ToString() });
        var initialRequest = new MqttApplicationMessageBuilder()
            .WithTopic("zigbee2mqtt/bridge/request/devices")
            .WithPayload(initialPayload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await mqttClient.PublishAsync(initialRequest);

        await tcs.Task;
    }


    internal async Task GetOptionDetails(string address, string model, List<string> readableProps,
        List<String> description)
    {
        var tcs = new TaskCompletionSource<bool>();

        Func<MqttApplicationMessageReceivedEventArgs, Task> handler = null!;
        handler = async e =>
        {
            string topic = e.ApplicationMessage.Topic;
            if (topic != $"zigbee2mqtt/{address}")
                return;
            var pl = JsonNode.Parse(Encoding.UTF8.GetString(e.ApplicationMessage.Payload.ToArray()));
            string payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
            JsonNode? node = JsonNode.Parse(payload);
            if (node == null)
                return;

            var filtered = new JsonObject();

            int i = 0;
            foreach (var prop in readableProps)
            {
                if (node[prop] != null)
                {
                    filtered[prop] = node[prop]!.DeepClone();
                    // 🔹 store in DB
                    dbQ.SetOptions(address, model, description[i], node[prop]!.ToJsonString(), prop);
                    Console.WriteLine($"Option: {prop} = {node[prop]}");
                    i++;
                }
            }

            mqttClient.ApplicationMessageReceivedAsync -= handler;
            tcs.TrySetResult(true);
        };

        mqttClient.ApplicationMessageReceivedAsync += handler;


        var getMessage = new MqttApplicationMessageBuilder()
            .WithTopic($"zigbee2mqtt/{address}")
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await mqttClient.PublishAsync(getMessage);

        // Wait up to 5 seconds for response
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(10000));
        mqttClient.ApplicationMessageReceivedAsync -= handler;

        if (completed != tcs.Task)
            Console.WriteLine($"Timeout while getting options for {address}");
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

    internal async Task sendESPConfig(int bright)
    {
        var address = "0xe4b323fffe9e2d38";

        var payload = new
        {
            brightness = bright
        };

        // Convert the payload to a JSON string
        string payloadToSend = JsonSerializer.Serialize(payload);

        // Construct the MQTT message
        var message = new MqttApplicationMessageBuilder()
            .WithTopic($"zigbee2mqtt/{address}/set") // Send directly to the device topic
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

                // if (node?["temperature"] != null)
                // {
                //     var temperature = node["temperature"]!.GetValue<Double>();
                //     Console.WriteLine($"Temperature: {temperature}");
                //     _serialPort.WriteLine( temperature.ToString());
                // }

                // if (filterTopic.Equals("0xe4b323fffe9e2d38"))
                // {
                //     for (int i = 0; i < 4; i++)
                //     {
                //         sendESPConfig(i + 1);
                //         Task.Delay(200);
                //     }
                // }
                Console.WriteLine($"[{topic}] {payload}");
                foreach (var key in keyPairs)
                {
                    if (node[key] != null)
                    {
                        filtered[key] = node[key]!.DeepClone();
                    }
                }

                Console.WriteLine($"[{dbQ.queryDeviceName(modelID)},{modelID}]{filtered.ToJsonString()}");

                
            }

            return Task.CompletedTask;
        };
    }
}