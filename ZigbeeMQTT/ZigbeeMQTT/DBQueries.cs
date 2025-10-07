using Npgsql;
using System;
using System.Collections.Generic;

namespace Zigbee2MQTTClient
{
    public class DBQueries
    {
        private List<ReportConfig> configList = new List<ReportConfig>();

        // PostgreSQL connection string
        static string connectionString =
            "Host=localhost;Port=5432;Database=DevicesData;Username=postgres;Password=0502";

        public NpgsqlConnection connection = new NpgsqlConnection(connectionString);

        // Open connection
        public void dbConnect()
        {
            if (connection.State != System.Data.ConnectionState.Open)
            {
                connection.Open();
            }
        }

        // Query device name by ModelID
        public string queryDeviceName(string modelId)
        {
            dbConnect();
            string deviceName = null;

            using var cmd = new NpgsqlCommand("SELECT Name FROM Devices WHERE ModelID = @modelId;", connection);
            cmd.Parameters.AddWithValue("@modelId", modelId);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                deviceName = reader.GetString(0);
            }

            reader.Close();
            return deviceName;
        }

        // Query device address by Name
        public string queryDeviceAddress(string name)
        {
            dbConnect();
            string deviceAddress = null;

            using var cmd = new NpgsqlCommand("SELECT Address FROM Devices WHERE Name = @Name;", connection);
            cmd.Parameters.AddWithValue("@Name", name);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                deviceAddress = reader.GetString(0);
            }

            reader.Close();
            return deviceAddress;
        }

        // Query ModelID by Address
        public string queryModelID(string address)
        {
            dbConnect();
            string modelID = null;

            using var cmd = new NpgsqlCommand("SELECT ModelID FROM Devices WHERE Address = @Address;", connection);
            cmd.Parameters.AddWithValue("@Address", address);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                modelID = reader.GetString(0);
            }

            reader.Close();
            return modelID;
        }

        // Query report intervals from either tables
        public void queryReportInterval(string address, string table)
        {
            configList.Clear();
            dbConnect();

            string tableName = table == "A" ? "configuredreportings" : "reporttemplate";
            
            string query = tableName == "configuredreportings"
                ? "SELECT * FROM configuredreportings WHERE address = @address;"
                : "SELECT * FROM reporttemplate;";

            using var cmd = new NpgsqlCommand(query, connection);
            
            if (tableName == "configuredreportings")
            {
                cmd.Parameters.AddWithValue("@address", address);
            }

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                
                string addressValue = tableName == "configuredreportings"
                    ? reader["address"].ToString()
                    : null;

                configList.Add(new ReportConfig(
                    addressValue,
                    reader["modelid"].ToString(),
                    reader["cluster"].ToString(),
                    reader["attribute"].ToString(),
                    reader["maximumreportinterval"].ToString(),
                    reader["minimumreportinterval"].ToString(),
                    reader["reportablechange"].ToString(),
                    reader["endpoint"].ToString()
                ));
            }
        }

        // Query DeviceFilters for a model
        public List<string> queryDataFilter(string modelId)
        {
            dbConnect();
            List<string> filterList = new List<string>();

            using var cmd = new NpgsqlCommand("SELECT FilterValue FROM DeviceFilters WHERE ModelID = @modelId;",
                connection);
            cmd.Parameters.AddWithValue("@modelId", modelId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                filterList.Add(reader.GetString(0));
            }

            reader.Close();
            return filterList.Count > 0 ? filterList : null;
        }

        // Update active status
        public void setActiveStatus(bool active, string address)
        {
            dbConnect();
            using var cmd = new NpgsqlCommand("UPDATE Devices SET active = @active WHERE Address = @Address;",
                connection);
            cmd.Parameters.AddWithValue("@active", active);
            cmd.Parameters.AddWithValue("@Address", address);
            cmd.ExecuteNonQuery();
        }

        // Update subscribed status
        public void setSubscribedStatus(bool subscribed, string address)
        {
            dbConnect();
            using var cmd = new NpgsqlCommand("UPDATE Devices SET subscribed = @subscribed WHERE Address = @Address;",
                connection);
            cmd.Parameters.AddWithValue("@subscribed", subscribed);
            cmd.Parameters.AddWithValue("@Address", address);
            cmd.ExecuteNonQuery();
        }

        // Check if device exists
        public bool devicePresent(string modelID, string address)
        {
            dbConnect();
            using var cmd = new NpgsqlCommand("SELECT 1 FROM Devices WHERE Address = @Address;", connection);
            cmd.Parameters.AddWithValue("@Address", address);

            var result = cmd.ExecuteScalar();
            if (result != null)
            {
                Console.WriteLine("Device already present");
                return true;
            }
            else
            {
                Console.WriteLine("Device not yet present");
                modelPresent(modelID, address);
                return false;
            }
        }

        // Check if model exists
        public bool modelPresent(string modelID, string address)
        {
            dbConnect();
            using var cmd = new NpgsqlCommand("SELECT 1 FROM Devices WHERE ModelID = @ModelID;", connection);
            cmd.Parameters.AddWithValue("@ModelID", modelID);

            var result = cmd.ExecuteScalar();
            if (result != null)
            {
                Console.WriteLine("Model already present");
                return true;
            }

            Console.WriteLine("Model not yet present");
            return false;
        }

        // Copy model template to create new device
        public void copyModelTemplate(string modelID, string address)
        {
            dbConnect();

            string newName = "temp";

            using (var cmd = new NpgsqlCommand(
                       "SELECT Name, NumberActive FROM DeviceTemplate WHERE ModelID = @ModelID;", connection))
            {
                cmd.Parameters.AddWithValue("@ModelID", modelID);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    string name = reader.GetString(0);
                    string numberActive = reader.GetInt32(1).ToString();
                    newName = name + numberActive;
                }
            }

            // Increment NumberActive
            using var cmdUpdate =
                new NpgsqlCommand("UPDATE DeviceTemplate SET NumberActive = NumberActive + 1 WHERE ModelID = @ModelID;",
                    connection);
            cmdUpdate.Parameters.AddWithValue("@ModelID", modelID);
            cmdUpdate.ExecuteNonQuery();

            // Insert new device
            newDeviceEntry(modelID, newName, address);


            // Copy report configs
            queryReportInterval(modelID, "B");
            foreach (var config in configList)
            {
                using var cmdReport = new NpgsqlCommand(@"
                    INSERT INTO configuredreportings
                    (Address, ModelID, cluster, attribute, maximumreportinterval, minimumreportinterval, reportablechange, endpoint)
                    VALUES
                    (@Address, @ModelID, @Cluster, @Attribute, @MaxInterval, @MinInterval, @ReportableChange, @Endpoint);",
                    connection);
            
                cmdReport.Parameters.AddWithValue("@Address", address);
                cmdReport.Parameters.AddWithValue("@ModelID", modelID);
                cmdReport.Parameters.AddWithValue("@Cluster", config.cluster);
                cmdReport.Parameters.AddWithValue("@Attribute", config.attribute);
                cmdReport.Parameters.AddWithValue("@MaxInterval", config.maximum_report_interval);
                cmdReport.Parameters.AddWithValue("@MinInterval", config.minimum_report_interval);
                cmdReport.Parameters.AddWithValue("@ReportableChange", config.reportable_change);
                cmdReport.Parameters.AddWithValue("@Endpoint", config.endpoint);
            
                cmdReport.ExecuteNonQuery();
            }
        }

        public void newDeviceEntry(string modelID, string newName, string address)
        {
            dbConnect();
            using var cmdInsert =
                new NpgsqlCommand("INSERT INTO Devices (ModelID, Name, Address) VALUES (@ModelID, @Name, @Address);",
                    connection);
            cmdInsert.Parameters.AddWithValue("@ModelID", modelID);
            cmdInsert.Parameters.AddWithValue("@Name", newName);
            cmdInsert.Parameters.AddWithValue("@Address", address);
            cmdInsert.ExecuteNonQuery();
        }

        public void newConfigRepEntry(
            string tableName,
            string address,
            string modelID,
            string cluster,
            string attribute,
            string maximumReportInterval,
            string minimumReportInterval,
            string reportableChange,
            string endpoint)
        {
            dbConnect();

            var allowedTables = new[] { "configuredreportings", "reporttemplate" };
            if (Array.IndexOf(allowedTables, tableName.ToLower()) < 0)
            {
                throw new ArgumentException("Invalid table name specified.");
            }

            string sql;
            using var cmdInsert = new NpgsqlCommand();
            cmdInsert.Connection = connection;

            if (tableName.ToLower() == "reporttemplate")
            {
                // omit address column
                sql = @"
            INSERT INTO reporttemplate 
            (modelid, cluster, attribute, maximumreportinterval, minimumreportinterval, reportablechange, endpoint)
            VALUES 
            (@ModelID, @Cluster, @Attribute, @MaxReportInterval, @MinReportInterval, @ReportableChange, @Endpoint);
        ";

                cmdInsert.CommandText = sql;
                cmdInsert.Parameters.AddWithValue("@ModelID", modelID);
                cmdInsert.Parameters.AddWithValue("@Cluster", cluster);
                cmdInsert.Parameters.AddWithValue("@Attribute", attribute);
                cmdInsert.Parameters.AddWithValue("@MaxReportInterval", maximumReportInterval);
                cmdInsert.Parameters.AddWithValue("@MinReportInterval", minimumReportInterval);
                cmdInsert.Parameters.AddWithValue("@ReportableChange", reportableChange);
                cmdInsert.Parameters.AddWithValue("@Endpoint", endpoint);
            }
            else
            {
                // include address column
                sql = @"
            INSERT INTO configuredreportings 
            (address, modelid, cluster, attribute, maximumreportinterval, minimumreportinterval, reportablechange, endpoint)
            VALUES 
            (@Address, @ModelID, @Cluster, @Attribute, @MaxReportInterval, @MinReportInterval, @ReportableChange, @Endpoint);
        ";

                cmdInsert.CommandText = sql;
                cmdInsert.Parameters.AddWithValue("@Address", address);
                cmdInsert.Parameters.AddWithValue("@ModelID", modelID);
                cmdInsert.Parameters.AddWithValue("@Cluster", cluster);
                cmdInsert.Parameters.AddWithValue("@Attribute", attribute);
                cmdInsert.Parameters.AddWithValue("@MaxReportInterval", maximumReportInterval);
                cmdInsert.Parameters.AddWithValue("@MinReportInterval", minimumReportInterval);
                cmdInsert.Parameters.AddWithValue("@ReportableChange", reportableChange);
                cmdInsert.Parameters.AddWithValue("@Endpoint", endpoint);
            }

            cmdInsert.ExecuteNonQuery();
        }


        public void newDVTemplateEntry(string modelID, string name)
        {
            dbConnect(); // Ensure the database connection is open

            using var cmdInsert = new NpgsqlCommand(@"
        INSERT INTO devicetemplate (modelid, name, numberactive)
        VALUES (@ModelID, @Name, 1);
    ", connection);

            cmdInsert.Parameters.AddWithValue("@ModelID", modelID);
            cmdInsert.Parameters.AddWithValue("@Name", name);

            cmdInsert.ExecuteNonQuery();
        }


        public void newFilterEntry(string modelID, string filterValue, bool active)
        {
            dbConnect();

            using var cmdInsert = new NpgsqlCommand(@"
        INSERT INTO devicefilters (modelid, filtervalue, active)
        VALUES (@ModelID, @FilterValue, @Active);
    ", connection);

            cmdInsert.Parameters.AddWithValue("@ModelID", modelID);
            cmdInsert.Parameters.AddWithValue("@FilterValue", filterValue);
            cmdInsert.Parameters.AddWithValue("@Active", active);

            cmdInsert.ExecuteNonQuery();
        }

        public void UnsubOnExit()
        {
            dbConnect();

            string sql = "UPDATE devices SET subscribed = false;";

            using var cmd = new NpgsqlCommand(sql, connection);
            cmd.ExecuteNonQuery();

            Console.WriteLine("All devices unsubscribed.");
        }

        public List<string> GetUnsubscribedAddresses()
        {
            var addresses = new List<string>();
            dbConnect();

            string sql = "SELECT address FROM devices WHERE subscribed = false;";

            using var cmd = new NpgsqlCommand(sql, connection);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                addresses.Add(reader.GetString(0));

                return addresses;
            }

            return null;
        }

        public List<string> GetSubscribedAddresses()
        {
            var addresses = new List<string>();
            dbConnect();

            string sql = "SELECT address FROM devices WHERE subscribed = true;";

            using var cmd = new NpgsqlCommand(sql, connection);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                addresses.Add(reader.GetString(0));

                return addresses;
            }

            return null;
        }

        public void AdjustRepConfig(
            string address,
            string cluster,
            string attribute,
            string maximumReportInterval,
            string minimumReportInterval,
            string reportableChange,
            string endpoint)
        {
            dbConnect();

            string sql = @"
        UPDATE configuredreportings
        SET 
            maximumreportinterval = @MaxReportInterval,
            minimumreportinterval = @MinReportInterval,
            reportablechange = @ReportableChange,
            adjusted = true
        WHERE 
            address = @Address AND
            cluster = @Cluster AND
            attribute = @Attribute AND
            endpoint = @Endpoint;
    ";

            using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@MaxReportInterval", maximumReportInterval);
            cmd.Parameters.AddWithValue("@MinReportInterval", minimumReportInterval);
            cmd.Parameters.AddWithValue("@ReportableChange", reportableChange);
            cmd.Parameters.AddWithValue("@Address", address);
            cmd.Parameters.AddWithValue("@Cluster", cluster);
            cmd.Parameters.AddWithValue("@Attribute", attribute);
            cmd.Parameters.AddWithValue("@Endpoint", endpoint);

            cmd.ExecuteNonQuery();
        }

        public List<ReportConfig> GetChangedReportConfigs(List<string> subscribedAddresses)
{
    var changedConfigs = new List<ReportConfig>();
    dbConnect(); 
    if (subscribedAddresses == null || subscribedAddresses.Count == 0)
        return changedConfigs; 

    
    var parameters = subscribedAddresses
        .Select((addr, index) => $"@addr{index}")
        .ToList();
    string inClause = string.Join(", ", parameters);

    
    string selectSql = $@"
        SELECT address, cluster, modelid, attribute, maximumreportinterval, minimumreportinterval, reportablechange, endpoint
        FROM configuredreportings
        WHERE adjusted = true AND address IN ({inClause});
    ";

    using var selectCmd = new NpgsqlCommand(selectSql, connection);

    for (int i = 0; i < subscribedAddresses.Count; i++)
    {
        selectCmd.Parameters.AddWithValue($"@addr{i}", subscribedAddresses[i]);
    }

    using var reader = selectCmd.ExecuteReader();
    while (reader.Read())
    {
        changedConfigs.Add(new ReportConfig(
            reader["address"].ToString(),
            reader["modelid"].ToString(),
            reader["cluster"].ToString(),
            reader["attribute"].ToString(),
            reader["minimumreportinterval"].ToString(),
            reader["maximumreportinterval"].ToString(),
            reader["reportablechange"].ToString(),
            reader["endpoint"].ToString()
        ));
    }
    reader.Close(); 

    
    string updateSql = $@"
        UPDATE configuredreportings
        SET adjusted = false
        WHERE adjusted = true AND address IN ({inClause});
    ";

    using var updateCmd = new NpgsqlCommand(updateSql, connection);

    for (int i = 0; i < subscribedAddresses.Count; i++)
    {
        updateCmd.Parameters.AddWithValue($"@addr{i}", subscribedAddresses[i]);
    }

    updateCmd.ExecuteNonQuery();

    return changedConfigs;
}


    }
}