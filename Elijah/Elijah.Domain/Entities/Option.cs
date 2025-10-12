using System.ComponentModel.DataAnnotations;

namespace Elijah.Domain.Entities;

public class Option
{
    [Key]
    public int optionId { get; set; }
    public string address  { get; set; }
    public string model {get; set;}
    public string description {get; set;}
    public string currentValue {get; set;}
    public bool changed { get; set;}
    public string property {get; set;}
}