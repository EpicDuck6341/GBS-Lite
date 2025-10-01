namespace Zigbee2MQTTClient;

public class ZigbeeDevice
{
    public string? givenName { get; set; }
    public string? ieee_address { get; set; }
    public string? type { get; set; }
    public string? model_id { get; set; }
    public string? description { get; set; }
    
    
    

    public ZigbeeDevice(string? givenName, string? type, string? ieee_address, string? model_id,
        string? description)
    {
        this.givenName = givenName ?? "";
        this.ieee_address = ieee_address ?? "";
        this.type = type ?? "";
        this.model_id = model_id ?? "";
        this.description = description ?? "";
    }
}
