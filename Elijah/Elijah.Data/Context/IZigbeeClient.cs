using System.Collections.Generic;
using System.Threading.Tasks;

namespace Elijah.Data.Context;

public interface IZigbeeClient
{
    /// <summary>
    /// Indicates whether the client has subscribed to all necessary devices and is ready.
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// Connects to the MQTT broker.
    /// </summary>
    Task ConnectToMqtt();

    /// <summary>
    /// Subscribes to all devices that have not yet been subscribed to.
    /// </summary>
    Task SubscribeDevices();

    /// <summary>
    /// Subscribes to a newly joined device.
    /// </summary>
    Task SubscribeAfterJoin(string address);

    /// <summary>
    /// Sends changed Zigbee report configurations to the MQTT broker.
    /// </summary>
    Task SendReportConfig();

    /// <summary>
    /// Sends changed option values to devices via MQTT.
    /// </summary>
    Task SendDeviceOptions();

    /// <summary>
    /// Allows new Zigbee devices to join for a specific time, then fetches details.
    /// </summary>
    Task AllowJoinAndListen(int seconds);

    /// <summary>
    /// Fetches device details from the Zigbee2MQTT bridge.
    /// </summary>
    Task GetDeviceDetails(string address, string modelID);

    /// <summary>
    /// Fetches and stores option details for a specific device.
    /// </summary>
    Task GetOptionDetails(string address, string model, List<string> readableProps, List<string> description);

    /// <summary>
    /// Removes a Zigbee device by name.
    /// </summary>
    Task RemoveDevice(string name);

    /// <summary>
    /// Starts continuously processing messages from the MQTT broker.
    /// </summary>
    void StartProcessingMessages();
}

