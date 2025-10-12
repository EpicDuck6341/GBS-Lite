using System.ComponentModel.DataAnnotations;

namespace Elijah.Domain.Entities;

public class DeviceFilter
{
    [Key]
    public int filterId { get; set; }
    public string modelId { get; set; }
    public string filterValue { get; set; }
    public bool active { get; set; }
}