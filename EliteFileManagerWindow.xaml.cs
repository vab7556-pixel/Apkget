using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Threading.Tasks;
using TcpServerApp.Services.Elite.Networking;

namespace TcpServerApp
{
    public class RemoteFile
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public bool IsDir { get; set; }
        public string Icon => IsDir ? "📁" : GetFileIcon(Name);
        public string Size { get; set; }
        public string Date { get; set; }
        public string Perms { get; set; }

        private static string GetFileIcon(string name)
        {
            if (string.IsNullOrEmpty(name)) return "📄";
            string ext = System.IO.Path.GetExtension(name).ToLowerInvariant();
            return ext switch
            {
                ".apk" => "📦",
                ".dex" => "🧬",
                ".so" => "⚙️",
                ".xml" => "📋",
                ".json" => "📋",
                ".db" or ".sqlite" => "🗄️",
                ".jpg" or ".png" or ".webp" => "🖼️",
                ".mp4" or ".mp3" => "🎬",
                ".txt" or ".log" => "📝",
                ".key" or ".pem" or ".cert" => "🔐",
                _ => "📄"
            };
        }
    }

    public partial class EliteFileManagerWindow : Window
    {
        private readonly string _clientId;
        private readonly EliteNetworkListener _listener;
        private readonly ObservableCollection<RemoteFile> _files = new();
        private string _currentPath = "/storage/emulated/0";

        public EliteFileManagerWindow(string clientId, string deviceName, EliteNetworkListener listener)
        {
            InitializeComponent();
            _clientId = clientId;
            _listener = listener;
            Title = $"Elite File Manager — {deviceName} [{clientId}]";

            lvFiles.ItemsSource = _files;

            // الاشتراك في الحزم الثنائية الواردة من الجهاز
            _listener.OnPacketReceived += OnBinaryPacketReceived;

            // تحميل المسار الافتراضي
            RequestFileList(_currentPath);
        }

        // ═══════════════════════════════════════════════════════════════
        //  البروتوكول الثنائي — إرسال الأوامر
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// إرسال طلب قائمة ملفات عبر البروتوكول الثنائي (0xB0)
        /// </summary>
        private async void RequestFileList(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) path = "/";
            txtStatus.Text = "⏳ Loading...";

            byte[] payload = Encoding.UTF8.GetBytes(path);
            var packet = new Packet(ElitePacketType.FileListRequest, payload);
            await _listener.SendPacketAsync(_clientId, packet);
        }

        /// <summary>
        /// إرسال طلب تحميل ملف عبر البروتوكول الثنائي (0xB2)
        /// </summary>
        private async void RequestDownload(string filePath)
        {
            txtStatus.Text = $"📥 Downloading: {System.IO.Path.GetFileName(filePath)}...";

            byte[] payload = Encoding.UTF8.GetBytes(filePath);
            var packet = new Packet(ElitePacketType.FileDownloadRequest, payload);
            await _listener.SendPacketAsync(_clientId, packet);
        }

        /// <summary>
        /// إرسال طلب حذف ملف عبر البروتوكول الثنائي (0xB4)
        /// </summary>
        private async void RequestDelete(string filePath)
        {
            byte[] payload = Encoding.UTF8.GetBytes(filePath);
            var packet = new Packet(ElitePacketType.FileDeleteRequest, payload);
            await _listener.SendPacketAsync(_clientId, packet);
        }

        /// <summary>
        /// إرسال ملف للجهاز عبر البروتوكول الثنائي (0xB5)
        /// الهيكل: [PathLen:4][Path][FileNameLen:4][FileName][Content]
        /// </summary>
        private async Task UploadFileAsync(string localFilePath, string remoteDirPath)
        {
            byte[] fileContent = await File.ReadAllBytesAsync(localFilePath);
            string fileName = System.IO.Path.GetFileName(localFilePath);

            byte[] pathBytes = Encoding.UTF8.GetBytes(remoteDirPath);
            byte[] nameBytes = Encoding.UTF8.GetBytes(fileName);

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            bw.Write(pathBytes.Length);
            bw.Write(pathBytes);
            bw.Write(nameBytes.Length);
            bw.Write(nameBytes);
            bw.Write(fileContent);

            var packet = new Packet(ElitePacketType.FileUploadRequest, ms.ToArray());
            await _listener.SendPacketAsync(_clientId, packet);
        }

        // ═══════════════════════════════════════════════════════════════
        //  البروتوكول الثنائي — استقبال الردود
        // ═══════════════════════════════════════════════════════════════

        private void OnBinaryPacketReceived(string senderId, Packet packet)
        {
            if (senderId != _clientId) return;

            switch (packet.Type)
            {
                case ElitePacketType.FileListResponse:
                    HandleFileListResponse(packet.Payload);
                    break;

                case ElitePacketType.FileDownloadResponse:
                    HandleFileDownloadResponse(packet.Payload);
                    break;
            }
        }

        /// <summary>
        /// معالجة رد قائمة الملفات (0xB1)
        /// الهيكل: JSON Array من {name, path, isDir, size, modified, perms}
        /// </summary>
        private void HandleFileListResponse(byte[] payload)
        {
            try
            {
                string json = Encoding.UTF8.GetString(payload);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string path = root.TryGetProperty("path", out var p) ? p.GetString() : _currentPath;

                Dispatcher.Invoke(() =>
                {
                    if (path != null)
                    {
                        _currentPath = path;
                        txtPath.Text = path;
                    }

                    _files.Clear();

                    // زر العودة للأعلى
                    if (_currentPath.Length > 1)
                    {
                        _files.Add(new RemoteFile { Name = "..", IsDir = true, Path = "PARENT" });
                    }

                    if (root.TryGetProperty("files", out var filesArray))
                    {
                        foreach (var f in filesArray.EnumerateArray())
                        {
                            bool isDir = f.GetProperty("isDir").GetBoolean();
                            long size = f.TryGetProperty("size", out var s) ? s.GetInt64() : 0;

                            _files.Add(new RemoteFile
                            {
                                Name = f.GetProperty("name").GetString(),
                                Path = f.GetProperty("path").GetString(),
                                IsDir = isDir,
                                Size = isDir ? "" : FormatBytes(size),
                                Date = f.TryGetProperty("modified", out var d) ? d.GetString() : "",
                                Perms = f.TryGetProperty("perms", out var pm) ? pm.GetString() : ""
                            });
                        }
                    }

                    txtStatus.Text = $"✅ {_files.Count} items — {_currentPath}";
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => txtStatus.Text = $"❌ Parse Error: {ex.Message}");
            }
        }

        /// <summary>
        /// معالجة رد تحميل ملف (0xB3)
        /// الهيكل: [NameLen:4][Name][Content]
        /// </summary>
        private void HandleFileDownloadResponse(byte[] payload)
        {
            try
            {
                using var ms = new MemoryStream(payload);
                using var br = new BinaryReader(ms);

                int nameLen = br.ReadInt32();
                string fileName = Encoding.UTF8.GetString(br.ReadBytes(nameLen));
                byte[] content = br.ReadBytes((int)(ms.Length - ms.Position));

                string downloadDir = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "ResearchOutput", "Downloads", _clientId);
                Directory.CreateDirectory(downloadDir);

                string filePath = System.IO.Path.Combine(downloadDir, fileName);
                File.WriteAllBytes(filePath, content);

                Dispatcher.Invoke(() =>
                {
                    txtStatus.Text = $"✅ Downloaded: {fileName} ({FormatBytes(content.Length)})";
                    MessageBox.Show($"File saved:\n{filePath}", "Download Complete",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => txtStatus.Text = $"❌ Download Error: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  أحداث الواجهة
        // ═══════════════════════════════════════════════════════════════

        private void BtnRefresh_Click(object sender, RoutedEventArgs e) => RequestFileList(txtPath.Text);

        private void BtnUp_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPath.EndsWith("/")) _currentPath = _currentPath.TrimEnd('/');
            int lastSlash = _currentPath.LastIndexOf('/');
            RequestFileList(lastSlash > 0 ? _currentPath.Substring(0, lastSlash) : "/");
        }

        private void TxtPath_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) RequestFileList(txtPath.Text);
        }

        private void LvFiles_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lvFiles.SelectedItem is RemoteFile file)
            {
                if (file.Path == "PARENT") { BtnUp_Click(sender, e); return; }
                if (file.IsDir) RequestFileList(file.Path);
            }
        }

        private void CtxDownload_Click(object sender, RoutedEventArgs e)
        {
            if (lvFiles.SelectedItem is RemoteFile file && !file.IsDir)
                RequestDownload(file.Path);
        }

        private void CtxDelete_Click(object sender, RoutedEventArgs e)
        {
            if (lvFiles.SelectedItem is RemoteFile file)
            {
                if (MessageBox.Show($"Delete {file.Name}?", "Confirm",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    RequestDelete(file.Path);
                    // إعادة تحميل بعد تأخير بسيط
                    Task.Delay(500).ContinueWith(_ => Dispatcher.Invoke(() => RequestFileList(_currentPath)));
                }
            }
        }

        private async void BtnUpload_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog();
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    txtStatus.Text = $"📤 Uploading {System.IO.Path.GetFileName(dlg.FileName)}...";
                    await UploadFileAsync(dlg.FileName, _currentPath);
                    txtStatus.Text = "✅ Upload sent.";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Upload Failed: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  مساعدات
        // ═══════════════════════════════════════════════════════════════

        private string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024L * 1024 * 1024) return $"{bytes / 1024.0 / 1024.0:F1} MB";
            return $"{bytes / 1024.0 / 1024.0 / 1024.0:F1} GB";
        }

        protected override void OnClosed(EventArgs e)
        {
            _listener.OnPacketReceived -= OnBinaryPacketReceived;
            base.OnClosed(e);
        }
    }
}
