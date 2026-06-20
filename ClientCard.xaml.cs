using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using TcpServerApp.Models;

namespace TcpServerApp
{
    public partial class ClientCard : UserControl
    {
        public event EventHandler<ClientInfo>? SendDataRequested;
        public event EventHandler<ClientInfo>? ShowDetailsRequested;
        public event EventHandler<ClientInfo>? DisconnectRequested;
        public event EventHandler<(ClientInfo, string)>? SendCommandRequested;
        public event EventHandler<ClientInfo>? SnapshotRequested;
        public event EventHandler<ClientInfo>? ShowLogRequested;
        public event EventHandler<ClientInfo>? SettingsRequested;

        // Fired when the user clicks a file-extension badge (Tag = extension string, e.g. ".png")
        public event EventHandler<(ClientInfo Client, string Extension)>? FileTypeSelectedRequested;

        // Fired when the user clicks a map-style badge (Tag = map style key from maps.inf)
        public event EventHandler<(ClientInfo Client, string StyleKey)>? MapStyleSelectedRequested;

        public ClientCard()
        {
            InitializeComponent();
        }

        private void ShowMenu_Click(object sender, RoutedEventArgs e)
        {
            ContextMenuPopup.IsOpen = !ContextMenuPopup.IsOpen;
        }

        private void SendData_Click(object sender, RoutedEventArgs e)
        {
            ContextMenuPopup.IsOpen = false;
            if (DataContext is ClientInfo clientInfo)
            {
                SendDataRequested?.Invoke(this, clientInfo);
            }
        }

        private void ShowDetails_Click(object sender, RoutedEventArgs e)
        {
            ContextMenuPopup.IsOpen = false;
            if (DataContext is ClientInfo clientInfo)
            {
                ShowDetailsRequested?.Invoke(this, clientInfo);
            }
        }

        private void SendPing_Click(object sender, RoutedEventArgs e)
        {
            ContextMenuPopup.IsOpen = false;
            if (DataContext is ClientInfo clientInfo)
            {
                SendCommandRequested?.Invoke(this, (clientInfo, "PING"));
            }
        }

        private void ShowLog_Click(object sender, RoutedEventArgs e)
        {
            ContextMenuPopup.IsOpen = false;
            if (DataContext is ClientInfo clientInfo)
            {
                ShowLogRequested?.Invoke(this, clientInfo);
            }
        }

        private void SendCommand_Click(object sender, RoutedEventArgs e)
        {
            ContextMenuPopup.IsOpen = false;
            if (DataContext is ClientInfo clientInfo)
            {
                var commandDialog = new CommandDialog(clientInfo);
                commandDialog.Owner = Window.GetWindow(this);
                
                if (commandDialog.ShowDialog() == true)
                {
                    SendCommandRequested?.Invoke(this, (clientInfo, commandDialog.Command));
                }
            }
        }

        private void Snapshot_Click(object sender, RoutedEventArgs e)
        {
            ContextMenuPopup.IsOpen = false;
            if (DataContext is ClientInfo clientInfo)
            {
                SnapshotRequested?.Invoke(this, clientInfo);
            }
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            ContextMenuPopup.IsOpen = false;
            if (DataContext is ClientInfo clientInfo)
            {
                SettingsRequested?.Invoke(this, clientInfo);
            }
        }

        private void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            ContextMenuPopup.IsOpen = false;
            if (DataContext is ClientInfo clientInfo)
            {
                var result = MessageBox.Show(
                    $"هل تريد قطع اتصال {clientInfo.ClientName}؟\n\n" +
                    $"📍 العنوان: {clientInfo.RemoteEndPoint}\n" +
                    $"🕐 مدة الاتصال: {clientInfo.ConnectedDuration}\n" +
                    $"📊 البيانات المستلمة: {clientInfo.BytesReceivedFormatted}\n" +
                    $"📊 البيانات المرسلة: {clientInfo.BytesSentFormatted}",
                    "⚠️ تأكيد قطع الاتصال",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    DisconnectRequested?.Invoke(this, clientInfo);
                }
            }
        }

        private void FileType_Click(object sender, RoutedEventArgs e)
        {
            ContextMenuPopup.IsOpen = false;
            if (DataContext is ClientInfo clientInfo
                && sender is Button btn
                && btn.Tag is string extension)
            {
                FileTypeSelectedRequested?.Invoke(this, (clientInfo, extension));
            }
        }

        private void MapStyle_Click(object sender, RoutedEventArgs e)
        {
            ContextMenuPopup.IsOpen = false;
            if (DataContext is ClientInfo clientInfo
                && sender is Button btn
                && btn.Tag is string styleKey)
            {
                MapStyleSelectedRequested?.Invoke(this, (clientInfo, styleKey));
            }
        }
    }
}
