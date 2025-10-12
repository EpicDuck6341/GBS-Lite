using System.ComponentModel.DataAnnotations;

namespace Elijah.Domain.Entities;

public class Device
{
    [Key]
    public string address {get; set;}
    public string modelId {get; set;}
    public string name {get; set;}
    public bool subscribed {get; set;}
    public bool active {get; set;}
}