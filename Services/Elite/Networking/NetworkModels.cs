using System;

namespace TcpServerApp.Services.Elite.Networking
{
    public class AppInfo
    {
        public string AppName { get; set; }
        public string PackageName { get; set; }
        public string Version { get; set; }
        public bool IsSystem { get; set; }
    }

    public class FederatedTaskResult
    {
        public string Population { get; set; }
        public string Task { get; set; }
        public string Status { get; set; } // SUCCESS/FAILURE
        public float Metric { get; set; }  // Accuracy/Loss
    }

    public class OdpUpdatePayload
    {
        public string ModelName { get; set; }
        public string FeatureVector { get; set; }
        public long TrainingTimeMs { get; set; }
    }
}
