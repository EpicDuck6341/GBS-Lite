namespace Zigbee2MQTTClient;

using SQLitePCL;
using System;
using Microsoft.Data.Sqlite;

public class DBQueries
{
    public List<ReportConfig> configList = new List<ReportConfig>();


    static string dbPath = "/home/vboxuser/RiderProjects/GBS-Lite/ZigbeeMQTT/ZigbeeMQTT/DevicesData.db";

    static string connectionString = $"Data Source={dbPath}";
    public SqliteConnection connection = new SqliteConnection(connectionString);

    public void dbConnect()
    {
        SQLitePCL.Batteries.Init();

        if (!File.Exists(dbPath))
        {
            Console.WriteLine($"Database file not found at: {dbPath}");
            return; // exit early
        }
        else
        {
            // Console.WriteLine($"Database file found at: {dbPath}");
        }

        connection.Open();
    }


    public string queryDeviceName(string modelId)
    {
        string deviceName;

        var command = connection.CreateCommand();
        command.CommandText = @"
        SELECT Name
        FROM Devices
        WHERE ModelID = $modelId;
    ";
        command.Parameters.AddWithValue("$modelId", modelId);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            deviceName = reader.GetString(0);
            return deviceName;
        }
        else
        {
            Console.WriteLine("No matching device found.");
            return null;
        }
    }
    
    public string queryModelID(string address)
    {
        string modelID;

        var command = connection.CreateCommand();
        command.CommandText = @"
        SELECT ModelID
        FROM Devices
        WHERE Address = $Address;
    ";
        command.Parameters.AddWithValue("$Address", address);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            modelID = reader.GetString(0);
            return modelID;
        }
        else
        {
            Console.WriteLine("No matching device found");
            return null;
        }
    }

    public void queryReportInterval(string modelId)
    {
        configList.Clear();
        dbConnect();
        var command = connection.CreateCommand();
        command.CommandText = @"
        SELECT *
        FROM configured_reportings
        WHERE ModelID = $modelId;
    ";
        command.Parameters.AddWithValue("$modelId", modelId);
        using var reader = command.ExecuteReader();
        bool anyRows = false;
        while (reader.Read())
        {
            anyRows = true;

            configList.Add(new ReportConfig(
                modelId,
                reader["cluster"].ToString(),
                reader["attribute"].ToString(),
                reader["maximum_report_interval"].ToString(),
                reader["minimum_report_interval"].ToString(),
                reader["reportable_change"].ToString(),
                reader["endpoint"].ToString()));
        }


        if (!anyRows)
        {
            Console.WriteLine("No matching device found.");
        }
    }
    
    public List<String> queryDataFilter(string modelId)
    {
        dbConnect();
        List<String> filterList = new List<String>();
        var command = connection.CreateCommand();
        command.CommandText = @"
        SELECT *
        FROM DeviceFilters
        WHERE ModelID = $modelId;
    ";
        command.Parameters.AddWithValue("$modelId", modelId);
        using var reader = command.ExecuteReader();
        bool anyRows = false;
        while (reader.Read())
        {
            anyRows = true;
            filterList.Add(reader["FilterValue"].ToString());
        }


        if (!anyRows)
        {
            Console.WriteLine("No matching device found.");
            return null;
        }

        return filterList;
    }
}