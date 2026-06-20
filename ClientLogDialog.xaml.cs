using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using TcpServerApp.Models;

namespace TcpServerApp
{
    public partial class ClientLogDialog : Window
    {
        private ClientInfo _clientInfo;

        public ClientLogDialog(ClientInfo clientInfo)
        {
            InitializeComponent();
            _clientInfo = clientInfo;
            
            ClientInfoText.Text = $"{clientInfo.ClientName} - {clientInfo.RemoteEndPoint}";
            
            // إنشاء سجل العميل
            var log = new StringBuilder();
            log.AppendLine($"═══════════════════════════════════════════════");
            log.AppendLine($"سجل العميل: {clientInfo.ClientName}");
            log.AppendLine($"═══════════════════════════════════════════════");
            log.AppendLine();
            log.AppendLine($"🔗 معرف العميل: {clientInfo.Id}");
            log.AppendLine($"📍 العنوان: {clientInfo.RemoteEndPoint}");
            log.AppendLine($"🔌 وقت الاتصال: {clientInfo.ConnectedAt:yyyy-MM-dd HH:mm:ss}");
            log.AppendLine($"🕐 مدة الاتصال: {clientInfo.ConnectedDuration}");
            log.AppendLine($"⏱️ آخر نشاط: {clientInfo.LastActivity:yyyy-MM-dd HH:mm:ss}");
            log.AppendLine($"✅ الحالة: {clientInfo.Status}");
            log.AppendLine();
            log.AppendLine($"───────────────────────────────────────────────");
            log.AppendLine($"📊 إحصائيات البيانات");
            log.AppendLine($"───────────────────────────────────────────────");
            log.AppendLine($"� البيانات المستلمة: {clientInfo.BytesReceivedFormatted} ({clientInfo.BytesReceived} بايت)");
            log.AppendLine($"�📤 البيانات المرسلة: {clientInfo.BytesSentFormatted} ({clientInfo.BytesSent} بايت)");
            log.AppendLine($"📊 إجمالي البيانات: {FormatBytes(clientInfo.BytesReceived + clientInfo.BytesSent)}");
            log.AppendLine();
            log.AppendLine($"═══════════════════════════════════════════════");
            log.AppendLine($"ملاحظة: هذا سجل مبسط للعميل");
            log.AppendLine($"═══════════════════════════════════════════════");
            
            LogTextBlock.Text = log.ToString();
            
            // Update log statistics
            UpdateLogStatistics();
        }

        private void UpdateLogStatistics()
        {
            var lines = LogTextBlock.Text.Split('\n').Length;
            var sizeKB = System.Text.Encoding.UTF8.GetByteCount(LogTextBlock.Text) / 1024.0;
            
            LogCountText.Text = $"{lines} سطر";
            LogSizeText.Text = $"{sizeKB:F2} KB";
        }

        private string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F2} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F2} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }
            else
            {
                DragMove();
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var filename = $"Client_{_clientInfo.Id}_Log_{timestamp}.txt";
                File.WriteAllText(filename, LogTextBlock.Text, Encoding.UTF8);
                
                NotificationManager.Instance.Success($"تم حفظ السجل في: {filename}", "حفظ السجل");
            }
            catch (Exception ex)
            {
                NotificationManager.Instance.Error($"فشل حفظ السجل: {ex.Message}", "خطأ");
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
