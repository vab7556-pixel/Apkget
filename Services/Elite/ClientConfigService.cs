using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using TcpServerApp.Services.Elite.Security;
using TcpServerApp.Services.Elite.Build;
using TcpServerApp.Services.Elite.Networking;
using TcpServerApp.Services.Elite.Geo;

namespace TcpServerApp.Services.Elite
{
    public class ClientConfigService
    {
        private readonly string _configDir;
        // ClientID -> Set of Allowed Permissions
        private readonly ConcurrentDictionary<string, HashSet<string>> _clientPermissions = new();
        private readonly ConcurrentDictionary<string, string> _clientAliases = new();

        public ClientConfigService()
        {
            _configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "res", "GeoIP", "Config");
            LoadConfiguration();
        }

        public void LoadConfiguration()
        {
            _clientPermissions.Clear();
            _clientAliases.Clear();

            string passFile = Path.Combine(_configDir, "Pass.inf");
            if (File.Exists(passFile))
            {
                // Expected Format: ClientID|Alias|Perm1,Perm2,Perm3
                // Or simply: ClientID=AllowAll
                foreach (var line in File.ReadAllLines(passFile))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                    var parts = line.Split('|');
                    if (parts.Length >= 1)
                    {
                        string clientId = parts[0].Trim();
                        string alias = parts.Length > 1 ? parts[1].Trim() : "Unknown";
                        string perms = parts.Length > 2 ? parts[2].Trim() : "BASIC";

                        _clientAliases[clientId] = alias;
                        
                        var permSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        if (perms == "*") 
                        {
                            permSet.Add("ALL");
                        }
                        else
                        {
                            foreach (var p in perms.Split(','))
                                permSet.Add(p.Trim());
                        }
                        _clientPermissions[clientId] = permSet;
                    }
                }
            }
        }

        public bool IsOperationAllowed(string clientId, string operation)
        {
            // Default policy: check if client exists in config
            if (!_clientPermissions.TryGetValue(clientId, out var perms))
            {
                // If not in config, allow only BASIC operations
                return operation == "PING" || operation == "INFO" || operation == "HEARTBEAT";
            }

            if (perms.Contains("ALL")) return true;
            return perms.Contains(operation);
        }

        public string GetClientAlias(string clientId)
        {
            return _clientAliases.TryGetValue(clientId, out var alias) ? alias : clientId;
        }

        public void AddAllowedClient(string clientId, string alias, string permissions = "ALL")
        {
            string passFile = Path.Combine(_configDir, "Pass.inf");
            string entry = $"{clientId}|{alias}|{permissions}{Environment.NewLine}";
            File.AppendAllText(passFile, entry);
            
            // Reload in memory
            _clientAliases[clientId] = alias;
             var permSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
             permSet.Add("ALL"); // Simplify for dynamic add
             _clientPermissions[clientId] = permSet;
        }
    }
}
