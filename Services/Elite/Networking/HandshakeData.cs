using System.Text.Json.Serialization;

namespace TcpServerApp.Services.Elite.Networking
{
    public class HandshakeData
    {
        [JsonPropertyName("id")]
        public string? ClientId { get; set; }

        [JsonPropertyName("name")]
        public string? DeviceName { get; set; }

        [JsonPropertyName("researcher")]
        public string? ResearcherName { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("ver")]
        public string? AndroidVersion { get; set; }

        [JsonPropertyName("batt")]
        public string? BatteryLevel { get; set; }

        [JsonPropertyName("screen")]
        public string? ScreenStatus { get; set; }

        [JsonPropertyName("root")]
        public bool IsRooted { get; set; }

        [JsonPropertyName("apps")]
        public int AppCount { get; set; }

        [JsonPropertyName("imei")]
        public string? IMEI { get; set; }

        [JsonPropertyName("sim")]
        public string? SimOperator { get; set; }

        [JsonPropertyName("mac")]
        public string? MacAddress { get; set; }

        [JsonPropertyName("pop")]
        public string? Population { get; set; }
    }
}
