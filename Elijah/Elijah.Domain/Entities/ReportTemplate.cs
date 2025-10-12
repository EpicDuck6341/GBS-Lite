using System.ComponentModel.DataAnnotations;

namespace Elijah.Domain.Entities;

public class ReportTemplate
{
    [Key]
    public int reportId { get; set; }
    public string modelId { get; set; }
    public string cluster { get; set; }
    public string attribute { get; set; }
    public string maxiumReportInterval { get; set; }
    public string minimumReportInterval { get; set; }
    public string reportableChange { get; set; }
    public string endpoint { get; set; }
}