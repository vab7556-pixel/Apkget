using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace TcpServerApp.Services.Elite.Geo
{
    public class GeoIPInfo
    {
        public string Country { get; set; } = "غير معروف";
        public string CountryCode { get; set; } = "??";
        public string City { get; set; } = "غير معروف";
        public string Region { get; set; } = "غير معروف";
        public string ISP { get; set; } = "غير معروف";
        public string Timezone { get; set; } = "غير معروف";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Flag => GetFlagEmoji(CountryCode);

        private string GetFlagEmoji(string countryCode)
        {
            if (string.IsNullOrEmpty(countryCode) || countryCode == "??") return "🌍";
            
            // تحويل كود الدولة إلى إيموجي العلم
            var code = countryCode.ToUpper();
            if (code.Length != 2) return "🌍";
            
            int firstChar = char.ConvertToUtf32(code, 0) - 0x41 + 0x1F1E6;
            int secondChar = char.ConvertToUtf32(code, 1) - 0x41 + 0x1F1E6;
            
            return char.ConvertFromUtf32(firstChar) + char.ConvertFromUtf32(secondChar);
        }
    }

    public class GeoIPService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly string[] _geoIPApis = new[]
        {
            "http://ip-api.com/json/{0}?fields=status,country,countryCode,region,city,isp,lat,lon,timezone",
            "https://ipapi.co/{0}/json/",
            "https://freegeoip.app/json/{0}"
        };

        public async Task<GeoIPInfo> GetGeoIPInfoAsync(string ipAddress)
        {
            try
            {
                // استخراج IP من العنوان الكامل
                var ip = ExtractIP(ipAddress);
                
                // تجاهل العناوين المحلية
                if (IsLocalIP(ip))
                {
                    return new GeoIPInfo
                    {
                        Country = "محلي",
                        CountryCode = "LO",
                        City = "localhost",
                        Region = "Local Network",
                        ISP = "Local",
                        Timezone = TimeZoneInfo.Local.Id
                    };
                }

                // محاولة الحصول على المعلومات من APIs مختلفة
                foreach (var apiUrl in _geoIPApis)
                {
                    try
                    {
                        var url = string.Format(apiUrl, ip);
                        var response = await _httpClient.GetStringAsync(url);
                        
                        if (apiUrl.Contains("ip-api.com"))
                        {
                            return ParseIpApiResponse(response);
                        }
                        else if (apiUrl.Contains("ipapi.co"))
                        {
                            return ParseIpapiCoResponse(response);
                        }
                        else if (apiUrl.Contains("freegeoip"))
                        {
                            return ParseFreeGeoIPResponse(response);
                        }
                    }
                    catch
                    {
                        continue; // جرب API التالي
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"خطأ في GeoIP: {ex.Message}");
            }

            return new GeoIPInfo();
        }

        private string ExtractIP(string address)
        {
            if (string.IsNullOrEmpty(address)) return "127.0.0.1";
            
            // استخراج IP من صيغة "IP:Port"
            var parts = address.Split(':');
            return parts[0];
        }

        private bool IsLocalIP(string ip)
        {
            if (string.IsNullOrEmpty(ip)) return true;
            
            return ip == "127.0.0.1" || 
                   ip == "localhost" || 
                   ip == "::1" ||
                   ip.StartsWith("192.168.") ||
                   ip.StartsWith("10.") ||
                   ip.StartsWith("172.16.") ||
                   ip.StartsWith("172.17.") ||
                   ip.StartsWith("172.18.") ||
                   ip.StartsWith("172.19.") ||
                   ip.StartsWith("172.20.") ||
                   ip.StartsWith("172.21.") ||
                   ip.StartsWith("172.22.") ||
                   ip.StartsWith("172.23.") ||
                   ip.StartsWith("172.24.") ||
                   ip.StartsWith("172.25.") ||
                   ip.StartsWith("172.26.") ||
                   ip.StartsWith("172.27.") ||
                   ip.StartsWith("172.28.") ||
                   ip.StartsWith("172.29.") ||
                   ip.StartsWith("172.30.") ||
                   ip.StartsWith("172.31.");
        }

        private GeoIPInfo ParseIpApiResponse(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            return new GeoIPInfo
            {
                Country = GetStringProperty(root, "country"),
                CountryCode = GetStringProperty(root, "countryCode"),
                City = GetStringProperty(root, "city"),
                Region = GetStringProperty(root, "region"),
                ISP = GetStringProperty(root, "isp"),
                Timezone = GetStringProperty(root, "timezone"),
                Latitude = GetDoubleProperty(root, "lat"),
                Longitude = GetDoubleProperty(root, "lon")
            };
        }

        private GeoIPInfo ParseIpapiCoResponse(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            return new GeoIPInfo
            {
                Country = GetStringProperty(root, "country_name"),
                CountryCode = GetStringProperty(root, "country_code"),
                City = GetStringProperty(root, "city"),
                Region = GetStringProperty(root, "region"),
                ISP = GetStringProperty(root, "org"),
                Timezone = GetStringProperty(root, "timezone"),
                Latitude = GetDoubleProperty(root, "latitude"),
                Longitude = GetDoubleProperty(root, "longitude")
            };
        }

        private GeoIPInfo ParseFreeGeoIPResponse(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            return new GeoIPInfo
            {
                Country = GetStringProperty(root, "country_name"),
                CountryCode = GetStringProperty(root, "country_code"),
                City = GetStringProperty(root, "city"),
                Region = GetStringProperty(root, "region_name"),
                Timezone = GetStringProperty(root, "time_zone"),
                Latitude = GetDoubleProperty(root, "latitude"),
                Longitude = GetDoubleProperty(root, "longitude")
            };
        }

        private string GetStringProperty(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var property))
            {
                var value = property.GetString();
                return string.IsNullOrEmpty(value) ? "غير معروف" : value;
            }
            return "غير معروف";
        }

        private double GetDoubleProperty(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var property))
            {
                if (property.ValueKind == JsonValueKind.Number)
                {
                    return property.GetDouble();
                }
            }
            return 0;
        }
    }
}
