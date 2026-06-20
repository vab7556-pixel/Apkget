using System;
using System.Windows;
using TcpServerApp.Models;

namespace TcpServerApp
{
    public partial class ClientDetailsDialog : Window
    {
        private ClientInfo _clientInfo;
        public event EventHandler<ClientInfo>? SendDataRequested;
        public event EventHandler<ClientInfo>? DisconnectRequested;

        public ClientDetailsDialog(ClientInfo clientInfo)
        {
            InitializeComponent();
            _clientInfo = clientInfo;
            LoadClientDetails();
        }

        private void LoadClientDetails()
        {
            ClientNameText.Text = _clientInfo.ClientName;
            StatusText.Text = $"الحالة: {_clientInfo.Status}";
            RemoteEndPointText.Text = _clientInfo.RemoteEndPoint;
            
            // معلومات GeoIP
            if (_clientInfo.GeoInfo != null)
            {
                LocationText.Text = $"{_clientInfo.GeoInfo.Flag} {_clientInfo.GeoInfo.City}, {_clientInfo.GeoInfo.Country}";
                ISPText.Text = _clientInfo.GeoInfo.ISP;
            }
            else
            {
                LocationText.Text = "جاري التحميل...";
                ISPText.Text = "غير متوفر";
            }
            
            ConnectedAtText.Text = _clientInfo.ConnectedAt.ToString("yyyy-MM-dd HH:mm:ss");
            DurationText.Text = _clientInfo.ConnectedDuration;
            BytesReceivedText.Text = _clientInfo.BytesReceivedFormatted;
            BytesSentText.Text = _clientInfo.BytesSentFormatted;
            LastActivityText.Text = _clientInfo.LastActivity.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private void SendData_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SendDataDialog(_clientInfo);
            dialog.Owner = this.Owner;
            dialog.ShowDialog();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadClientDetails();
            MessageBox.Show("تم تحديث المعلومات", "تحديث", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                $"هل تريد قطع اتصال {_clientInfo.ClientName}؟",
                "تأكيد قطع الاتصال",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                DisconnectRequested?.Invoke(this, _clientInfo);
                Close();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
