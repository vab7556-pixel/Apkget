using System;
using System.ComponentModel;
using System.Net.Sockets;
using TcpServerApp.Services;
using TcpServerApp.Services.Elite.Geo;

namespace TcpServerApp.Models
{
    public class ClientInfo : INotifyPropertyChanged
    {
        private string _status = "متصل";
        private long _bytesReceived;
        private long _bytesSent;
        private DateTime _lastActivity;
        private GeoIPInfo? _geoInfo;

        public int Id { get; set; }
        public string ClientName => $"عميل #{Id}";
        public TcpClient TcpClient { get; set; }
        public string RemoteEndPoint { get; set; }
        public DateTime ConnectedAt { get; set; }
        
        public GeoIPInfo? GeoInfo
        {
            get => _geoInfo;
            set
            {
                _geoInfo = value;
                OnPropertyChanged(nameof(GeoInfo));
                OnPropertyChanged(nameof(LocationInfo));
                OnPropertyChanged(nameof(CountryFlag));
                OnPropertyChanged(nameof(CountryCode));
            }
        }

        public string LocationInfo => GeoInfo != null ? 
            $"{GeoInfo.City}, {GeoInfo.Country}" : "جاري التحميل...";
        
        public string CountryFlag => GeoInfo?.Flag ?? "🌍";
        
        // كود الدولة لعرض العلم من مجلد GeoIP/Flags
        public string CountryCode => GeoInfo?.CountryCode ?? "-1";
        
        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }

        public long BytesReceived
        {
            get => _bytesReceived;
            set
            {
                _bytesReceived = value;
                OnPropertyChanged(nameof(BytesReceived));
                OnPropertyChanged(nameof(BytesReceivedFormatted));
            }
        }

        public long BytesSent
        {
            get => _bytesSent;
            set
            {
                _bytesSent = value;
                OnPropertyChanged(nameof(BytesSent));
                OnPropertyChanged(nameof(BytesSentFormatted));
            }
        }

        public DateTime LastActivity
        {
            get => _lastActivity;
            set
            {
                _lastActivity = value;
                OnPropertyChanged(nameof(LastActivity));
                OnPropertyChanged(nameof(LastActivityFormatted));
            }
        }

        public string BytesReceivedFormatted => FormatBytes(BytesReceived);
        public string BytesSentFormatted => FormatBytes(BytesSent);
        public string LastActivityFormatted => LastActivity.ToString("HH:mm:ss");
        public string ConnectedDuration => GetDuration();

        public ClientInfo(TcpClient client, int id)
        {
            TcpClient = client;
            Id = id;
            RemoteEndPoint = client.Client.RemoteEndPoint?.ToString() ?? "غير معروف";
            ConnectedAt = DateTime.Now;
            LastActivity = DateTime.Now;
        }

        private string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F2} KB";
            return $"{bytes / (1024.0 * 1024.0):F2} MB";
        }

        private string GetDuration()
        {
            var duration = DateTime.Now - ConnectedAt;
            if (duration.TotalMinutes < 1)
                return $"{(int)duration.TotalSeconds} ثانية";
            if (duration.TotalHours < 1)
                return $"{(int)duration.TotalMinutes} دقيقة";
            return $"{(int)duration.TotalHours} ساعة {duration.Minutes} دقيقة";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
