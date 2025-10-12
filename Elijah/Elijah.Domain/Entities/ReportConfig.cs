using System.ComponentModel.DataAnnotations;

namespace Elijah.Domain.Entities;

public class ReportConfig
{
    public string? address { get; set; }
    public string? modelID { get; set; }
    public string? cluster { get; set; }
    public string? attribute { get; set; }
    public string? maximum_report_interval { get; set; }
    public string? minimum_report_interval { get; set; }
    public string? reportable_change { get; set; }
    public string? endpoint { get; set; }
    

    public ReportConfig(string? address, string? model_id, string? cluster, string? attribute, string? maximum_report_interval,string? minimum_report_interval,string? reportable_change,string? endpoint)
    {
        this.address = address;
        this.modelID = model_id;
        this.cluster = cluster;
        this.attribute = attribute;
        this.maximum_report_interval = maximum_report_interval;
        this.minimum_report_interval = minimum_report_interval;
        this.reportable_change = reportable_change;
        this.endpoint = endpoint;
    }
}