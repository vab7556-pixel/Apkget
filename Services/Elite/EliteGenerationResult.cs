using System.Collections.Generic;

namespace TcpServerApp.Services.Elite
{
    public class EliteGenerationResult
    {
        public string DexPath { get; set; }
        public List<string> RequiredPermissions { get; set; } = new List<string>();
        public string SourceCodePath { get; set; }
        public string SourceCode { get; set; }
        public string PackageName { get; set; }
        public string ServiceName { get; set; }
        public string IconPath { get; set; } // Added for AAPT2 Resource Compilation
        public bool EnableNativeBridge { get; set; } // Trigger for NDK Build
        public Dictionary<string, string> ExtraManifestReceivers { get; set; } = new Dictionary<string, string>();
        public List<string> ExtraManifestActivities { get; set; } = new List<string>();
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
