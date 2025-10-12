using System.ComponentModel.DataAnnotations;

namespace Elijah.Domain.Entities;

public class DeviceTemplate
{
    [Key]
    public string modelId {get; set;}
    public string name {get; set;}
    public string numberActive {get; set;}
    
}