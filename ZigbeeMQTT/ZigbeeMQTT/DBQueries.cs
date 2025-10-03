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
        dbConnect();
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

    public string queryDeviceAddress(string name)
    {
        dbConnect();
        string deviceAddress;

        var command = connection.CreateCommand();
        command.CommandText = @"
        SELECT Address
        FROM Devices
        WHERE Name = $Name;
    ";
        command.Parameters.AddWithValue("$Name", name);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            deviceAddress = reader.GetString(0);
            return deviceAddress;
        }
        else
        {
            Console.WriteLine("No matching device found.");
            return null;
        }
    }

    public string queryModelID(string address)
    {
        dbConnect();
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

    public void
        queryReportInterval(string modelId, string table) //call a for configured reportings and B for report template
    {
        configList.Clear();
        dbConnect();

        // Decide table based on model
        string tableName = table == "A" ? "configured_reportings" : "ReportTemplate";

        using var command = connection.CreateCommand();
        command.CommandText = $@"
        SELECT *
        FROM {tableName}
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
            Console.WriteLine("No matching device found.");
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

    public void setActiveStatus(bool active, string address)
    {
        dbConnect();
        var command = connection.CreateCommand();
        command.CommandText = @"
        UPDATE Devices 
        SET active = $active
        Where Address = $Address;
    ";

        command.Parameters.AddWithValue("$active", active);
        command.Parameters.AddWithValue("$Address", address);

        command.ExecuteNonQuery();
    }

    public void setSubscribedStatus(bool subscribed, string address)
    {
        dbConnect();
        var command = connection.CreateCommand();
        command.CommandText = @"
        UPDATE Devices 
        SET subscribed = $subscribed
        Where Address = $Address;
    ";

        command.Parameters.AddWithValue("$subscribed", subscribed);
        command.Parameters.AddWithValue("$Address", address);

        command.ExecuteNonQuery();
    }


    public void devicePresent(string modelID, string address)
    {
        dbConnect();
        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 1
            FROM Devices
            WHERE Address = $Address
                ";
        command.Parameters.AddWithValue("$Address", address);

        object result = command.ExecuteScalar();

        if (result != null && (long)result == 1)
        {
            Console.WriteLine("Device already present");
            setActiveStatus(true, address);
        }
        else
        {
            Console.WriteLine("Device not yet present");
            modelPresent(modelID, address);
        }
    }

    public void modelPresent(string modelID, string address)
    {
        dbConnect();
        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 1
            FROM Devices
            WHERE ModelID = $ModelID
                ";
        command.Parameters.AddWithValue("ModelID", modelID);

        object result = command.ExecuteScalar();

        if (result != null && (long)result == 1)
        {
            Console.WriteLine("Model already present");
            copyModelTemplate(modelID, address);
        }
        else
        {
            Console.WriteLine("Model not yet present");
            newDeviceEntry();
        }
    }

    public void copyModelTemplate(string modelID, string address)
    {
        dbConnect();
        var command = connection.CreateCommand();
        command.CommandText = @"
        SELECT Name,NumberActive
        FROM DeviceTemplate
        WHERE ModelID = $ModelID;
    ";
        command.Parameters.AddWithValue("$ModelID", modelID);

        using var reader = command.ExecuteReader();

        string newName = "temp";
        if (reader.Read())
        {
            string name = reader["Name"].ToString();
            string numberActive = reader["NumberActive"].ToString();
            newName = name + numberActive;

            Console.WriteLine($"New Name: {newName}");
        }
        
        var command4 = connection.CreateCommand();
        command4.CommandText = @"
                UPDATE DeviceTemplate
                SET NumberActive = NumberActive + 1
                WHERE ModelID = $modelId;
            ";
        command4.Parameters.AddWithValue("$modelId", modelID);

        command4.ExecuteNonQuery();

        using (var command2 = connection.CreateCommand())
        {
            command2.CommandText = @"
            INSERT INTO Devices (ModelID, Name, Address)
            VALUES ($ModelID, $Name, $Address);
        ";

            command2.Parameters.AddWithValue("$ModelID", modelID);
            command2.Parameters.AddWithValue("$Name", newName);
            command2.Parameters.AddWithValue("$Address", address);
            command2.ExecuteNonQuery();
        }

        queryReportInterval(modelID, "B");

        foreach (var config in configList)
        {
            using var command3 = connection.CreateCommand();
            command3.CommandText = @"
            INSERT INTO configured_reportings
            (Address, ModelID, cluster, attribute, maximum_report_interval, minimum_report_interval, reportable_change, endpoint)
            VALUES
            ($Address, $ModelID, $Cluster, $Attribute, $MaxInterval, $MinInterval, $ReportableChange, $Endpoint);
        ";

            command3.Parameters.AddWithValue("$Address", address);
            command3.Parameters.AddWithValue("$ModelID", modelID);
            command3.Parameters.AddWithValue("$Cluster", config.cluster);
            command3.Parameters.AddWithValue("$Attribute", config.attribute);
            command3.Parameters.AddWithValue("$MaxInterval", config.maximum_report_interval);
            command3.Parameters.AddWithValue("$MinInterval", config.minimum_report_interval);
            command3.Parameters.AddWithValue("$ReportableChange", config.reportable_change);
            command3.Parameters.AddWithValue("$Endpoint", config.endpoint);

            command3.ExecuteNonQuery();
        }
    }

    public void newDeviceEntry()
    {
        dbConnect();
        var command = connection.CreateCommand();
    }
}