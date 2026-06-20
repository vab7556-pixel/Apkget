using System.Windows;
using TcpServerApp.Models;

namespace TcpServerApp
{
    public partial class CommandDialog : Window
    {
        public string Command { get; private set; } = string.Empty;
        private ClientInfo _clientInfo;

        public CommandDialog(ClientInfo clientInfo)
        {
            InitializeComponent();
            _clientInfo = clientInfo;
            ClientNameText.Text = $"إلى: {clientInfo.ClientName} ({clientInfo.RemoteEndPoint})";
        }

        private void Command_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is string command)
            {
                Command = command;
                DialogResult = true;
                Close();
            }
        }

        private void SendCustom_Click(object sender, RoutedEventArgs e)
        {
            var customCommand = CustomCommandTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(customCommand))
            {
                Command = customCommand;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("الرجاء إدخال أمر", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
