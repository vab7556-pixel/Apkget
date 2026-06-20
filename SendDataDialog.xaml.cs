using System;
using System.Text;
using System.Windows;
using TcpServerApp.Models;

namespace TcpServerApp
{
    public partial class SendDataDialog : Window
    {
        public byte[] DataToSend { get; private set; } = Array.Empty<byte>();
        public string DataType { get; private set; } = "Text";
        private ClientInfo? _clientInfo;

        public SendDataDialog(ClientInfo? clientInfo)
        {
            InitializeComponent();
            _clientInfo = clientInfo;
            
            if (clientInfo != null)
            {
                ClientNameText.Text = $"إلى: {clientInfo.ClientName} ({clientInfo.RemoteEndPoint})";
            }
            else
            {
                ClientNameText.Text = "إلى: جميع العملاء المتصلين (بث جماعي)";
            }
        }

        private void DataType_Changed(object sender, RoutedEventArgs e)
        {
            if (TextRadio?.IsChecked == true)
            {
                HintText.Text = "مثال: Hello World";
                DataType = "Text";
            }
            else if (HexRadio?.IsChecked == true)
            {
                HintText.Text = "مثال: 48 65 6C 6C 6F أو 48656C6C6F";
                DataType = "Hex";
            }
            else if (Base64Radio?.IsChecked == true)
            {
                HintText.Text = "مثال: SGVsbG8gV29ybGQ=";
                DataType = "Base64";
            }
        }

        private void QuickCommand_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button button) return;
            
            var tag = button.Tag?.ToString();
            switch (tag)
            {
                case "PING":
                    TextRadio.IsChecked = true;
                    DataTextBox.Text = "PING";
                    break;
                case "STATUS":
                    TextRadio.IsChecked = true;
                    DataTextBox.Text = "STATUS";
                    break;
                case "RESET":
                    TextRadio.IsChecked = true;
                    DataTextBox.Text = "RESET";
                    break;
                case "00FF":
                    HexRadio.IsChecked = true;
                    DataTextBox.Text = "00 FF";
                    break;
                case "TEST":
                    TextRadio.IsChecked = true;
                    DataTextBox.Text = "Test Data from Server";
                    break;
            }
        }

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var input = DataTextBox.Text.Trim();
                
                if (string.IsNullOrEmpty(input))
                {
                    MessageBox.Show("الرجاء إدخال البيانات المراد إرسالها", "خطأ", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (TextRadio.IsChecked == true)
                {
                    DataToSend = Encoding.UTF8.GetBytes(input);
                }
                else if (HexRadio.IsChecked == true)
                {
                    var hex = input.Replace(" ", "").Replace("-", "");
                    if (hex.Length % 2 != 0)
                    {
                        MessageBox.Show("البيانات السداسية العشرية يجب أن تكون أزواج", "خطأ", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    
                    DataToSend = new byte[hex.Length / 2];
                    for (int i = 0; i < hex.Length; i += 2)
                    {
                        DataToSend[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
                    }
                }
                else if (Base64Radio.IsChecked == true)
                {
                    DataToSend = Convert.FromBase64String(input);
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في معالجة البيانات: {ex.Message}", "خطأ", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
