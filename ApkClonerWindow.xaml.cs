using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using WpfMessageBox = System.Windows.MessageBox;
using WpfOpenFileDialog = Microsoft.Win32.OpenFileDialog;


namespace TcpServerApp
{
    public partial class ApkClonerWindow : Window
    {
        private readonly string _apktoolPath;
        private readonly string _aaptPath;
        private readonly string _aapt2Path;
        private readonly string _zipalignPath;
        private readonly string _dexdumpPath;
        private readonly string _apksignerJar;
        private readonly string _debugKeystore;
        private readonly string _javaExePath;   // JRE المضمّن من build-tools/36.1.0/bin
        private readonly string _keytoolPath;   // لتوليد debug.keystore تلقائياً
        private readonly string _workDir;
        private readonly string _outputDir;

        private string? _sourceApkPath;
        private string? _newIconPath;
        private string? _originalPackage;
        private string? _originalAppName;
        private string? _originalVersion;
        private CancellationTokenSource? _cts;
        private bool _isCloning;

        public ApkClonerWindow()
        {
            InitializeComponent();

            // ── مسارات الأدوات الحقيقية ─────────────────────────────────────────
            string basePath  = AppDomain.CurrentDomain.BaseDirectory;
            string toolsRoot = Path.Combine(basePath, "res", "ResearchPayloadTools");

            // apktool من المجلد الجذري لـ ResearchPayloadTools
            _apktoolPath  = Path.Combine(toolsRoot, "apktool.bat");

            // aapt و zipalign من build-tools/36.1.0 (الأحدث)
            string buildTools36 = Path.Combine(toolsRoot, "build-tools", "36.1.0");
            _aaptPath     = Path.Combine(buildTools36, "aapt.exe");
            _zipalignPath = Path.Combine(buildTools36, "zipalign.exe");
            _aapt2Path    = Path.Combine(buildTools36, "aapt2.exe");
            _dexdumpPath  = Path.Combine(buildTools36, "dexdump.exe");
            _apksignerJar = Path.Combine(buildTools36, "apksigner.jar");
            _debugKeystore= Path.Combine(toolsRoot, "debug.keystore");

            // ── اكتشاف java.exe المناسبة ─────────────────────────────────────────
            // أولوية: build-tools/36.1.0/bin/java.exe ← JAVA_HOME ← PATH
            _javaExePath  = ResolveJavaExePath(buildTools36);
            _keytoolPath  = Path.Combine(buildTools36, "bin", "keytool.exe");

            _workDir   = Path.Combine(Path.GetTempPath(), "ApkCloner");
            _outputDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ClonedApks");

            if (!Directory.Exists(_outputDir))
                Directory.CreateDirectory(_outputDir);

            chkMultiClone.Checked   += (s, e) => pnlMultiClone.Visibility = Visibility.Visible;
            chkMultiClone.Unchecked += (s, e) => pnlMultiClone.Visibility = Visibility.Collapsed;

            LogMessage("🧬 APK DNA Cloner — Android 36 جاهز للعمل");
            LogMessage($"📁 مجلد المخرجات: {_outputDir}");
            LogMessage($"⚙️  build-tools: {(Directory.Exists(buildTools36) ? "36.1.0 ✅" : "غير موجود ❌")}");
            LogMessage($"☕ Java: {_javaExePath}");
        }

        /// <summary>
        /// يكتشف مسار java.exe الأنسب بالأولوية:
        ///   1. build-tools/36.1.0/bin/java.exe (JRE 17 من Google — مضمون)
        ///   2. JAVA_HOME/bin/java.exe
        ///   3. java (من PATH النظام كـ fallback)
        /// </summary>
        private static string ResolveJavaExePath(string buildTools36)
        {
            // 1️⃣ أولوية قصوى: JRE المضمّن من Google
            string embedded = Path.Combine(buildTools36, "bin", "java.exe");
            if (File.Exists(embedded)) return embedded;

            // 2️⃣ JAVA_HOME
            string? javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrEmpty(javaHome))
            {
                string jh = Path.Combine(javaHome, "bin", "java.exe");
                if (File.Exists(jh)) return jh;
            }

            // 3️⃣ البحث في PATH
            string? pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string dir in pathEnv.Split(Path.PathSeparator))
            {
                try
                {
                    string candidate = Path.Combine(dir.Trim(), "java.exe");
                    if (File.Exists(candidate)) return candidate;
                }
                catch { }
            }

            // 4️⃣ fallback بلا مسار (سيفشل لاحقاً بخطأ واضح)
            return "java";
        }

        private void LogMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
                txtLog.ScrollToEnd();
            });
        }

        private void UpdateProgress(int value, string step)
        {
            Dispatcher.Invoke(() =>
            {
                progressBar.Value = value;
                txtCurrentStep.Text = step;
                txtProgress.Text = $"{value}%";
            });
        }

        private void SetStatus(string status)
        {
            Dispatcher.Invoke(() => txtStatus.Text = status);
        }

        #region File Selection

        private async void BtnSelectSourceApk_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new WpfOpenFileDialog
            {
                Filter = "APK Files (*.apk)|*.apk",
                Title = "اختر ملف APK المصدر"
            };

            if (dialog.ShowDialog() == true)
            {
                _sourceApkPath = dialog.FileName;
                txtSourceApk.Text = Path.GetFileName(_sourceApkPath);

                LogMessage($"📦 تم اختيار: {Path.GetFileName(_sourceApkPath)}");
                SetStatus("جاري تحليل APK...");

                await AnalyzeApkAsync();

                btnPreview.IsEnabled        = true;
                btnClone.IsEnabled          = true;
                btnSignOnly.IsEnabled       = true;
                btnExtractNetwork.IsEnabled = true;   // ✅ متاح بعد اختيار APK
                btnApiProfiler.IsEnabled    = true;   // ✅ Android 36 API Profiler
                btnEntropyFingerprint.IsEnabled = true; // ✅ DEX Entropy Fingerprinter
                btnC2Detector.IsEnabled         = true; // ✅ C2 Panel Detector
                btnPrefsExtractor.IsEnabled     = true; // ✅ Preferences Tree Extractor
                SetStatus("جاهز للاستنساخ");
            }
        }

        private async Task AnalyzeApkAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    LogMessage("🔍 جاري تحليل معلومات APK...");

                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName               = _aaptPath,
                            Arguments              = $"dump badging \"{_sourceApkPath}\"",
                            RedirectStandardOutput = true,
                            RedirectStandardError  = true,   // ← مهم: يمنع deadlock
                            UseShellExecute        = false,
                            CreateNoWindow         = true,
                            StandardOutputEncoding = Encoding.UTF8,
                            StandardErrorEncoding  = Encoding.UTF8
                        }
                    };

                    process.Start();

                    // قراءة stdout وstderr بالتوازي لتجنب deadlock
                    var stdoutTask = Task.Run(() => process.StandardOutput.ReadToEnd());
                    var stderrTask = Task.Run(() => process.StandardError.ReadToEnd());
                    process.WaitForExit();
                    Task.WaitAll(stdoutTask, stderrTask);

                    string output    = stdoutTask.Result;
                    string errOutput = stderrTask.Result;

                    if (!string.IsNullOrWhiteSpace(errOutput))
                        LogMessage($"  ⚠ aapt: {errOutput.Trim()}");

                    // ── استخراج معلومات الحزمة ──────────────────────────────────
                    var packageMatch     = Regex.Match(output, @"package: name='([^']+)'");
                    var versionCodeMatch = Regex.Match(output, @"versionCode='([^']+)'");
                    var versionNameMatch = Regex.Match(output, @"versionName='([^']+)'");

                    // ── استخراج اسم التطبيق — نجرب عدة أنماط ───────────────────
                    // بعض التطبيقات تستخدم لغات مختلفة أو تضع الاسم في حقل موارد
                    string appName = "Unknown App";
                    var appNamePatterns = new[]
                    {
                        @"application-label:'([^']+)'",
                        @"application-label-en:'([^']+)'",
                        @"application-label-en-US:'([^']+)'",
                        @"application-label-ar:'([^']+)'",
                        @"application-label-\w[^:]*:'([^']+)'"
                    };
                    foreach (var pattern in appNamePatterns)
                    {
                        var m = Regex.Match(output, pattern);
                        // نتجاهل resource references مثل @0x7f0b0000
                        if (m.Success && !m.Groups[1].Value.StartsWith("@"))
                        {
                            appName = m.Groups[1].Value;
                            break;
                        }
                    }

                    // ── استخراج SDK versions ─────────────────────────────────────
                    var minSdkMatch    = Regex.Match(output, @"sdkVersion:'([^']+)'");
                    var targetSdkMatch = Regex.Match(output, @"targetSdkVersion:'([^']+)'");
                    string minSdk    = minSdkMatch.Success    ? minSdkMatch.Groups[1].Value    : "?";
                    string targetSdk = targetSdkMatch.Success ? targetSdkMatch.Groups[1].Value : "?";

                    _originalPackage = packageMatch.Success     ? packageMatch.Groups[1].Value     : "unknown";
                    _originalVersion = versionNameMatch.Success ? versionNameMatch.Groups[1].Value : "1.0";
                    _originalAppName = appName;

                    string versionCode = versionCodeMatch.Success ? versionCodeMatch.Groups[1].Value : "1";

                    Dispatcher.Invoke(() =>
                    {
                        txtOriginalPackage.Text = $"📦 Package: {_originalPackage}";
                        txtOriginalAppName.Text = $"📱 App: {_originalAppName}";
                        txtOriginalVersion.Text = $"🔢 v{_originalVersion} (code {versionCode})  |  SDK {minSdk}→{targetSdk}";
                        pnlOriginalInfo.Visibility = Visibility.Visible;

                        // ملء حقول الهوية الجديدة تلقائياً
                        txtNewPackage.Text     = _originalPackage + ".clone";
                        txtNewAppName.Text     = _originalAppName + " Clone";
                        txtNewVersionName.Text = _originalVersion;
                        txtNewVersionCode.Text = versionCode;   // ← كان ناقصاً!
                    });

                    LogMessage($"✓ Package: {_originalPackage}");
                    LogMessage($"✓ App Name: {_originalAppName}");
                    LogMessage($"✓ Version: {_originalVersion} (code: {versionCode})");
                    LogMessage($"✓ SDK: minSdk={minSdk}  targetSdk={targetSdk}");
                }
                catch (Exception ex)
                {
                    LogMessage($"❌ خطأ في التحليل: {ex.Message}");
                }
            });
        }

        private void BtnSelectIcon_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new WpfOpenFileDialog
            {
                Filter = "Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
                Title = "اختر الأيقونة الجديدة"
            };

            if (dialog.ShowDialog() == true)
            {
                _newIconPath = dialog.FileName;
                
                try
                {
                    var bitmap = new BitmapImage(new Uri(_newIconPath));
                    imgNewIcon.Source = bitmap;
                    txtIconStatus.Text = $"{bitmap.PixelWidth}x{bitmap.PixelHeight} - سيتم تغيير الحجم";
                    LogMessage($"🎨 تم اختيار أيقونة: {Path.GetFileName(_newIconPath)}");
                }
                catch
                {
                    txtIconStatus.Text = "خطأ في تحميل الصورة";
                }
            }
        }

        #endregion

        #region Preview

        private void BtnPreview_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_sourceApkPath)) return;

            LogMessage("\n========== معاينة التغييرات ==========");
            LogMessage($"📦 Package: {_originalPackage} → {txtNewPackage.Text}");
            LogMessage($"📱 App Name: {_originalAppName} → {txtNewAppName.Text}");
            LogMessage($"🔢 Version: {_originalVersion} → {txtNewVersionName.Text} ({txtNewVersionCode.Text})");
            
            if (!string.IsNullOrEmpty(_newIconPath))
                LogMessage($"🎨 Icon: سيتم استبدال الأيقونة");
            
            LogMessage("\n[التعديلات المخطط لها]:");
            if (chkUpdateSmali.IsChecked == true)
                LogMessage("  ✓ تحديث جميع مراجع Package في ملفات Smali");
            if (chkUpdateResources.IsChecked == true)
                LogMessage("  ✓ تحديث Resource IDs");
            if (chkUpdateProviders.IsChecked == true)
                LogMessage("  ✓ تحديث Content Providers authorities");
            if (chkRemoveSignature.IsChecked == true)
                LogMessage("  ✓ إزالة التوقيع القديم");
            if (chkZipalign.IsChecked == true)
                LogMessage("  ✓ تطبيق Zipalign للتحسين");
            
            if (chkMultiClone.IsChecked == true)
            {
                int count = int.TryParse(txtCloneCount.Text, out int c) ? c : 1;
                LogMessage($"\n[الاستنساخ المتعدد] سيتم إنشاء {count} نسخة");
            }
            
            LogMessage("=====================================\n");
        }

        #endregion

        #region Clone Process

        private async void BtnClone_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_sourceApkPath) || !File.Exists(_sourceApkPath))
            {
                WpfMessageBox.Show("الرجاء اختيار ملف APK أولاً", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtNewPackage.Text))
            {
                WpfMessageBox.Show("الرجاء إدخال Package Name الجديد", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _isCloning = true;
            _cts = new CancellationTokenSource();
            
            btnClone.IsEnabled = false;
            btnPreview.IsEnabled = false;
            btnCancel.IsEnabled = true;

            try
            {
                if (chkMultiClone.IsChecked == true)
                {
                    int count = int.TryParse(txtCloneCount.Text, out int c) ? Math.Min(c, 10) : 1;
                    await CloneMultipleAsync(count);
                }
                else
                {
                    await CloneSingleAsync(txtNewPackage.Text, txtNewAppName.Text);
                }
            }
            catch (OperationCanceledException)
            {
                LogMessage("⚠️ تم إلغاء العملية");
                UpdateProgress(0, "تم الإلغاء");
            }
            catch (Exception ex)
            {
                LogMessage($"❌ خطأ: {ex.Message}");
                UpdateProgress(0, "فشل");
            }
            finally
            {
                _isCloning = false;
                btnClone.IsEnabled = true;
                btnPreview.IsEnabled = true;
                btnCancel.IsEnabled = false;
            }
        }

        private async Task CloneMultipleAsync(int count)
        {
            LogMessage($"\n🔄 بدء الاستنساخ المتعدد ({count} نسخة)...\n");
            
            string basePackage = txtNewPackage.Text;
            string baseAppName = txtNewAppName.Text;
            
            for (int i = 1; i <= count; i++)
            {
                _cts?.Token.ThrowIfCancellationRequested();
                
                string newPackage = $"{basePackage}{i}";
                string newAppName = $"{baseAppName} {i}";
                
                LogMessage($"\n========== النسخة {i}/{count} ==========");
                await CloneSingleAsync(newPackage, newAppName, i, count);
            }
            
            LogMessage($"\n✅ تم إنشاء {count} نسخة بنجاح!");
        }

        private async Task CloneSingleAsync(string newPackage, string newAppName, int current = 1, int total = 1)
        {
            string projectDir = Path.Combine(_workDir, $"clone_{Guid.NewGuid():N}");
            string outputApk  = Path.Combine(_outputDir, $"{newPackage}.apk");

            // ── قراءة حالة UI قبل الدخول في Task.Run لتجنب Cross-Thread Exception ──
            bool doUpdateSmali    = Dispatcher.Invoke(() => chkUpdateSmali.IsChecked    == true);
            bool doUpdateRes      = Dispatcher.Invoke(() => chkUpdateResources.IsChecked == true);
            bool doUpdateProvs    = Dispatcher.Invoke(() => chkUpdateProviders.IsChecked == true);
            bool doRemoveSig      = Dispatcher.Invoke(() => chkRemoveSignature.IsChecked == true);
            bool doZipalign       = Dispatcher.Invoke(() => chkZipalign.IsChecked        == true);
            bool hasNewIcon       = !string.IsNullOrEmpty(_newIconPath);
            string newVersionName = Dispatcher.Invoke(() => txtNewVersionName.Text);
            string newVersionCode = Dispatcher.Invoke(() => txtNewVersionCode.Text);

            try
            {
                int baseProgress = (current - 1) * 100 / total;
                int stepSize     = 100 / total;

                // ── Step 1: Decompile ─────────────────────────────────────────────
                UpdateProgress(baseProgress + stepSize * 5  / 100, "فك تجميع APK...");
                LogMessage("📦 [1/8] فك تجميع APK عبر apktool...");
                await DecompileApkAsync(projectDir);
                _cts?.Token.ThrowIfCancellationRequested();

                // ── Step 2: Dexdump Analysis (حقيقي — يستخدم dexdump.exe 36.1.0) ─
                UpdateProgress(baseProgress + stepSize * 12 / 100, "تحليل DEX...");
                LogMessage("🔬 [2/8] تحليل DEX بـ dexdump v36.1.0...");
                await RunDexdumpAnalysisAsync(projectDir);
                _cts?.Token.ThrowIfCancellationRequested();

                // ── Step 3: Update Manifest (+ ترقية targetSdk = 36) ─────────────
                UpdateProgress(baseProgress + stepSize * 22 / 100, "تحديث Manifest...");
                LogMessage("📝 [3/8] تحديث AndroidManifest.xml (targetSdk=36)...");
                await UpdateManifestAsync(projectDir, newPackage, newAppName,
                    doUpdateProvs, newVersionCode, newVersionName);
                _cts?.Token.ThrowIfCancellationRequested();

                // ── Step 4: Update Smali ──────────────────────────────────────────
                if (doUpdateSmali)
                {
                    UpdateProgress(baseProgress + stepSize * 40 / 100, "تحديث Smali...");
                    LogMessage("🔧 [4/8] تحديث مراجع Package في ملفات Smali...");
                    await UpdateSmaliFilesAsync(projectDir, newPackage);
                    _cts?.Token.ThrowIfCancellationRequested();
                }

                // ── Step 5: Update Resources ──────────────────────────────────────
                if (doUpdateRes)
                {
                    UpdateProgress(baseProgress + stepSize * 53 / 100, "تحديث الموارد...");
                    LogMessage("🎨 [5/8] تحديث strings.xml...");
                    await UpdateResourcesAsync(projectDir, newAppName);
                    _cts?.Token.ThrowIfCancellationRequested();
                }

                // ── Step 6: Replace Icon (Resize حقيقي بـ WPF) ───────────────────
                if (hasNewIcon)
                {
                    UpdateProgress(baseProgress + stepSize * 63 / 100, "تغيير حجم الأيقونة...");
                    LogMessage("🖼️  [6/8] تغيير حجم الأيقونة وتطبيقها (WPF Render)...");
                    await ReplaceIconsAsync(projectDir);
                    _cts?.Token.ThrowIfCancellationRequested();
                }

                // ── Step 6b: Remove Signature (META-INF حقيقي) ──────────────────
                if (doRemoveSig)
                {
                    LogMessage("🗑️  [6b] حذف META-INF (إزالة التوقيع القديم)...");
                    RemoveMetaInf(projectDir);
                    _cts?.Token.ThrowIfCancellationRequested();
                }

                // ── Step 7: Update apktool.yml ────────────────────────────────────
                UpdateProgress(baseProgress + stepSize * 72 / 100, "تحديث apktool.yml...");
                LogMessage("⚙️  [7/8] تحديث apktool.yml...");
                await UpdateApktoolYmlAsync(projectDir, newPackage, newVersionName, newVersionCode);
                _cts?.Token.ThrowIfCancellationRequested();

                // ── Step 8: Recompile ─────────────────────────────────────────────
                UpdateProgress(baseProgress + stepSize * 82 / 100, "إعادة البناء...");
                LogMessage("🔨 [8/8] إعادة تجميع APK...");
                string builtApk = await RecompileApkAsync(projectDir);
                _cts?.Token.ThrowIfCancellationRequested();

                // ── Step 9: Zipalign 36.1.0 ───────────────────────────────────────
                string finalApk = builtApk;
                if (doZipalign && File.Exists(builtApk))
                {
                    UpdateProgress(baseProgress + stepSize * 92 / 100, "Zipalign 36.1.0...");
                    LogMessage("✅  Zipalign (4-byte alignment, build-tools 36.1.0)...");
                    await ZipalignApkAsync(builtApk, outputApk);
                    finalApk = outputApk;
                }
                else if (File.Exists(builtApk))
                {
                    File.Copy(builtApk, outputApk, true);
                    finalApk = outputApk;
                }

                // ── Step 10: V4 Signing (apksigner.jar 36.1.0) ───────────────────
                bool doSign = Dispatcher.Invoke(() => chkSignApk.IsChecked == true);
                if (doSign && File.Exists(finalApk))
                {
                    UpdateProgress(baseProgress + stepSize * 98 / 100, "V4 Signing...");
                    bool signed = await SignApkWithV4Async(finalApk);
                    if (signed)
                        await VerifyApkSignatureAsync(finalApk);
                }

                UpdateProgress(baseProgress + stepSize, "اكتمل!");

                if (File.Exists(finalApk))
                {
                    var fi = new FileInfo(finalApk);
                    LogMessage($"\n✅ APK جاهز: {Path.GetFileName(finalApk)}");
                    LogMessage($"   الحجم: {fi.Length / 1024.0 / 1024.0:F2} MB");
                    LogMessage($"   Package: {newPackage}");
                    LogMessage($"   Target: {finalApk}");
                }
            }
            finally
            {
                if (Directory.Exists(projectDir))
                    try { Directory.Delete(projectDir, true); } catch { }
            }
        }

        #endregion

        #region APK Operations

        private async Task DecompileApkAsync(string outputDir)
        {
            await RunApktoolAsync($"d \"{_sourceApkPath}\" -o \"{outputDir}\" -f");
        }

        private async Task<string> RecompileApkAsync(string projectDir)
        {
            string outputApk = Path.Combine(projectDir, "dist", Path.GetFileName(_sourceApkPath));
            await RunApktoolAsync($"b \"{projectDir}\" -o \"{outputApk}\"");
            return outputApk;
        }

        private async Task ZipalignApkAsync(string inputApk, string outputApk)
        {
            if (!File.Exists(_zipalignPath))
            {
                LogMessage($"  ⚠ zipalign.exe غير موجود في: {_zipalignPath}");
                File.Copy(inputApk, outputApk, true);
                return;
            }

            await Task.Run(() =>
            {
                // zipalign -f -v 4 — 4-byte boundary alignment (required for APK in Android 6+)
                using var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName               = _zipalignPath,
                        Arguments              = $"-f -v 4 \"{inputApk}\" \"{outputApk}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError  = true,
                        UseShellExecute        = false,
                        CreateNoWindow         = true
                    }
                };
                proc.Start();
                string err = proc.StandardError.ReadToEnd();
                proc.WaitForExit();

                if (proc.ExitCode != 0 && !string.IsNullOrWhiteSpace(err))
                    LogMessage($"  ⚠ zipalign: {err.Trim()}");
                else
                    LogMessage($"  ✓ zipalign exitCode={proc.ExitCode}");
            });
        }

        private async Task RunApktoolAsync(string arguments)
        {
            await Task.Run(() =>
            {
                // ══════════════════════════════════════════════════════════════════
                // السبب الحقيقي للتوقف كان السطر 85 من apktool.bat:
                //   for %%i in (%cmdcmdline%) do if /i "%%~i"=="/c" pause & exit /b
                // عند استدعائه عبر cmd.exe /c يُشغَّل "pause" وينتظر ضغطة Enter أبداً!
                // الحل: نستدعي java.exe مع apktool.jar مباشرةً بدون cmd.exe
                // ══════════════════════════════════════════════════════════════════
                string apktoolJar = Path.Combine(
                    Path.GetDirectoryName(_apktoolPath)!, "apktool.jar");

                if (!File.Exists(apktoolJar))
                {
                    LogMessage($"  ❌ apktool.jar غير موجود: {apktoolJar}");
                    return;
                }

                // نفس وسائط JVM الموجودة في apktool.bat السطر 82
                string jvmArgs = $"-jar -Xmx1024M " +
                                 $"-Duser.language=en " +
                                 $"-Dfile.encoding=UTF8 " +
                                 $"-Djdk.util.zip.disableZip64ExtraFieldValidation=true " +
                                 $"-Djdk.nio.zipfs.allowDotZipEntry=true " +
                                 $"\"{apktoolJar}\"";

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName               = _javaExePath,
                        Arguments              = $"{jvmArgs} {arguments}",
                        RedirectStandardOutput = true,
                        RedirectStandardError  = true,
                        UseShellExecute        = false,
                        CreateNoWindow         = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding  = Encoding.UTF8
                    }
                };

                // apktool يكتب رسائله على stdout وstderr — نقرأ كليهما
                process.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        LogMessage($"  {e.Data}");
                };
                process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        LogMessage($"  {e.Data}");
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                LogMessage($"  ✓ apktool انتهى (exitCode={process.ExitCode})");
            });
        }

        #endregion

        #region Update Methods

        private async Task UpdateManifestAsync(
            string projectDir, string newPackage, string newAppName,
            bool updateProviders, string newVersionCode, string newVersionName)
        {
            await Task.Run(() =>
            {
                string manifestPath = Path.Combine(projectDir, "AndroidManifest.xml");
                if (!File.Exists(manifestPath)) return;

                var doc = XDocument.Load(manifestPath);
                XNamespace android = "http://schemas.android.com/apk/res/android";

                // تحديث اسم الحزمة
                doc.Root?.SetAttributeValue("package", newPackage);

                // ── ترقية targetSdkVersion إلى 36 (Android 16) ─────────────────
                // Android 16 يشترط targetSdk >= 28 للتشغيل الكامل
                var usesSdk = doc.Descendants("uses-sdk").FirstOrDefault();
                if (usesSdk == null)
                {
                    usesSdk = new XElement("uses-sdk");
                    doc.Root?.AddFirst(usesSdk);
                }
                // نرفع فقط إذا كان أقل من 36 — لا نخفّض أبداً
                string curTarget = usesSdk.Attribute(android + "targetSdkVersion")?.Value ?? "0";
                if (!int.TryParse(curTarget, out int curTargetInt)) curTargetInt = 0;
                if (curTargetInt < 36)
                {
                    usesSdk.SetAttributeValue(android + "targetSdkVersion", "36");
                    LogMessage($"  ↑ targetSdkVersion: {curTargetInt} → 36");
                }

                // minSdkVersion لا تقل عن 28 (شرط Android 16)
                string curMin = usesSdk.Attribute(android + "minSdkVersion")?.Value ?? "0";
                if (!int.TryParse(curMin, out int curMinInt)) curMinInt = 0;
                if (curMinInt < 28)
                {
                    usesSdk.SetAttributeValue(android + "minSdkVersion", "28");
                    LogMessage($"  ↑ minSdkVersion: {curMinInt} → 28");
                }

                // تحديث versionCode و versionName في عنصر <manifest>
                if (!string.IsNullOrWhiteSpace(newVersionCode))
                    doc.Root?.SetAttributeValue(android + "versionCode", newVersionCode);
                if (!string.IsNullOrWhiteSpace(newVersionName))
                    doc.Root?.SetAttributeValue(android + "versionName", newVersionName);

                // تحديث label التطبيق
                var application = doc.Descendants("application").FirstOrDefault();
                if (application != null)
                    application.SetAttributeValue(android + "label", newAppName);

                // تحديث authorities لـ Content Providers
                if (updateProviders && !string.IsNullOrEmpty(_originalPackage))
                {
                    int provCount = 0;
                    foreach (var provider in doc.Descendants("provider"))
                    {
                        string? auth = provider.Attribute(android + "authorities")?.Value;
                        if (!string.IsNullOrEmpty(auth))
                        {
                            provider.SetAttributeValue(android + "authorities",
                                auth.Replace(_originalPackage, newPackage));
                            provCount++;
                        }
                    }
                    if (provCount > 0) LogMessage($"  ✓ Content Providers محدَّثة: {provCount}");
                }

                doc.Save(manifestPath);
                LogMessage($"  ✓ Package: {newPackage}");
                LogMessage($"  ✓ Label: {newAppName}");
                LogMessage($"  ✓ targetSdkVersion=36 | minSdkVersion=28");
            });
        }

        private async Task UpdateSmaliFilesAsync(string projectDir, string newPackage)
        {
            await Task.Run(() =>
            {
                if (string.IsNullOrEmpty(_originalPackage)) return;

                string oldPackagePath = _originalPackage.Replace(".", "/");
                string newPackagePath = newPackage.Replace(".", "/");

                int updatedFiles = 0;
                var smaliDirs = GetSmaliDirectories(projectDir);

                foreach (var smaliDir in smaliDirs)
                {
                    foreach (var file in Directory.GetFiles(smaliDir, "*.smali", SearchOption.AllDirectories))
                    {
                        _cts?.Token.ThrowIfCancellationRequested();
                        
                        string content = File.ReadAllText(file);
                        if (content.Contains(oldPackagePath) || content.Contains(_originalPackage))
                        {
                            content = content.Replace($"L{oldPackagePath}", $"L{newPackagePath}");
                            content = content.Replace($"\"{_originalPackage}", $"\"{newPackage}");
                            File.WriteAllText(file, content);
                            updatedFiles++;
                        }
                    }
                }

                LogMessage($"  ✓ تم تحديث {updatedFiles} ملف Smali");
            });
        }

        private async Task UpdateResourcesAsync(string projectDir, string newAppName)
        {
            await Task.Run(() =>
            {
                // Update strings.xml
                string stringsPath = Path.Combine(projectDir, "res", "values", "strings.xml");
                if (File.Exists(stringsPath))
                {
                    try
                    {
                        var doc = XDocument.Load(stringsPath);
                        var appNameString = doc.Descendants("string")
                            .FirstOrDefault(s => s.Attribute("name")?.Value == "app_name");
                        
                        if (appNameString != null)
                        {
                            appNameString.Value = newAppName;
                            doc.Save(stringsPath);
                            LogMessage($"  ✓ تم تحديث app_name في strings.xml");
                        }
                    }
                    catch { }
                }

                // Update other language strings
                string resDir = Path.Combine(projectDir, "res");
                if (Directory.Exists(resDir))
                {
                    foreach (var valuesDir in Directory.GetDirectories(resDir, "values-*"))
                    {
                        string langStringsPath = Path.Combine(valuesDir, "strings.xml");
                        if (File.Exists(langStringsPath))
                        {
                            try
                            {
                                var doc = XDocument.Load(langStringsPath);
                                var appNameString = doc.Descendants("string")
                                    .FirstOrDefault(s => s.Attribute("name")?.Value == "app_name");
                                
                                if (appNameString != null)
                                {
                                    appNameString.Value = newAppName;
                                    doc.Save(langStringsPath);
                                }
                            }
                            catch { }
                        }
                    }
                }
            });
        }

        /// <summary>
        /// تغيير حجم الأيقونة وحفظها بالأبعاد الصحيحة لكل كثافة شاشة باستخدام WPF Rendering.
        /// هذا يضمن أن الأيقونة تظهر بجودة كاملة على جميع الأجهزة.
        /// </summary>
        private async Task ReplaceIconsAsync(string projectDir)
        {
            if (string.IsNullOrEmpty(_newIconPath) || !File.Exists(_newIconPath)) return;

            string resDir = Path.Combine(projectDir, "res");
            if (!Directory.Exists(resDir)) return;

            // أبعاد الأيقونة القياسية لكل كثافة شاشة Android
            var densityMap = new Dictionary<string, int>
            {
                { "mipmap-mdpi",      48  },
                { "mipmap-hdpi",      72  },
                { "mipmap-xhdpi",     96  },
                { "mipmap-xxhdpi",    144 },
                { "mipmap-xxxhdpi",   192 },
                { "drawable-mdpi",    48  },
                { "drawable-hdpi",    72  },
                { "drawable-xhdpi",   96  },
                { "drawable-xxhdpi",  144 },
                { "drawable-xxxhdpi", 192 },
            };

            // تحميل الصورة المصدر مرة واحدة فقط
            BitmapImage sourceImage = null!;
            await Dispatcher.InvokeAsync(() =>
            {
                sourceImage = new BitmapImage(new Uri(_newIconPath));
                sourceImage.Freeze(); // thread-safe
            });

            int total = 0;
            await Task.Run(() =>
            {
                foreach (var (folder, px) in densityMap)
                {
                    string iconDir = Path.Combine(resDir, folder);
                    if (!Directory.Exists(iconDir)) continue;

                    // نبحث عن كل ملفات PNG للأيقونة في هذا المجلد
                    var targets = Directory.GetFiles(iconDir, "ic_launcher*.png")
                                  .Concat(Directory.GetFiles(iconDir, "icon*.png"))
                                  .ToArray();

                    foreach (var targetFile in targets)
                    {
                        try
                        {
                            // تغيير الحجم بـ WPF (bicubic جودة عالية)
                            byte[] pngBytes = ResizeImageWpf(sourceImage, px, px);
                            File.WriteAllBytes(targetFile, pngBytes);
                            total++;
                        }
                        catch (Exception ex)
                        {
                            LogMessage($"  ⚠ فشل استبدال {Path.GetFileName(targetFile)}: {ex.Message}");
                        }
                    }
                }
            });

            LogMessage($"  ✓ تم تغيير حجم الأيقونة وحفظها في {total} موضع");
        }

        /// <summary>
        /// تغيير حجم BitmapImage إلى أبعاد محددة وإعادتها كـ PNG bytes.
        /// يستخدم WPF RenderTarget للحصول على جودة عالية.
        /// </summary>
        private static byte[] ResizeImageWpf(BitmapImage source, int width, int height)
        {
            var drawingVisual = new DrawingVisual();
            using (var dc = drawingVisual.RenderOpen())
            {
                dc.DrawImage(source, new Rect(0, 0, width, height));
            }

            var rtb = new RenderTargetBitmap(
                width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(drawingVisual);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));

            using var ms = new MemoryStream();
            encoder.Save(ms);
            return ms.ToArray();
        }

        private async Task UpdateApktoolYmlAsync(
            string projectDir, string newPackage,
            string newVersionName, string newVersionCode)
        {
            await Task.Run(() =>
            {
                string ymlPath = Path.Combine(projectDir, "apktool.yml");
                if (!File.Exists(ymlPath)) return;

                string content = File.ReadAllText(ymlPath);

                // تحديث versionName — نص: يحتاج أقواس مفردة (صحيح)
                if (!string.IsNullOrWhiteSpace(newVersionName))
                    content = Regex.Replace(content,
                        @"versionName:\s*'?[^'\n]+'?",
                        $"versionName: '{newVersionName}'");

                // تحديث versionCode — رقم صحيح: يجب بدون أقواس!
                // apktool 3.x يرفض '105' ويقبل 105 فقط
                // السبب: NumberFormatException: For input string: "'105'"
                if (!string.IsNullOrWhiteSpace(newVersionCode))
                {
                    // نحذف أي أقواس محتملة من القيمة قبل الكتابة
                    string cleanCode = newVersionCode.Trim().Trim('\'');
                    if (!int.TryParse(cleanCode, out _)) cleanCode = "1";
                    content = Regex.Replace(content,
                        @"versionCode:\s*'?\d+'?",
                        $"versionCode: {cleanCode}");
                }

                // renameManifestPackage — يُخبر apktool بالـ package الجديد
                if (!content.Contains("renameManifestPackage"))
                    content += $"\nrenameManifestPackage: {newPackage}\n";
                else
                    content = Regex.Replace(content,
                        @"renameManifestPackage:.*",
                        $"renameManifestPackage: {newPackage}");

                // لا نُغيِّر frameworkVersion — apktool يختار الإطار تلقائياً
                // تغييره يدوياً كان يُسبب أخطاء في بعض APKs

                File.WriteAllText(ymlPath, content);
                string logCode = newVersionCode.Trim().Trim('\'');
                LogMessage($"  ✓ apktool.yml: package={newPackage}, versionCode={logCode}");
            });
        }

        #endregion

        #region Android 36 — Real Analysis Methods

        /// <summary>
        /// تشغيل dexdump.exe الحقيقي من build-tools/36.1.0 لتحليل ملفات DEX.
        /// يُستخرج: عدد الكلاسات، عدد الميثودز، الحزم الملحوظة.
        /// </summary>
        private async Task RunDexdumpAnalysisAsync(string projectDir)
        {
            if (!File.Exists(_dexdumpPath))
            {
                LogMessage("  ⚠ dexdump.exe غير موجود — تخطي تحليل DEX");
                return;
            }

            // نجمع كل ملفات classes*.dex في المشروع
            var dexFiles = Directory.GetFiles(projectDir, "classes*.dex",
                SearchOption.TopDirectoryOnly);

            if (dexFiles.Length == 0)
            {
                LogMessage("  ℹ لا توجد ملفات DEX في مجلد المشروع (طبيعي بعد فك التجميع)");
                return;
            }

            await Task.Run(() =>
            {
                int totalClasses = 0;
                int totalMethods = 0;

                foreach (var dex in dexFiles)
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName               = _dexdumpPath,
                        Arguments              = $"-f \"{dex}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError  = true,
                        UseShellExecute        = false,
                        CreateNoWindow         = true,
                        StandardOutputEncoding = Encoding.UTF8
                    };

                    using var proc = Process.Start(psi)!;
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit();

                    // استخراج إحصائيات من مخرجات dexdump
                    var classMatches  = Regex.Matches(output, @"^Class #", RegexOptions.Multiline);
                    var methodMatches = Regex.Matches(output, @"^\s+Method #", RegexOptions.Multiline);
                    totalClasses += classMatches.Count;
                    totalMethods += methodMatches.Count;

                    LogMessage($"  📊 {Path.GetFileName(dex)}: "
                             + $"{classMatches.Count} كلاس، {methodMatches.Count} ميثود");
                }

                LogMessage($"  📊 الإجمالي: {totalClasses} كلاس — {totalMethods} ميثود");
            });
        }

        /// <summary>
        /// حذف مجلد META-INF بالكامل من مجلد المشروع المفكوك.
        /// هذا يزيل التوقيع الرقمي القديم — ضروري قبل إعادة البناء والتوقيع.
        /// </summary>
        private void RemoveMetaInf(string projectDir)
        {
            // apktool لا يُدرج META-INF في المشروع المفكوك عادةً،
            // لكن بعض الأدوات قد تتركه
            string metaInfPath = Path.Combine(projectDir, "META-INF");
            if (Directory.Exists(metaInfPath))
            {
                Directory.Delete(metaInfPath, recursive: true);
                LogMessage("  ✓ META-INF محذوف (التوقيع القديم أُزيل)");
            }
            else
            {
                // apktool فككه بالفعل — التوقيع لن يُدرج تلقائياً عند إعادة البناء
                LogMessage("  ✓ META-INF غير موجود (apktool أزاله تلقائياً)");
            }
        }

        #endregion

        #region Helper Methods

        private List<string> GetSmaliDirectories(string projectDir)
        {
            var smaliDirs = new List<string>();

            string baseSmaliDir = Path.Combine(projectDir, "smali");
            if (Directory.Exists(baseSmaliDir))
                smaliDirs.Add(baseSmaliDir);

            for (int i = 2; i <= 10; i++)
            {
                string smaliClassesDir = Path.Combine(projectDir, $"smali_classes{i}");
                if (Directory.Exists(smaliClassesDir))
                    smaliDirs.Add(smaliClassesDir);
            }

            return smaliDirs;
        }

        /// <summary>
        /// يعيد مسار الـ Keystore الفعّال: إما ما أدخله المستخدم أو debug.keystore الافتراضي.
        /// </summary>
        private string ResolveKeystorePath()
        {
            string raw = Dispatcher.Invoke(() => txtKeystorePath.Text).Trim();
            if (!string.IsNullOrEmpty(raw)
                && !raw.Contains("افتراضي")
                && File.Exists(raw))
                return raw;

            // debug.keystore الافتراضي من ResearchPayloadTools
            if (File.Exists(_debugKeystore)) return _debugKeystore;

            // debug.keystore القياسي من Android SDK
            string androidHome = Environment.GetEnvironmentVariable("ANDROID_HOME") ?? "";
            string sdkDebug = Path.Combine(androidHome, "debug.keystore");
            if (File.Exists(sdkDebug)) return sdkDebug;

            // debug.keystore من مجلد المستخدم (مولَّد تلقائياً بواسطة Android Studio)
            string userDebug = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".android", "debug.keystore");
            return userDebug;
        }

        #endregion

        #region Android 36 — V4 Signing (apksigner.jar 36.1.0 + JRE مضمّن)

        // ══════════════════════════════════════════════════════════════════════
        //  SignApkWithV4Async — التوقيع الاحترافي المُحسَّن
        // ══════════════════════════════════════════════════════════════════════
        // مبني على apksigner.bat الأصلي من Google (build-tools/36.1.0):
        //   -Xmx1024M -Xss1m (من السطر 108 في apksigner.bat)
        //   يستخدم java.exe المضمّنة في build-tools/36.1.0/bin/
        //   يدعم --out لإنتاج ملف موقَّع جديد بجانب الأصل
        //   يتحقق من .idsig عند تفعيل V4
        // ══════════════════════════════════════════════════════════════════════
        private async Task<bool> SignApkWithV4Async(string apkPath)
        {
            // ── التحقق من وجود الأدوات ───────────────────────────────────────────
            if (!File.Exists(_apksignerJar))
            {
                LogMessage($"  ❌ apksigner.jar غير موجود: {_apksignerJar}");
                return false;
            }

            if (!File.Exists(apkPath))
            {
                LogMessage($"  ❌ ملف APK غير موجود: {apkPath}");
                return false;
            }

            // ── التحقق من Keystore — توليد تلقائي إن لم يوجد ─────────────────────
            string keystorePath = ResolveKeystorePath();
            if (!File.Exists(keystorePath))
            {
                LogMessage("  ⚠️ debug.keystore غير موجود — جاري التوليد التلقائي...");
                bool generated = await AutoGenerateDebugKeystoreAsync(keystorePath);
                if (!generated)
                {
                    LogMessage("  ❌ فشل توليد Keystore — تعذّر التوقيع");
                    return false;
                }
            }

            // ── قراءة قيم UI من Thread الرئيسي ──────────────────────────────────
            bool   v1      = Dispatcher.Invoke(() => chkV1Sign.IsChecked == true);
            bool   v2      = Dispatcher.Invoke(() => chkV2Sign.IsChecked == true);
            bool   v3      = Dispatcher.Invoke(() => chkV3Sign.IsChecked == true);
            bool   v4      = Dispatcher.Invoke(() => chkV4Sign.IsChecked == true);
            string alias   = Dispatcher.Invoke(() => txtKeyAlias.Text.Trim());
            string ksPass  = Dispatcher.Invoke(() => pwdKeystore.Password);
            string keyPass = ksPass;

            // ── تحديد minSdkVersion الصحيح ───────────────────────────────────────
            // V4 يتطلب minSdk >= 30 لكن apksigner يُطبَّق على APK كما هو
            // نحدد min-sdk-version من قيمة targetSdk الحقيقية للـ APK أو 28 كحد أدنى
            int minSdkForSigning = await GetApkMinSdkAsync(apkPath);
            // apksigner يحتاج --min-sdk-version ليتحكم في الـ schemes المدعومة
            // إذا كان minSdk أقل من 30 ولكن V4 مُفعَّل، نضبط على 28 (Android 16 يتطلبه)
            int effectiveMinSdk = Math.Max(minSdkForSigning, 28);

            // ── بناء وسائط apksigner ─────────────────────────────────────────────
            // الصيغة المرجعية من Google apksigner.bat:
            // java -Xmx1024M -Xss1m -jar apksigner.jar sign [options] <apk>
            string outApkPath = apkPath; // التوقيع in-place (apksigner يدعم ذلك)

            var signerArgs = new StringBuilder();
            signerArgs.Append("sign");
            signerArgs.Append($" --ks \"{keystorePath}\"");
            signerArgs.Append($" --ks-key-alias \"{alias}\"");
            signerArgs.Append($" --ks-pass pass:{ksPass}");
            signerArgs.Append($" --key-pass pass:{keyPass}");
            signerArgs.Append($" --v1-signing-enabled {v1.ToString().ToLower()}");
            signerArgs.Append($" --v2-signing-enabled {v2.ToString().ToLower()}");
            signerArgs.Append($" --v3-signing-enabled {v3.ToString().ToLower()}");
            signerArgs.Append($" --v4-signing-enabled {v4.ToString().ToLower()}");
            signerArgs.Append($" --min-sdk-version {effectiveMinSdk}");
            // توليد verbose output للتشخيص
            signerArgs.Append(" --v");
            signerArgs.Append($" \"{outApkPath}\"");

            // وسائط JVM مثل apksigner.bat الرسمي من Google
            string jvmArgs = $"-Xmx1024M -Xss1m -jar \"{_apksignerJar}\"";
            string fullArgs = $"{jvmArgs} {signerArgs}";

            LogMessage($"\n🔏 [V4 Signing] — build-tools/36.1.0");
            LogMessage($"   ☕ Java  : {Path.GetFileName(_javaExePath)}");
            LogMessage($"   📦 APK  : {Path.GetFileName(apkPath)}");
            LogMessage($"   🔑 KS   : {Path.GetFileName(keystorePath)} | alias={alias}");
            LogMessage($"   🛡️  Schemes: V1={v1} V2={v2} V3={v3} V4={v4}");
            LogMessage($"   📋 minSdk: {effectiveMinSdk} (APK أصلي: {minSdkForSigning})");

            bool success = false;
            await Task.Run(() =>
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = _javaExePath,
                    Arguments              = fullArgs,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding  = Encoding.UTF8
                };

                using var proc = Process.Start(psi)!;

                // قراءة stdout وstderr بالتوازي لتجنّب الـ deadlock
                string stdout = "";
                string stderr = "";
                var t1 = Task.Run(() => stdout = proc.StandardOutput.ReadToEnd());
                var t2 = Task.Run(() => stderr = proc.StandardError.ReadToEnd());

                // انتظار بـ timeout 120 ثانية (APKs كبيرة تأخذ وقتاً)
                bool exited = proc.WaitForExit(120_000);
                Task.WaitAll(t1, t2);

                if (!exited)
                {
                    try { proc.Kill(); } catch { }
                    LogMessage("  ❌ انتهت مهلة التوقيع (120ث) — تم إيقاف العملية");
                    return;
                }

                success = proc.ExitCode == 0;

                // عرض مخرجات apksigner المفيدة
                foreach (string raw in new[] { stdout, stderr })
                {
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    foreach (string line in raw.Split('\n'))
                    {
                        string l = line.Trim();
                        if (string.IsNullOrEmpty(l)) continue;
                        // نُظهر فقط الأسطر المفيدة
                        if (l.StartsWith("WARNING", StringComparison.OrdinalIgnoreCase) ||
                            l.StartsWith("ERROR",   StringComparison.OrdinalIgnoreCase) ||
                            l.StartsWith("Signed",  StringComparison.OrdinalIgnoreCase) ||
                            l.Contains("scheme") || l.Contains("signer") ||
                            l.Contains("Exception") || l.Contains("failed"))
                        {
                            LogMessage($"  {l}");
                        }
                    }
                }

                if (success)
                {
                    LogMessage($"  ✅ تم التوقيع بنجاح (exitCode=0)");

                    // V4 Signing يولّد ملف .idsig منفصل للـ Incremental APK Installation
                    string idsigPath = apkPath + ".idsig";
                    if (v4 && File.Exists(idsigPath))
                    {
                        long idsigSize = new FileInfo(idsigPath).Length;
                        LogMessage($"  📄 V4 .idsig : {Path.GetFileName(idsigPath)} ({idsigSize / 1024.0:F1} KB)");
                        LogMessage("  ℹ️  نقل .idsig مع APK للتثبيت التزايدي عبر adb");
                    }
                    else if (v4)
                    {
                        LogMessage("  ⚠️ ملف .idsig لم يُولَّد — تحقق من إصدار apksigner");
                    }

                    // عرض حجم APK الموقَّع
                    long apkSize = new FileInfo(apkPath).Length;
                    LogMessage($"  📦 حجم APK الموقَّع: {apkSize / 1024.0 / 1024.0:F2} MB");
                }
                else
                {
                    LogMessage($"  ❌ فشل التوقيع (exitCode={proc.ExitCode})");
                    LogMessage($"  ℹ️  java المستخدمة: {_javaExePath}");
                    LogMessage($"  ℹ️  apksigner.jar : {_apksignerJar}");
                }
            });

            return success;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  AutoGenerateDebugKeystoreAsync
        // ══════════════════════════════════════════════════════════════════════
        // يولّد debug.keystore متوافق مع Android Studio باستخدام keytool
        // من build-tools/36.1.0/bin/ مباشرةً.
        // البيانات الافتراضية: alias=androiddebugkey | pass=android | validity=27375 يوم (75 سنة)
        // ══════════════════════════════════════════════════════════════════════
        private async Task<bool> AutoGenerateDebugKeystoreAsync(string keystorePath)
        {
            // نبحث عن keytool في نفس bin/ الذي وجدنا فيه java
            string keytool = _keytoolPath;
            bool useJavaKeytool = false;

            if (!File.Exists(keytool))
            {
                // fallback: keytool بجانب java.exe
                string javaDir = Path.GetDirectoryName(_javaExePath) ?? "";
                keytool = Path.Combine(javaDir, "keytool.exe");
                if (!File.Exists(keytool))
                {
                    // fallback أخير: keytool.exe من PATH
                    keytool = "keytool";
                    useJavaKeytool = true;
                }
            }

            LogMessage($"  🔧 توليد debug.keystore عبر keytool...");
            if (!useJavaKeytool)
                LogMessage($"  ☕ keytool: {Path.GetFileName(keytool)}");

            // حذف قديم إن وجد (لتجنّب تعارض)
            if (File.Exists(keystorePath))
                try { File.Delete(keystorePath); } catch { }

            string args =
                $"-genkeypair" +
                $" -keystore \"{keystorePath}\"" +
                $" -storepass android" +
                $" -keypass android" +
                $" -alias androiddebugkey" +
                $" -keyalg RSA" +
                $" -keysize 2048" +
                $" -validity 27375" +
                $" -dname \"CN=Android Debug,O=Android,C=US\"" +
                $" -storetype JKS";

            bool ok = false;
            await Task.Run(() =>
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = keytool,
                    Arguments              = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                try
                {
                    using var proc = Process.Start(psi)!;
                    string err = proc.StandardError.ReadToEnd();
                    proc.WaitForExit(30_000);
                    ok = proc.ExitCode == 0 && File.Exists(keystorePath);

                    if (ok)
                        LogMessage($"  ✅ debug.keystore مُولَّد تلقائياً ({new FileInfo(keystorePath).Length / 1024.0:F1} KB)");
                    else
                        LogMessage($"  ⚠️ keytool: {err.Trim().Replace("\n", " ")}");
                }
                catch (Exception ex)
                {
                    LogMessage($"  ❌ keytool غير متاح: {ex.Message}");
                }
            });

            return ok;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  GetApkMinSdkAsync — يستخرج minSdkVersion الحقيقي من APK
        // ══════════════════════════════════════════════════════════════════════
        private async Task<int> GetApkMinSdkAsync(string apkPath)
        {
            int result = 28; // Android 16 minimum per build.prop
            if (!File.Exists(_aapt2Path) && !File.Exists(_aaptPath)) return result;

            await Task.Run(() =>
            {
                string tool = File.Exists(_aapt2Path) ? _aapt2Path : _aaptPath;
                var psi = new ProcessStartInfo
                {
                    FileName               = tool,
                    Arguments              = $"dump badging \"{apkPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                try
                {
                    using var proc = Process.Start(psi)!;
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(20_000);

                    var m = Regex.Match(output, @"sdkVersion:'(\d+)'");
                    if (m.Success && int.TryParse(m.Groups[1].Value, out int sdk))
                        result = sdk;
                }
                catch { }
            });

            return result;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  VerifyApkSignatureAsync — تحقق شامل مُحسَّن
        // ══════════════════════════════════════════════════════════════════════
        // يتحقق من:
        //   - كل signing schemes المُفعَّلة (V1/V2/V3/V4)
        //   - معلومات الشهادة (Subject, Issuer, SHA-256)
        //   - وجود ملف .idsig لـ V4
        //   - تاريخ انتهاء الشهادة
        // ══════════════════════════════════════════════════════════════════════
        private async Task VerifyApkSignatureAsync(string apkPath)
        {
            if (!File.Exists(_apksignerJar) || !File.Exists(apkPath)) return;

            LogMessage("\n🔍 التحقق من التوقيع (apksigner verify)...");

            await Task.Run(() =>
            {
                string jvmArgs   = $"-Xmx512M -jar \"{_apksignerJar}\"";
                string verifyArgs = $"verify --verbose --print-certs \"{apkPath}\"";

                var psi = new ProcessStartInfo
                {
                    FileName               = _javaExePath,
                    Arguments              = $"{jvmArgs} {verifyArgs}",
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding  = Encoding.UTF8
                };

                using var proc = Process.Start(psi)!;
                string stdout = proc.StandardOutput.ReadToEnd();
                string stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit(30_000);

                string all = stdout + stderr;
                if (string.IsNullOrWhiteSpace(all)) return;

                bool verified       = false;
                bool v1ok = false, v2ok = false, v3ok = false, v4ok = false;

                foreach (string line in all.Split('\n'))
                {
                    string l = line.Trim();
                    if (string.IsNullOrEmpty(l)) continue;

                    if (l.StartsWith("Verified"))        { verified = true; LogMessage($"  ✅ {l}"); }
                    else if (l.Contains("v1 scheme"))    { v1ok = l.Contains("true"); LogMessage($"  🛡️ {l}"); }
                    else if (l.Contains("v2 scheme"))    { v2ok = l.Contains("true"); LogMessage($"  🛡️ {l}"); }
                    else if (l.Contains("v3 scheme"))    { v3ok = l.Contains("true"); LogMessage($"  🛡️ {l}"); }
                    else if (l.Contains("v4 scheme"))    { v4ok = l.Contains("true"); LogMessage($"  🛡️ {l}"); }
                    else if (l.Contains("Signer #"))     LogMessage($"  🔏 {l}");
                    else if (l.Contains("Subject:"))     LogMessage($"  👤 {l}");
                    else if (l.Contains("Issuer:"))      LogMessage($"  🏛️ {l}");
                    else if (l.Contains("SHA-256"))      LogMessage($"  #️⃣ {l}");
                    else if (l.Contains("Not valid"))    LogMessage($"  📅 {l}");
                    else if (l.Contains("WARNING") || l.Contains("ERROR") || l.Contains("Exception"))
                        LogMessage($"  ⚠️ {l}");
                }

                // ملخص نهائي
                if (verified)
                {
                    string schemes = string.Join(" ", new[]
                    {
                        v1ok?"V1":"" , v2ok?"V2":"" , v3ok?"V3":"" , v4ok?"V4":""
                    }.Where(s => !string.IsNullOrEmpty(s)));
                    LogMessage($"  📋 Schemes نشطة: {(string.IsNullOrEmpty(schemes) ? "(غير محدد)" : schemes)}");
                }

                // التحقق من .idsig إن كان V4 مُفعَّلاً
                bool v4Enabled = System.Windows.Application.Current.Dispatcher
                    .Invoke(() => Dispatcher.Invoke(() =>
                        System.Windows.Application.Current.Windows
                            .OfType<ApkClonerWindow>().FirstOrDefault()
                            ?.chkV4Sign.IsChecked == true));

                string idsigPath = apkPath + ".idsig";
                if (File.Exists(idsigPath))
                {
                    long sz = new FileInfo(idsigPath).Length;
                    LogMessage($"  📄 V4 .idsig موجود ✅ ({sz / 1024.0:F1} KB) — جاهز للـ Incremental Install");
                }
            });
        }

        #endregion

        #region UI Events

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (_isCloning)
            {
                var result = WpfMessageBox.Show(
                    "هل تريد إلغاء عملية الاستنساخ؟", "تأكيد",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _cts?.Cancel();
                    LogMessage("⏹️ جاري إلغاء العملية...");
                }
            }
        }

        private void BtnOpenOutput_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!Directory.Exists(_outputDir))
                    Directory.CreateDirectory(_outputDir);
                Process.Start("explorer.exe", _outputDir);
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"خطأ في فتح المجلد:\n{ex.Message}",
                    "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// اختيار ملف Keystore من النظام وتحديث حقل المسار.
        /// </summary>
        private void BtnSelectKeystore_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new WpfOpenFileDialog
            {
                Filter = "Keystore Files (*.keystore;*.jks)|*.keystore;*.jks|All Files (*.*)|*.*",
                Title  = "اختر ملف Keystore"
            };

            if (dialog.ShowDialog() == true)
            {
                txtKeystorePath.Text      = dialog.FileName;
                txtKeystorePath.Foreground = System.Windows.Media.Brushes.White;
                LogMessage($"🔑 Keystore: {Path.GetFileName(dialog.FileName)}");
            }
        }

        /// <summary>
        /// توقيع APK موجود مستقل (بدون استنساخ) بالضغط على الزر المخصص.
        /// يفتح مربع حوار لاختيار APK ثم يوقّعه مباشرةً.
        /// </summary>
        private async void BtnSignOnly_Click(object sender, RoutedEventArgs e)
        {
            // اختيار APK المراد توقيعه
            var dialog = new WpfOpenFileDialog
            {
                Filter = "APK Files (*.apk)|*.apk",
                Title  = "اختر ملف APK للتوقيع"
            };
            if (dialog.ShowDialog() != true) return;

            string apkPath = dialog.FileName;

            btnSignOnly.IsEnabled = false;
            SetStatus("جاري التوقيع...");
            LogMessage($"\n📦 APK: {Path.GetFileName(apkPath)}");

            try
            {
                // Zipalign قبل التوقيع (apksigner يرفض APK غير مُزالَّن)
                if (chkZipalign.IsChecked == true)
                {
                    string aligned = apkPath.Replace(".apk", "_aligned.apk");
                    LogMessage("⚙️  تطبيق Zipalign قبل التوقيع...");
                    await ZipalignApkAsync(apkPath, aligned);
                    if (File.Exists(aligned))
                    {
                        File.Delete(apkPath);
                        File.Move(aligned, apkPath);
                    }
                }

                bool signed = await SignApkWithV4Async(apkPath);

                if (signed)
                {
                    await VerifyApkSignatureAsync(apkPath);
                    SetStatus("تم التوقيع ✅");
                }
                else
                {
                    SetStatus("فشل التوقيع ❌");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ خطأ: {ex.Message}");
                SetStatus("خطأ");
            }
            finally
            {
                btnSignOnly.IsEnabled = true;
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_isCloning)
            {
                var result = WpfMessageBox.Show(
                    "عملية الاستنساخ قيد التنفيذ. هل تريد الإلغاء والخروج؟",
                    "تأكيد", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
                _cts?.Cancel();
            }

            try
            {
                if (Directory.Exists(_workDir))
                    Directory.Delete(_workDir, true);
            }
            catch { }

            base.OnClosing(e);
        }

        #endregion

        #region Network Info Extraction (Smali + Resources)

        /// <summary>
        /// استخراج معلومات الاتصال الكاملة من APK.
        /// يفك APK مؤقتاً ثم يمسح Smali/XML/assets للكشف عن:
        /// URLs, IPs, API Endpoints, WebSockets, Firebase, Secrets, Deep-Links.
        /// </summary>
        private async void BtnExtractNetwork_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_sourceApkPath) || !File.Exists(_sourceApkPath))
            {
                WpfMessageBox.Show("اختر ملف APK أولاً.", "تنبيه",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            btnExtractNetwork.IsEnabled = false;
            SetStatus("جاري استخراج معلومات الاتصال...");
            LogMessage("\n══════════════════════════════════════════");
            LogMessage("🌐 [استخراج معلومات الاتصال] — Smali + Resources");
            LogMessage("══════════════════════════════════════════");

            string extractDir = Path.Combine(_workDir, $"netextract_{Guid.NewGuid():N}");

            try
            {
                var result = await Task.Run(() => DoExtractNetworkInfo(extractDir));
                DisplayNetworkResults(result);
            }
            catch (Exception ex)
            {
                LogMessage($"❌ خطأ في الاستخراج: {ex.Message}");
            }
            finally
            {
                try { if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true); } catch { }
                btnExtractNetwork.IsEnabled = true;
                SetStatus("اكتمل الاستخراج");
            }
        }

        /// <summary>
        /// المنطق الفعلي للاستخراج — يعمل على Thread منفصل.
        /// الخطوات:
        ///   1. فك APK عبر apktool (--no-src لسرعة أكبر، fallback بدونه).
        ///   2. مسح ملفات Smali لاستخراج const-string / const-string/jumbo.
        ///   3. مسح AndroidManifest + res + assets.
        /// </summary>
        private NetworkExtractResult DoExtractNetworkInfo(string workDir)
        {
            var result = new NetworkExtractResult();

            // ─── Step 1: فك APK ──────────────────────────────────────────────
            LogMessage("📦 [1/3] فك APK عبر apktool...");

            RunApktoolSync($"d \"{_sourceApkPath}\" -o \"{workDir}\" -f --no-src");

            // إذا لم يُنتج مجلد smali (--no-src يحتاج dex) نعيد المحاولة
            if (!Directory.Exists(Path.Combine(workDir, "smali")))
            {
                LogMessage("  ↩ إعادة المحاولة بدون --no-src...");
                try { Directory.Delete(workDir, true); } catch { }
                RunApktoolSync($"d \"{_sourceApkPath}\" -o \"{workDir}\" -f");
            }

            LogMessage($"  ✓ فُكّ إلى: {workDir}");

            // ─── Step 2: مسح Smali ───────────────────────────────────────────
            LogMessage("🔬 [2/3] تحليل ملفات Smali...");

            var smaliDirs = new List<string>();
            string mainSmali = Path.Combine(workDir, "smali");
            if (Directory.Exists(mainSmali)) smaliDirs.Add(mainSmali);
            for (int i = 2; i <= 20; i++)
            {
                string sd = Path.Combine(workDir, $"smali_classes{i}");
                if (Directory.Exists(sd)) smaliDirs.Add(sd);
            }

            int smaliCount = 0;
            foreach (string sd in smaliDirs)
                foreach (string file in Directory.EnumerateFiles(sd, "*.smali",
                    SearchOption.AllDirectories))
                {
                    smaliCount++;
                    ScanSmaliFile(file, result);
                }

            LogMessage($"  ✓ تم فحص {smaliCount} ملف Smali");

            // ─── Step 3: مسح الموارد والـ manifest والـ assets ────────────────
            LogMessage("📂 [3/3] تحليل Manifest + res + assets...");
            ScanManifestAndResources(workDir, result);

            return result;
        }

        /// <summary>
        /// تشغيل apktool بشكل متزامن (blocking) مع timeout.
        /// </summary>
        private void RunApktoolSync(string arguments)
        {
            try
            {
                using var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName               = "cmd.exe",
                        Arguments              = $"/c \"\"{_apktoolPath}\" {arguments}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError  = true,
                        UseShellExecute        = false,
                        CreateNoWindow         = true,
                        StandardOutputEncoding = Encoding.UTF8
                    }
                };
                proc.Start();
                proc.WaitForExit(120_000); // timeout 2 دقيقة
            }
            catch { /* نتجاهل ونتابع */ }
        }

        /// <summary>
        /// يمسح ملف Smali واحد ويستخرج الثوابت النصية الشبكية.
        ///
        /// بنية Smali للثوابت النصية:
        ///   const-string vX, "VALUE"
        ///   const-string/jumbo vX, "VALUE"
        ///
        /// كذلك نبحث عن Field values:
        ///   .field public static final BASE_URL:Ljava/lang/String; = "VALUE"
        /// </summary>
        private void ScanSmaliFile(string path, NetworkExtractResult result)
        {
            string content;
            try { content = File.ReadAllText(path, Encoding.UTF8); }
            catch { return; }

            // اسم الكلاس المصدر للـ context
            var classMatch = Regex.Match(content, @"^\.class\s+\S+\s+(\S+)",
                RegexOptions.Multiline);
            string ctx = classMatch.Success
                ? classMatch.Groups[1].Value.Split('/').LastOrDefault()
                    ?? Path.GetFileNameWithoutExtension(path)
                : Path.GetFileNameWithoutExtension(path);

            // ─────────────────────────────────────────────────────────────────
            // [A] const-string / const-string/jumbo
            // بنية bytecode الحقيقية: const-string vX, "VALUE"
            // ─────────────────────────────────────────────────────────────────
            foreach (Match m in Regex.Matches(
                content,
                @"const-string(?:/jumbo)?\s+\w[\w\d]*,\s+""([^""]{4,2048})""",
                RegexOptions.Multiline))
            {
                ClassifyNetworkString(m.Groups[1].Value, ctx, result);
            }

            // ─────────────────────────────────────────────────────────────────
            // [B] Static Field بقيمة مجردة
            // مثال: .field public static final BASE_URL:Ljava/lang/String; = "https://..."
            // أو: .field public static final C2_HOST:Ljava/lang/String; = "badguy.no-ip.org"
            // أو: .field public static final PORT:I = 4444 (إن أمكن)
            // ─────────────────────────────────────────────────────────────────
            foreach (Match m in Regex.Matches(
                content,
                @"\.field\s+(?:private|public|static|final|volatile|transient|protected)\s*.*?=\s*""([^""]{2,512})""",
                RegexOptions.Multiline))
            {
                string fieldVal = m.Groups[1].Value;
                
                // اصطياد أسماء الحقول المشبوهة
                var fieldNameMatch = Regex.Match(m.Value, @"\s([A-Za-z0-9_]+):(?:L|I)");
                string fieldName = fieldNameMatch.Success ? fieldNameMatch.Groups[1].Value : "Unknown_Field";
                
                if (Regex.IsMatch(fieldName, @"(IP|HOST|DOMAIN|SERVER|C2|PORT|KEY|PASS|SECRET|TOKEN|AUTH|URL)", RegexOptions.IgnoreCase))
                {
                    ClassifyNetworkString(fieldVal, $"[StaticField: {fieldName}] {ctx}", result);
                }
                else if (fieldVal.StartsWith("http") || Regex.IsMatch(fieldVal, @"^[0-9]{1,3}\.[0-9]{1,3}\."))
                {
                     ClassifyNetworkString(fieldVal, $"[StaticField] {ctx}", result);
                }
            }

            // ─────────────────────────────────────────────────────────────────
            // [C] WebSocket const-string
            // ─────────────────────────────────────────────────────────────────
            foreach (Match m in Regex.Matches(
                content,
                @"const-string(?:/jumbo)?\s+\w[\w\d]*,\s+""(wss?://[^""]{4,512})""",
                RegexOptions.Multiline))
            {
                result.AddWebSocket(m.Groups[1].Value, ctx);
            }

            // ─────────────────────────────────────────────────────────────────
            // [D] MAC Address strings (BLE / L2CAP / WiFi Direct)
            // تظهر في Smali كـ const-string: "AA:BB:CC:DD:EE:FF"
            // مرتبطة بـ android.net.L2capNetworkSpecifier (Android 36)
            //        وبـ BluetoothDevice.getAddress()
            // ─────────────────────────────────────────────────────────────────
            foreach (Match m in Regex.Matches(
                content,
                @"const-string(?:/jumbo)?\s+\w[\w\d]*,\s+""([0-9A-Fa-f]{2}(?::[0-9A-Fa-f]{2}){5})""",
                RegexOptions.Multiline))
            {
                result.AddMacAddress(m.Groups[1].Value.ToUpper(), ctx);
            }

            // ─────────────────────────────────────────────────────────────────
            // [E] android.net.L2capNetworkSpecifier (Android 36 — IPv6 over BLE)
            // في Smali تظهر class references هكذا:
            //   Landroid/net/L2capNetworkSpecifier;  (في توقيع الميثود أو sget/invoke)
            // هذا يعني التطبيق يبني شبكة BLE L2CAP بروتوكول مباشر
            // ─────────────────────────────────────────────────────────────────
            if (content.Contains("Landroid/net/L2capNetworkSpecifier"))
            {
                // نستخرج PSM إذا وُجد (قيمة int بعد setPsm أو const/16)
                // في apktool يُترجم: builder.setPsm(0x80) → const/16 vX, 0x80
                var psmMatches = Regex.Matches(content,
                    @"const(?:/4|/16)?\s+\w+,\s+(0x[89A-Fa-f][0-9A-Fa-f]|1[2-9][0-9]|2[0-4][0-9]|25[0-5])\b");
                string psmInfo = psmMatches.Count > 0
                    ? $"PSM={psmMatches[0].Groups[1].Value}"
                    : "PSM=dynamic";
                result.AddAndroid36Protocol(
                    $"L2CAP/BLE Network ({psmInfo})", ctx,
                    "IPv6-over-BLE via android.net.L2capNetworkSpecifier [Android 16 API36]");
            }

            // ─────────────────────────────────────────────────────────────────
            // [F] android.net.thread (Thread IoT — Android 36)
            // Thread هو بروتوكول mesh networking للـ IoT (Matter/Home automation)
            // أُضيف بشكل أصلي في Android 16
            // ─────────────────────────────────────────────────────────────────
            if (content.Contains("Landroid/net/thread/"))
            {
                // Thread network credentials تظهر كـ hex string (Network Key 16 bytes)
                var threadKeys = Regex.Matches(content,
                    @"const-string(?:/jumbo)?\s+\w+,\s+""([0-9a-fA-F]{32})""",
                    RegexOptions.Multiline);
                string credInfo = threadKeys.Count > 0
                    ? $"NetworkKey={threadKeys[0].Groups[1].Value[..8]}..."
                    : "";
                result.AddAndroid36Protocol(
                    $"Thread IoT Network {credInfo}".Trim(), ctx,
                    "Matter/Thread mesh via android.net.thread [Android 16 API36]");
            }

            // ─────────────────────────────────────────────────────────────────
            // [G] android.net.vcn.VcnManager (Virtual Carrier Network — Android 36)
            // شبكة افتراضية محمولة جديدة في Baklava/Android 16
            // ─────────────────────────────────────────────────────────────────
            if (content.Contains("Landroid/net/vcn/VcnManager") ||
                content.Contains("Landroid/net/vcn/VcnConfig"))
            {
                result.AddAndroid36Protocol(
                    "VCN (Virtual Carrier Network)", ctx,
                    "android.net.vcn.VcnManager [Android 16 API36]");
            }

            // ─────────────────────────────────────────────────────────────────
            // [H] android.net.DscpPolicy (DSCP QoS Traffic Marking — Android 36)
            // يُحدد أولوية حركة الشبكة على مستوى kernel
            // ─────────────────────────────────────────────────────────────────
            if (content.Contains("Landroid/net/DscpPolicy"))
            {
                result.AddAndroid36Protocol(
                    "DSCP QoS Traffic Policy", ctx,
                    "android.net.DscpPolicy [Android 16 API36]");
            }

            // ─────────────────────────────────────────────────────────────────
            // [I] android.net.NearbyManager / NearbyDevice (Nearby Connections)
            // من مجلد android/nearby — واجهة برمجية للاتصال القريب
            // ─────────────────────────────────────────────────────────────────
            if (content.Contains("Landroid/nearby/NearbyManager") ||
                content.Contains("Landroid/nearby/NearbyDevice"))
            {
                result.AddAndroid36Protocol(
                    "Nearby Connections API", ctx,
                    "android.nearby.NearbyManager [Android 16 API36]");
            }

            // ─────────────────────────────────────────────────────────────────
            // [J] java.util.prefs.Preferences — تخزين التفضيلات الهرمي
            // مصدر: android-36/java/util/prefs/Preferences.java
            //       android-36/java/util/prefs/AbstractPreferences.java
            // putByteArray/getByteArray → binary data مُشفَّرة بـ Base64
            // XmlSupport.java → export/import كـ XML هرمي
            // ─────────────────────────────────────────────────────────────────
            if (content.Contains("Ljava/util/prefs/Preferences;"))
            {
                bool hasUserRoot    = content.Contains("Preferences;->userRoot");
                bool hasSystemRoot  = content.Contains("Preferences;->systemRoot");
                bool hasBinaryPrefs = content.Contains("Preferences;->putByteArray") ||
                                      content.Contains("Preferences;->getByteArray");
                bool hasXmlExport   = content.Contains("Preferences;->exportNode") ||
                                      content.Contains("Preferences;->exportSubtree");

                var prefKeyMatches = Regex.Matches(content,
                    @"const-string(?:/jumbo)?\s+\w[\w\d]*,\s+""([a-zA-Z0-9_\.\-]{3,60})""",
                    RegexOptions.Multiline);

                var sb = new System.Text.StringBuilder("java.util.prefs.Preferences");
                sb.Append(hasUserRoot    ? " [user-root]"     : "");
                sb.Append(hasSystemRoot  ? " [system-root]"   : "");
                sb.Append(hasBinaryPrefs ? " [binary/Base64]" : "");
                sb.Append(hasXmlExport   ? " [XML-export]"    : "");

                var distinctKeys = prefKeyMatches.Cast<Match>()
                    .Select(m => m.Groups[1].Value).Distinct().Take(5).ToList();
                if (distinctKeys.Count > 0)
                    sb.Append($" | keys: {string.Join(", ", distinctKeys)}");

                result.AddAndroid36Protocol(sb.ToString(), ctx,
                    "Hierarchical key-value prefs — persisted as XML [android-36/java/util/prefs/Preferences.java]");
            }

            // ─────────────────────────────────────────────────────────────────
            // [K] java.util.logging.SocketHandler — إرسال logs عبر TCP socket
            // مصدر: android-36/java/util/logging/SocketHandler.java
            // Constructor: SocketHandler(String host, int port)
            // الخطورة: APK يرسل logs إلى خادم خارجي — vector تسريب بيانات
            // ─────────────────────────────────────────────────────────────────
            if (content.Contains("Ljava/util/logging/SocketHandler;"))
            {
                var hostMatches = Regex.Matches(content,
                    @"const-string(?:/jumbo)?\s+\w+,\s+""([a-zA-Z0-9][a-zA-Z0-9\.\-]{2,100}(?:\.[a-zA-Z]{2,}))""",
                    RegexOptions.Multiline);

                var portMatches = Regex.Matches(content,
                    @"const(?:/4|/16)?\s+\w+,\s+((?:0x[0-9A-Fa-f]{1,4}|\d{2,5}))\b",
                    RegexOptions.Multiline);

                bool foundEndpoint = false;
                foreach (Match hm in hostMatches)
                {
                    string socketHost = hm.Groups[1].Value;
                    string socketPort = "?";
                    if (portMatches.Count > 0)
                    {
                        string rawPort = portMatches[0].Groups[1].Value;
                        if (rawPort.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                            int.TryParse(rawPort[2..], System.Globalization.NumberStyles.HexNumber,
                                null, out int portHex) && portHex > 0 && portHex < 65536)
                            socketPort = portHex.ToString();
                        else if (int.TryParse(rawPort, out int portDec) &&
                                 portDec > 0 && portDec < 65536)
                            socketPort = portDec.ToString();
                    }
                    result.AddIpAddress($"{socketHost}:{socketPort}", ctx + " [logging/SocketHandler]");
                    foundEndpoint = true;
                }

                result.AddAndroid36Protocol(
                    "java.util.logging.SocketHandler — Remote TCP log sender" +
                    (foundEndpoint ? "" : " (endpoint not extracted)"),
                    ctx,
                    "Logs over TCP → potential exfil channel [android-36/java/util/logging/SocketHandler.java]");
            }

            // ─────────────────────────────────────────────────────────────────
            // [L] java.util.logging.XMLFormatter / FileHandler
            // مصدر: android-36/java/util/logging/XMLFormatter.java (9.3 KB)
            //       android-36/java/util/logging/FileHandler.java (29 KB)
            // XMLFormatter → XML structured logs → كشف paths/endpoints
            // FileHandler → مسارات ملفات logging على filesystem
            // ─────────────────────────────────────────────────────────────────
            if (content.Contains("Ljava/util/logging/XMLFormatter;") ||
                content.Contains("Ljava/util/logging/FileHandler;"))
            {
                var logFilePaths = Regex.Matches(content,
                    @"const-string(?:/jumbo)?\s+\w+,\s+""(/[a-zA-Z0-9_/\.\-]{4,200}\.(?:xml|log|txt))""",
                    RegexOptions.Multiline);

                foreach (Match fp in logFilePaths)
                    result.AddDeepLink("file://" + fp.Groups[1].Value,
                        ctx + " [XMLFormatter/FileHandler]");

                bool hasXmlFmt   = content.Contains("Ljava/util/logging/XMLFormatter;");
                bool hasFileFmt  = content.Contains("Ljava/util/logging/FileHandler;");

                result.AddAndroid36Protocol(
                    "java.util.logging" +
                    (hasXmlFmt  ? ".XMLFormatter" : "") +
                    (hasFileFmt ? "+FileHandler"  : ""),
                    ctx,
                    "Structured XML logging — may expose filesystem paths [android-36/java/util/logging/XMLFormatter.java]");
            }

            // ─────────────────────────────────────────────────────────────────
            // [M] java.util.zip — ضغط وتعبئة البيانات
            // مصدر: android-36/java/util/zip/CRC32C.java    ← جديد Android 36 فقط
            //       android-36/java/util/zip/GZIPInputStream.java
            //       android-36/java/util/zip/ZipFile.java (80 KB)
            // CRC32C: hardware-accelerated checksum لا يوجد قبل API 36
            // ─────────────────────────────────────────────────────────────────
            {
                bool hasZipGzip    = content.Contains("Ljava/util/zip/GZIPInputStream;") ||
                                     content.Contains("Ljava/util/zip/GZIPOutputStream;");
                bool hasZipCrc32c  = content.Contains("Ljava/util/zip/CRC32C;");
                bool hasZipFile    = content.Contains("Ljava/util/zip/ZipFile;");
                bool hasZipDeflater= content.Contains("Ljava/util/zip/Deflater;");

                if (hasZipGzip || hasZipCrc32c || hasZipFile || hasZipDeflater)
                {
                    var zipEntries = Regex.Matches(content,
                        @"const-string(?:/jumbo)?\s+\w+,\s+""([^""]{3,120}\.(?:json|xml|db|bin|dat|proto|pb|gz|zip))""",
                        RegexOptions.Multiline);

                    foreach (Match ze in zipEntries)
                        result.AddAndroid36Protocol(
                            $"Compressed asset: {ze.Groups[1].Value}", ctx,
                            hasZipGzip
                                ? "GZIP payload [android-36/java/util/zip/GZIPInputStream.java]"
                                : "ZIP entry [android-36/java/util/zip/ZipFile.java]");

                    if (hasZipCrc32c)
                        result.AddAndroid36Protocol(
                            "CRC32C checksum (Android 36 new API)", ctx,
                            "Hardware-accelerated CRC32C — API 36+ only [android-36/java/util/zip/CRC32C.java]");

                    if (hasZipDeflater && !hasZipGzip)
                        result.AddAndroid36Protocol(
                            "Raw Deflate compression (java.util.zip.Deflater)", ctx,
                            "Binary payload compression [android-36/java/util/zip/Deflater.java]");
                }
            }

            // =================================================================
            // [N] SOCKS / HTTP / FTP Proxy Detection
            // المصدر: android-36/sun/net/spi/DefaultProxySelector.java
            //         android-36/sun/net/SocksProxy.java
            //
            // DefaultProxySelector.props[][] يدعم:
            //   http  → http.proxyHost, http.proxyPort  (port 80  default)
            //   https → https.proxyHost, https.proxyPort (port 443 default)
            //   ftp   → ftp.proxyHost,  ftpProxyHost,   (port 80  default)
            //   socket→ socksProxyHost, socksProxyPort   (port 1080 default)
            //
            // nonProxyHosts bypass pattern → تحديد النطاقات التي يتجاوزها التطبيق
            // =================================================================
            {
                // SOCKS proxy (version 4 أو 5 — من SocksProxy.java + DefaultProxySelector)
                bool usesSocks = content.Contains("Lsun/net/SocksProxy;") ||
                                 content.Contains("Lsun/net/spi/DefaultProxySelector;") ||
                                 content.Contains("Ljava/net/Proxy$Type;") ||
                                 content.Contains("socksProxyHost") ||
                                 content.Contains("socksProxyVersion");

                if (usesSocks)
                {
                    // استخراج إصدار SOCKS (4 أو 5)
                    string socksVersion = "5"; // الافتراضي من DefaultProxySelector: version=5
                    if (content.Contains("socksProxyVersion"))
                    {
                        var verM = Regex.Match(content, @"const(?:/4|/16)?\s+\w+,\s+(4|5)\b");
                        if (verM.Success) socksVersion = verM.Groups[1].Value;
                    }

                    // استخراج SOCKS host إذا وُجد مباشرةً
                    var socksHostM = Regex.Match(content,
                        @"const-string(?:/jumbo)?\s+\w+,\s+""(socksProxyHost)""\s*[\s\S]{0,200}const-string(?:/jumbo)?\s+\w+,\s+""([a-zA-Z0-9][\w\.\-]{3,100})""",
                        RegexOptions.Multiline);
                    string socksHostInfo = socksHostM.Success
                        ? $" → Host={socksHostM.Groups[2].Value}"
                        : "";

                    result.AddSunNetFinding(
                        $"SOCKS v{socksVersion} Proxy{socksHostInfo}",
                        ctx,
                        $"sun.net.SocksProxy + DefaultProxySelector [android-36/sun/net/spi/DefaultProxySelector.java]",
                        SunNetRisk.High);
                }

                // HTTP Proxy explicit
                bool usesHttpProxy = content.Contains("http.proxyHost") ||
                                     content.Contains("https.proxyHost") ||
                                     content.Contains("Ljava/net/Proxy$Type;->HTTP");
                if (usesHttpProxy)
                {
                    // استخراج proxy host + port
                    var proxyHostM = Regex.Match(content,
                        @"const-string(?:/jumbo)?\s+\w+,\s+""(https?\.proxyHost)""\s*[\s\S]{0,300}const-string(?:/jumbo)?\s+\w+,\s+""([a-zA-Z0-9][\w\.\-]{3,100})""",
                        RegexOptions.Multiline);
                    string proxyInfo = proxyHostM.Success
                        ? $" → {proxyHostM.Groups[2].Value}"
                        : "";
                    result.AddSunNetFinding(
                        $"HTTP/HTTPS Proxy{proxyInfo}",
                        ctx,
                        "http.proxyHost/https.proxyHost via DefaultProxySelector [android-36/sun/net/spi/DefaultProxySelector.java]",
                        SunNetRisk.Medium);
                }

                // nonProxyHosts bypass — النطاقات المستثناة من الـ proxy (تخفي traffic)
                if (content.Contains("nonProxyHosts") || content.Contains("ftp.nonProxyHosts"))
                {
                    var bypassM = Regex.Match(content,
                        @"const-string(?:/jumbo)?\s+\w+,\s+""([^""]{4,200})""",
                        RegexOptions.Multiline);
                    string bypassList = bypassM.Success ? bypassM.Groups[1].Value : "";

                    result.AddSunNetFinding(
                        $"Proxy Bypass (nonProxyHosts){(bypassList.Length > 0 ? $": {bypassList[..Math.Min(60, bypassList.Length)]}" : "")}",
                        ctx,
                        "nonProxyHosts bypass — النطاقات التي يتجاوز فيها التطبيق proxy كشف [android-36/sun/net/spi/DefaultProxySelector.java L127]",
                        SunNetRisk.Medium);
                }
            }

            // =================================================================
            // [O] FTP / FTPS Detection
            // المصدر: android-36/sun/net/ftp/FtpClient.java (40 KB)
            //
            // FtpClient.connect(host, port) → port 21/990
            // FtpClient.login(user, password) → credentials مُضمَّنة
            // PASSIVE vs ACTIVE mode → firewall evasion
            // FtpLoginException → محاولات login تلقائية
            // =================================================================
            {
                bool usesFtp = content.Contains("Lsun/net/ftp/FtpClient;") ||
                               content.Contains("Lsun/net/ftp/FtpClientProvider;") ||
                               content.Contains("Ljava/net/URLConnection;") &&
                               (content.Contains("\"ftp://") || content.Contains("\"ftps://"));

                if (usesFtp)
                {
                    // استخراج FTP host
                    var ftpHostM = Regex.Match(content,
                        @"const-string(?:/jumbo)?\s+\w+,\s+""(ftps?://[a-zA-Z0-9][\w\.\-]{2,100}(?::\d{1,5})?)""",
                        RegexOptions.Multiline);
                    string ftpEndpoint = ftpHostM.Success ? ftpHostM.Groups[1].Value : "(dynamic)";

                    // هل يوجد login credentials؟
                    bool hasLogin = content.Contains("FtpClient;->login") ||
                                   content.Contains("\"anonymous\"");

                    // هل PASSIVE mode (firewall evasion)؟
                    bool hasPassive = content.Contains("PASSIVE") ||
                                     content.Contains("enterLocalPassiveMode") ||
                                     content.Contains("0x1027F"); // 66175 = EPSV

                    // هل FTPS/SSL؟
                    bool isFtps = ftpEndpoint.StartsWith("ftps://", StringComparison.OrdinalIgnoreCase) ||
                                  content.Contains("FtpClient;->enableSSL");

                    string detail = $"FTP{(isFtps ? "S" : "")} endpoint: {ftpEndpoint}" +
                                    (hasLogin ? " | ⚠ يحتوي login() — credentials محتملة" : "") +
                                    (hasPassive ? " | PASSIVE mode" : "");

                    result.AddSunNetFinding(
                        detail,
                        ctx,
                        $"sun.net.ftp.FtpClient [android-36/sun/net/ftp/FtpClient.java — 40 KB — FTP full impl]",
                        hasLogin ? SunNetRisk.Critical : SunNetRisk.High);
                }
            }

            // =================================================================
            // [P] Telnet Detection — بروتوكول غير آمن (Plaintext)
            // المصدر: android-36/sun/net/TelnetInputStream.java
            //         android-36/sun/net/TelnetOutputStream.java
            //         android-36/sun/net/TelnetProtocolException.java
            //
            // Telnet يرسل كل البيانات بنص واضح (لا تشفير)
            // الخطورة: CRITICAL — اعتراض بيانات كامل ممكن
            // المنفذ الافتراضي: TCP/23
            // =================================================================
            {
                bool usesTelnet = content.Contains("Lsun/net/TelnetInputStream;") ||
                                  content.Contains("Lsun/net/TelnetOutputStream;") ||
                                  content.Contains("Lsun/net/TelnetProtocolException;") ||
                                  content.Contains("TelnetInputStream") ||
                                  content.Contains("TelnetOutputStream");

                if (usesTelnet)
                {
                    // استخراج host إذا وُجد
                    var telnetHostM = Regex.Match(content,
                        @"const-string(?:/jumbo)?\s+\w+,\s+""([a-zA-Z0-9][\w\.\-]{3,100})""",
                        RegexOptions.Multiline);
                    string telnetHost = telnetHostM.Success ? telnetHostM.Groups[1].Value : "(dynamic)";

                    // البحث عن port 23
                    bool hasPort23 = content.Contains("0x17") || content.Contains(", 23") ||
                                     content.Contains("\"23\"");

                    result.AddSunNetFinding(
                        $"⛔ Telnet (Plaintext Protocol) → {telnetHost}{(hasPort23 ? ":23" : "")}",
                        ctx,
                        "sun.net.TelnetInputStream/TelnetOutputStream — NO ENCRYPTION, ALL DATA PLAINTEXT! [android-36/sun/net/TelnetInputStream.java]",
                        SunNetRisk.Critical);
                }
            }

            // =================================================================
            // [Q] NetworkClient Timeout Fingerprinting — C2 Beacon Indicator
            // المصدر: android-36/sun/net/NetworkClient.java
            //
            // إذا كان APK يضبط:
            //   sun.net.client.defaultReadTimeout    → مؤشر اتصال مُضبوط دقيقاً
            //   sun.net.client.defaultConnectTimeout → زمن انتظار C2 beacon محدد
            //   InetSocketAddress مع port غير معتاد → C2 port مشبوه
            //
            // NetworkClient.DEFAULT_READ_TIMEOUT = -1 (infinite) — خطير
            // NetworkClient.DEFAULT_CONNECT_TIMEOUT = -1 (infinite)
            // =================================================================
            {
                bool usesNetworkClient = content.Contains("Lsun/net/NetworkClient;") ||
                                         content.Contains("sun.net.client.defaultReadTimeout") ||
                                         content.Contains("sun.net.client.defaultConnectTimeout") ||
                                         content.Contains("defaultReadTimeout") ||
                                         content.Contains("defaultConnectTimeout");

                if (usesNetworkClient)
                {
                    // استخراج timeout values
                    string readTimeout = "∞ (infinite — خطير)";
                    string connectTimeout = "∞ (infinite)";

                    var readTM = Regex.Match(content,
                        @"defaultReadTimeout.*?const(?:/4|/16)?\s+\w+,\s+(\d+)", RegexOptions.Singleline);
                    if (readTM.Success) readTimeout = $"{readTM.Groups[1].Value} ms";

                    var connTM = Regex.Match(content,
                        @"defaultConnectTimeout.*?const(?:/4|/16)?\s+\w+,\s+(\d+)", RegexOptions.Singleline);
                    if (connTM.Success) connectTimeout = $"{connTM.Groups[1].Value} ms";

                    // البحث عن منافذ C2 كلاسيكية (4444, 1337, 8888, 9090, 31337)
                    var c2Ports = new[] { "0x115C", "0x539", "0x22B8", "0x2382", "0x7A69",
                                          "4444", "1337", "8888", "9090", "31337", "6666", "7777" };
                    var foundC2Ports = c2Ports.Where(p => content.Contains(p)).ToList();

                    string detail = $"ReadTimeout={readTimeout} | ConnectTimeout={connectTimeout}";
                    if (foundC2Ports.Count > 0)
                        detail += $" | ⚠ C2 Ports: {string.Join(", ", foundC2Ports)}";

                    result.AddSunNetFinding(
                        $"NetworkClient Timeout Config → {detail}",
                        ctx,
                        "sun.net.NetworkClient (TCP base class) [android-36/sun/net/NetworkClient.java] — Timeout=∞ يعني C2 persistent connection",
                        foundC2Ports.Count > 0 ? SunNetRisk.Critical : SunNetRisk.Medium);
                }

                // InetSocketAddress مع منافذ C2 مشبوهة (حتى بدون NetworkClient)
                var inetPorts = Regex.Matches(content,
                    @"Ljava/net/InetSocketAddress;-><init>[\s\S]{0,150}const(?:/4|/16)?\s+\w+,\s+(0x[0-9A-Fa-f]+|\d+)\b",
                    RegexOptions.Multiline);
                foreach (Match pm in inetPorts)
                {
                    string rawPv = pm.Groups[1].Value;
                    int portVal = rawPv.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                        ? Convert.ToInt32(rawPv, 16)
                        : int.TryParse(rawPv, out int pv) ? pv : -1;

                    if (portVal <= 0 || portVal > 65535) continue;

                    // منافذ مشبوهة (C2, RAT, shell)
                    bool suspPort = portVal is 4444 or 1337 or 8888 or 9090 or 31337
                                             or 6666 or 7777 or 5555 or 2222 or 12345;
                    if (suspPort)
                        result.AddSunNetFinding(
                            $"InetSocketAddress — Port {portVal} (C2/Shell suspect)",
                            ctx,
                            $"Ljava/net/InetSocketAddress port={portVal} — منفذ معروف لأدوات RAT/C2/Shell [android-36/sun/net/NetworkClient.java]",
                            SunNetRisk.Critical);
                }
            }

            // =================================================================
            // [R] HTTP MessageHeader Analysis — Custom Headers Detection
            // المصدر: android-36/sun/net/www/MessageHeader.java (16 KB)
            //
            // يكشف HTTP headers مُضمَّنة في APK:
            //   Authorization: → مفاتيح مضمنة
            //   X-API-Key:      → API keys
            //   X-Auth-Token:   → tokens
            //   User-Agent:     → هوية شبكية مخصصة
            //   X-Forwarded-For:→ تلاعب بعنوان IP
            //   Proxy-Authorization: → proxy credentials
            // =================================================================
            {
                // Headers الحساسة من MessageHeader.java
                var sensitiveHeaders = new (string Pattern, string Label, SunNetRisk Risk)[]
                {
                    (@"(?i)Authorization:\s*(?:Bearer|Basic|Digest|Token)\s+([A-Za-z0-9+/=._-]{8,256})",
                     "Authorization Header (token/credential)", SunNetRisk.Critical),
                    (@"(?i)X-API-Key:\s*([A-Za-z0-9+/=._\-]{8,256})",
                     "X-API-Key Header", SunNetRisk.Critical),
                    (@"(?i)X-Auth-Token:\s*([A-Za-z0-9+/=._\-]{8,256})",
                     "X-Auth-Token Header", SunNetRisk.Critical),
                    (@"(?i)Proxy-Authorization:\s*([A-Za-z0-9+/=._\-]{8,256})",
                     "Proxy-Authorization Header", SunNetRisk.High),
                    (@"(?i)X-Forwarded-For:\s*([\d\.]+)",
                     "X-Forwarded-For (IP spoofing)", SunNetRisk.High),
                    (@"(?i)User-Agent:\s*([^\r\n""]{8,128})",
                     "Custom User-Agent", SunNetRisk.Medium),
                    (@"(?i)Content-Type:\s*application/octet-stream",
                     "Binary stream Content-Type", SunNetRisk.Medium),
                };

                // البحث في const-string values
                var constStrings = Regex.Matches(content,
                    @"const-string(?:/jumbo)?\s+\w[\w\d]*,\s+""([^""]{4,512})""",
                    RegexOptions.Multiline);

                foreach (Match cs in constStrings)
                {
                    string sv = cs.Groups[1].Value;
                    foreach (var (pat, label, risk) in sensitiveHeaders)
                    {
                        var hm = Regex.Match(sv, pat);
                        if (!hm.Success) continue;

                        string extracted = hm.Groups.Count > 1 && hm.Groups[1].Length > 0
                            ? hm.Groups[1].Value[..Math.Min(hm.Groups[1].Value.Length, 40)]
                            : sv[..Math.Min(sv.Length, 40)];

                        result.AddSunNetFinding(
                            $"{label}: {extracted}{(extracted.Length >= 40 ? "..." : "")}",
                            ctx,
                            $"sun.net.www.MessageHeader Custom HTTP Header [android-36/sun/net/www/MessageHeader.java]",
                            risk);
                        break; // مطابقة واحدة كافية لكل string
                    }
                }

                // URL parameters مع credentials مُعمَّاة (مثال: https://user:pass@host)
                foreach (Match m in Regex.Matches(content,
                    @"const-string(?:/jumbo)?\s+\w[\w\d]*,\s+""https?://([^:@\s""]{2,60}):([^@\s""]{4,60})@([^/\s""]{4,100})",
                    RegexOptions.Multiline))
                {
                    result.AddSunNetFinding(
                        $"URL Embedded Credentials: {m.Groups[1].Value}:***@{m.Groups[3].Value}",
                        ctx,
                        "Credentials embedded in URL — plaintext HTTP auth [OWASP A3:Sensitive Data Exposure]",
                        SunNetRisk.Critical);
                }
            }

            // =================================================================
            // [S] Enhanced IPv4/IPv6 Validation — IPAddressUtil Logic
            // المصدر: android-36/sun/net/util/IPAddressUtil.java
            //
            // Android-changed: يشترط 4 octets كاملة (لا يقبل partial IPv4)
            // يكتشف:
            //   - IPv4-Mapped IPv6: ::ffff:x.x.x.x (tunneling)
            //   - Loopback/link-local/multicast
            //   - Hex-encoded IPs (C2 obfuscation: 0xc0a80001 = 192.168.0.1)
            //   - Octal-encoded IPs (0300.0250.0.1)
            //   - Private-range IPs in non-local context
            // =================================================================
            {
                // كشف Hex-encoded IPs (C2 obfuscation)
                // مثال: 0xC0A80001 = 192.168.0.1
                foreach (Match m in Regex.Matches(content,
                    @"const(?:-string(?:/jumbo)?)?\s+\w[\w\d]*,\s+(0x[0-9A-Fa-f]{8})\b",
                    RegexOptions.Multiline))
                {
                    string hexVal = m.Groups[1].Value;
                    if (uint.TryParse(hexVal[2..], System.Globalization.NumberStyles.HexNumber,
                        null, out uint ipInt))
                    {
                        byte b1 = (byte)(ipInt >> 24), b2 = (byte)(ipInt >> 16),
                             b3 = (byte)(ipInt >> 8),  b4 = (byte)ipInt;
                        // تحقق بسيط: أرقام IPv4 منطقية
                        if (b1 <= 255 && b2 <= 255 && b3 <= 255 && b4 <= 255 &&
                            ipInt > 0x01000000) // > 1.0.0.0
                        {
                            string decoded = $"{b1}.{b2}.{b3}.{b4}";
                            result.AddSunNetFinding(
                                $"Hex-Encoded IP: {hexVal} → {decoded}",
                                ctx,
                                $"Obfuscated IP address — C2 detection technique [android-36/sun/net/util/IPAddressUtil.java]",
                                SunNetRisk.High);
                        }
                    }
                }

                // كشف IPv4-Mapped IPv6 (::ffff:x.x.x.x) — tunneling technique
                foreach (Match m in Regex.Matches(content,
                    @"const-string(?:/jumbo)?\s+\w[\w\d]*,\s+""::ffff:(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})""",
                    RegexOptions.Multiline))
                {
                    result.AddSunNetFinding(
                        $"IPv4-Mapped IPv6: ::ffff:{m.Groups[1].Value}",
                        ctx,
                        "IPv4-mapped IPv6 address — potential IPv4/IPv6 tunneling [android-36/sun/net/util/IPAddressUtil.java — convertFromIPv4MappedAddress()]",
                        SunNetRisk.Medium);
                }

                // كشف multicast IPs (224.x.x.x - 239.x.x.x) في Smali — مجموعات خفية
                foreach (Match m in Regex.Matches(content,
                    @"const-string(?:/jumbo)?\s+\w[\w\d]*,\s+""(2(?:2[4-9]|3\d)\.\d{1,3}\.\d{1,3}\.\d{1,3})""",
                    RegexOptions.Multiline))
                {
                    result.AddSunNetFinding(
                        $"Multicast IP: {m.Groups[1].Value}",
                        ctx,
                        "Multicast address (224-239.x.x.x) — one-to-many communication channel [android-36/sun/net/util/IPAddressUtil.java]",
                        SunNetRisk.High);
                }
            }
        }

        /// <summary>
        /// يصنّف نص مستخرج ويضيفه للقائمة المناسبة.
        /// </summary>
        private static void ClassifyNetworkString(
            string value, string ctx, NetworkExtractResult result)
        {
            if (string.IsNullOrWhiteSpace(value)) return;

            // ── HTTP / HTTPS URL ─────────────────────────────────────────────
            if (Regex.IsMatch(value, @"^https?://", RegexOptions.IgnoreCase))
            {
                result.AddUrl(value, ctx);

                // API endpoint
                if (Regex.IsMatch(value,
                    @"/api[/\?]|/v\d+[/\?]|/graphql|/rest/|/rpc",
                    RegexOptions.IgnoreCase))
                    result.AddApiEndpoint(value, ctx);

                // Firebase Realtime Database
                if (Regex.IsMatch(value, @"\.firebaseio\.com", RegexOptions.IgnoreCase))
                    result.AddFirebase(value, "Firebase RTDB");

                // Firebase Hosting / Web App
                if (Regex.IsMatch(value,
                    @"\.firebaseapp\.com|\.web\.app", RegexOptions.IgnoreCase))
                    result.AddFirebase(value, "Firebase Hosting");

                // Google Cloud Storage / APIs
                if (Regex.IsMatch(value,
                    @"storage\.googleapis\.com|\.googleapis\.com",
                    RegexOptions.IgnoreCase))
                    result.AddCloudService(value, "Google Cloud");

                // AWS
                if (Regex.IsMatch(value,
                    @"\.amazonaws\.com|\.s3\.|\.execute-api\.",
                    RegexOptions.IgnoreCase))
                    result.AddCloudService(value, "AWS");

                // Azure
                if (Regex.IsMatch(value,
                    @"\.azure\.com|\.azurewebsites\.net",
                    RegexOptions.IgnoreCase))
                    result.AddCloudService(value, "Azure");

                return;
            }

            // ── WebSocket (ws / wss) ─────────────────────────────────────────
            if (Regex.IsMatch(value, @"^wss?://", RegexOptions.IgnoreCase))
            {
                result.AddWebSocket(value, ctx);
                return;
            }

            // ── Raw IP (مع بورت اختياري) ─────────────────────────────────────
            if (Regex.IsMatch(value,
                @"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}(:\d{1,5})?$"))
            {
                result.AddIpAddress(value, ctx);
                return;
            }

            // ── hostname:port ─────────────────────────────────────────────────
            if (Regex.IsMatch(value,
                @"^[a-zA-Z0-9][a-zA-Z0-9\-\.]+\.[a-zA-Z]{2,}:\d{2,5}$"))
            {
                result.AddIpAddress(value, ctx + " [host:port]");
                return;
            }

            // ── Firebase API Key  AIza... ────────────────────────────────────
            if (Regex.IsMatch(value, @"^AIza[0-9A-Za-z\-_]{35}$"))
            {
                result.AddSecret(value, "Google/Firebase API Key");
                return;
            }

            // ── JWT Token (eyJ... format) ─────────────────────────────────────
            if (Regex.IsMatch(value,
                @"^eyJ[A-Za-z0-9\-_]+\.[A-Za-z0-9\-_]+\.[A-Za-z0-9\-_]+$"))
            {
                result.AddSecret(value, "JWT Token");
                return;
            }

            // ── Bearer / Basic / Token header ────────────────────────────────
            if (Regex.IsMatch(value,
                @"^(Bearer|Basic|Token)\s+[A-Za-z0-9+/=_\-]{10,}",
                RegexOptions.IgnoreCase))
            {
                result.AddSecret(value, "Auth Header Value");
                return;
            }

            // ── FCM / GCM Server Key ─────────────────────────────────────────
            if (Regex.IsMatch(value, @"^AAAA[A-Za-z0-9_\-]{7}:[A-Za-z0-9_\-]{100,}"))
            {
                result.AddSecret(value, "FCM Server Key");
                return;
            }

            // ── Dynamic DNS (DDNS) / Custom Generic Domains ──────────────────
            if (Regex.IsMatch(value, @"(?i)^[a-z0-9\-]+\.(no-ip\.(biz|info|org)|ddns\.net|duckdns\.org|hopto\.org|bounceme\.net|sytes\.net|serve(?:blog|ftp|game|http|mp3|pics|quake)\.com)$"))
            {
                result.AddIpAddress(value, ctx + " [DDNS Domain]");
                return;
            }
            
            // ── Telegram Bot Token ───────────────────────────────────────────
            var tgMatch = Regex.Match(value, @"([0-9]{8,10}:[a-zA-Z0-9_\-]{35})");
            if (tgMatch.Success)
            {
                result.AddTokensAndWebhooks(tgMatch.Groups[1].Value, ctx + " [Telegram Bot Token]");
                // We don't return here just in case the string contains other things, but usually it's standalone.
            }

            // ── Discord Webhook ──────────────────────────────────────────────
            var dcMatch = Regex.Match(value, @"discord(?:app)?\.com/api/webhooks/([0-9]{17,19}/[a-zA-Z0-9_\-]{60,})");
            if (dcMatch.Success)
            {
                result.AddTokensAndWebhooks(dcMatch.Groups[1].Value, ctx + " [Discord Webhook]");
            }

            // ── Cryptocurrency Wallets (BTC / ETH / TRON) ─────────────────────
            // These regexes check if the entire string represents a crypto address
            // BTC (P2PKH, P2SH, Bech32)
            if (Regex.IsMatch(value, @"^(?:[13][a-km-zA-HJ-NP-Z1-9]{25,34}|bc1[a-zA-HJ-NP-Z0-9]{39,59})$"))
            {
                result.AddCryptoWallet(value, ctx + " [Bitcoin (BTC) Address]");
                return;
            }
            // ETH (ERC-20 / BSC)
            if (Regex.IsMatch(value, @"^0x[a-fA-F0-9]{40}$"))
            {
                result.AddCryptoWallet(value, ctx + " [Ethereum/BSC (ETH) Address]");
                return;
            }
            // TRON (USDT TRC-20)
            if (Regex.IsMatch(value, @"^T[A-Za-z1-9]{33}$"))
            {
                result.AddCryptoWallet(value, ctx + " [TRON/USDT (TRX) Address]");
                return;
            }
            
            // ── Standalone Base64 / Hex Strings (Potential Keys) ─────────────
            if (value.Length >= 16 && value.Length <= 64)
            {
                // Hex
                if (Regex.IsMatch(value, @"^[a-fA-F0-9]{32,64}$"))
                {
                    result.AddSecret(value, "Potential Hex Key / Hash");
                    return;
                }
                // Base64 (Basic heuristic)
                if (Regex.IsMatch(value, @"^[A-Za-z0-9+/]+={0,2}$"))
                {
                    // Filter out obvious not-random English words occasionally caught 
                    if (!Regex.IsMatch(value, @"(?i)^[a-z]+$"))
                    {
                         result.AddSecret(value, "Potential Base64 Key");
                         return;
                    }
                }
            }
            // ── Ports (standalone integer strings) ─────────────
            if (Regex.IsMatch(value, @"^\d{2,5}$"))
            {
                if (int.TryParse(value, out int p) && p > 100 && p < 65536)
                {
                    result.AddIpAddress($"Port: {value}", ctx + " [Potential Port]");
                }
            }

            // ── Deep-Link / Custom URI scheme ────────────────────────────────
            if (Regex.IsMatch(value, @"^[a-z][a-z0-9+\-.]+://",
                RegexOptions.IgnoreCase)
                && !value.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                && !value.StartsWith("ws", StringComparison.OrdinalIgnoreCase))
            {
                result.AddDeepLink(value, ctx);
            }

            // ── Pastebin / Hastebin / Ghostbin (قناة C2 خفية) ───────────────
            // يستخدم المهاجمون Pastebin لتخزين تعليمات C2 ديناميكياً
            if (Regex.IsMatch(value,
                @"(?i)(pastebin\.com|hastebin\.com|ghostbin\.com|paste\.ee|dpaste\.org)/",
                RegexOptions.IgnoreCase))
            {
                result.AddUrl(value, ctx + " [Pastebin C2 — قناة تحكم مشبوهة]");
            }

            // ── عنوان IPv6 خام ───────────────────────────────────────────────
            // مهم لـ Android 36 الذي يدعم IPv6 over BLE (L2CAP)
            if (Regex.IsMatch(value,
                @"^\[?([0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}\]?(:\d{1,5})?$"))
            {
                result.AddIpAddress(value, ctx + " [IPv6 — قد يعمل عبر L2CAP BLE في Android 36]");
            }
        }

        /// <summary>
        /// يمسح AndroidManifest.xml وكل ملفات XML في res/ وملفات الـ assets.
        /// </summary>
        private void ScanManifestAndResources(
            string workDir, NetworkExtractResult result)
        {
            // AndroidManifest.xml
            string manifestPath = Path.Combine(workDir, "AndroidManifest.xml");
            if (File.Exists(manifestPath))
                ScanXmlFile(manifestPath, "AndroidManifest", result);

            // XML في res/
            string resDir = Path.Combine(workDir, "res");
            if (Directory.Exists(resDir))
                foreach (var xml in Directory.EnumerateFiles(
                    resDir, "*.xml", SearchOption.AllDirectories))
                    ScanXmlFile(xml, "res/" + Path.GetFileName(xml), result);

            // assets (JSON / properties / txt / conf)
            string assetsDir = Path.Combine(workDir, "assets");
            if (Directory.Exists(assetsDir))
                foreach (var ext in new[] { "*.json", "*.properties",
                    "*.txt", "*.cfg", "*.conf", "*.ini" })
                foreach (var file in Directory.EnumerateFiles(
                    assetsDir, ext, SearchOption.AllDirectories))
                    ScanTextFile(file, "assets/" + Path.GetFileName(file), result);

            // google-services.json
            foreach (var f in Directory.EnumerateFiles(
                workDir, "google-services.json", SearchOption.AllDirectories))
                ScanTextFile(f, "google-services.json", result);
        }

        private void ScanXmlFile(
            string path, string context, NetworkExtractResult result)
        {
            string content;
            try { content = File.ReadAllText(path, Encoding.UTF8); }
            catch { return; }

            // URLs داخل قيم XML
            foreach (Match m in Regex.Matches(
                content,
                @"(?<=""|>)(https?://[^""<>\s]{4,512})(?=""|<)",
                RegexOptions.IgnoreCase))
                ClassifyNetworkString(m.Value, context, result);

            // intent-filter schemes (deep links في Manifest)
            var schemes = Regex.Matches(content,
                @"android:scheme=""([^""]+)""", RegexOptions.IgnoreCase);
            var hosts = Regex.Matches(content,
                @"android:host=""([^""]+)""", RegexOptions.IgnoreCase);

            for (int i = 0; i < schemes.Count; i++)
            {
                string scheme = schemes[i].Groups[1].Value;
                string host   = i < hosts.Count ? hosts[i].Groups[1].Value : "";
                string dl = string.IsNullOrEmpty(host)
                    ? scheme + "://"
                    : $"{scheme}://{host}";
                result.AddDeepLink(dl, "Manifest intent-filter");
            }
        }

        private void ScanTextFile(
            string path, string context, NetworkExtractResult result)
        {
            string content;
            try { content = File.ReadAllText(path, Encoding.UTF8); }
            catch { return; }

            foreach (Match m in Regex.Matches(
                content,
                @"https?://[^""'\s<>]{4,512}",
                RegexOptions.IgnoreCase))
                ClassifyNetworkString(m.Value, context, result);

            // Firebase project_id
            var pid = Regex.Match(content,
                @"""project_id""\s*:\s*""\s*([^""]+)\s*""");
            if (pid.Success)
                result.AddFirebase($"project_id: {pid.Groups[1].Value}", context);

            // OAuth2 client_id
            var cid = Regex.Match(content,
                @"""client_id""\s*:\s*""\s*([^""]+\.apps\.googleusercontent\.com)\s*""");
            if (cid.Success)
                result.AddSecret(cid.Groups[1].Value, "OAuth2 client_id");
        }

        /// <summary>
        /// عرض النتائج في اللوج بتنسيق واضح ومنظّم.
        /// </summary>
        private void DisplayNetworkResults(NetworkExtractResult r)
        {
            Dispatcher.Invoke(() =>
            {
                LogMessage("\n╔══════════════════════════════════════════════════╗");
                LogMessage("║   📊 نتائج استخراج معلومات الاتصال                ║");
                LogMessage("║   Aleppo University Research — Android 36 (API 36)║");
                LogMessage("╚══════════════════════════════════════════════════╝");
                LogMessage($"   APK: {Path.GetFileNameWithoutExtension(_sourceApkPath ?? "")}");

                // ── ملخص سريع بالأرقام ─────────────────────────────────────
                int total = r.Urls.Count + r.ApiEndpoints.Count + r.WebSockets.Count
                          + r.IpAddresses.Count + r.Firebase.Count + r.CloudServices.Count
                          + r.DeepLinks.Count + r.Secrets.Count + r.MacAddresses.Count
                          + r.Android36Protocols.Count + r.TokensAndWebhooks.Count
                          + r.CryptoWallets.Count + r.SunNetFindings.Count;

                string riskIcon = total switch
                {
                    >= 20 => "🔴",
                    >= 10 => "🟠",
                    >= 3  => "🟡",
                    > 0   => "🟢",
                    _     => "✅"
                };
                LogMessage($"\n{riskIcon} إجمالي العناصر المكتشفة: {total}");
                if (r.Urls.Count            > 0) LogMessage($"   🌐 URLs           : {r.Urls.Count}");
                if (r.ApiEndpoints.Count    > 0) LogMessage($"   📡 API Endpoints  : {r.ApiEndpoints.Count}");
                if (r.WebSockets.Count      > 0) LogMessage($"   🔌 WebSockets     : {r.WebSockets.Count}");
                if (r.IpAddresses.Count     > 0) LogMessage($"   🖥️  IPs / Hosts    : {r.IpAddresses.Count}");
                if (r.Firebase.Count        > 0) LogMessage($"   🔥 Firebase       : {r.Firebase.Count}");
                if (r.CloudServices.Count   > 0) LogMessage($"   ☁️  Cloud Services : {r.CloudServices.Count}");
                if (r.TokensAndWebhooks.Count > 0) LogMessage($"   🤖 Bots/Webhooks  : {r.TokensAndWebhooks.Count}");
                if (r.CryptoWallets.Count   > 0) LogMessage($"   💰 Crypto Wallets : {r.CryptoWallets.Count}");
                if (r.MacAddresses.Count    > 0) LogMessage($"   📶 MAC Addresses  : {r.MacAddresses.Count}");
                if (r.DeepLinks.Count       > 0) LogMessage($"   🔗 Deep Links     : {r.DeepLinks.Count}");
                if (r.Secrets.Count         > 0) LogMessage($"   🔑 Secrets/Keys   : {r.Secrets.Count}");
                if (r.Android36Protocols.Count > 0) LogMessage($"   ⚡ Android 36 APIs: {r.Android36Protocols.Count}");
                // ── sun/net محركات جديدة ──────────────────────────────────
                if (r.SunNetFindings.Count  > 0)
                {
                    int critical = r.SunNetFindings.Count(f => f.Risk == SunNetRisk.Critical);
                    int high     = r.SunNetFindings.Count(f => f.Risk == SunNetRisk.High);
                    int medium   = r.SunNetFindings.Count(f => f.Risk == SunNetRisk.Medium);
                    LogMessage($"   🔬 sun/net Findings: {r.SunNetFindings.Count}" +
                               $"  (🔴{critical} 🟠{high} 🟡{medium})");
                }
                LogMessage("   ─────────────────────────────────────────────────");

                void Section(string icon, string title,
                    List<(string val, string ctx)> items)
                {
                    if (items.Count == 0) return;
                    LogMessage($"\n{icon} {title} ({items.Count}):");
                    foreach (var (val, ctx) in items)
                        LogMessage($"  • {val}\n    └─ [{ctx}]");
                }

                Section("🌐", "URLs",                        r.Urls);
                Section("📡", "API Endpoints",               r.ApiEndpoints);
                Section("🔌", "WebSockets",                   r.WebSockets);
                Section("🖥️",  "IP / Hosts",                  r.IpAddresses);
                Section("🔥", "Firebase",                     r.Firebase);
                Section("☁️",  "Cloud Services",              r.CloudServices);
                Section("🤖", "Bots & Webhooks",              r.TokensAndWebhooks);
                Section("💰", "Crypto Wallets",               r.CryptoWallets);
                Section("🔗", "Deep Links",                   r.DeepLinks);
                Section("📶", "MAC Addresses (BLE/WiFi)", r.MacAddresses);

                // Android 36 Protocols — نوع 3-tuple (val, ctx, detail)
                if (r.Android36Protocols.Count > 0)
                {
                    LogMessage($"\n🤖 Android 36 Protocols (API36) ({r.Android36Protocols.Count}):");
                    foreach (var (val, ctx, detail) in r.Android36Protocols)
                    {
                        LogMessage($"  • {val}");
                        LogMessage($"    └─ [{ctx}]");
                        LogMessage($"    ℹ {detail}");
                    }
                }

                // Secrets — نُخفي جزءاً من القيمة
                if (r.Secrets.Count > 0)
                {
                    LogMessage($"\n🔑 Secrets / Tokens / Keys ({r.Secrets.Count}):");
                    foreach (var (val, ctx) in r.Secrets)
                    {
                        string display = val.Length > 30
                            ? val[..16] + "..." + val[^6..]
                            : val;
                        LogMessage($"  • [{ctx}]  {display}");
                    }
                }

                // ── قسم sun/net Findings الاحترافي الجديد ──────────────────────
                if (r.SunNetFindings.Count > 0)
                {
                    LogMessage($"\n🔬 sun/net Security Findings — android-36/sun/net/ ({r.SunNetFindings.Count}):");
                    LogMessage("   المصدر: NetworkClient.java | DefaultProxySelector.java | FtpClient.java");
                    LogMessage("           TelnetInputStream.java | MessageHeader.java | IPAddressUtil.java");
                    LogMessage("");

                    // تجميع بالخطورة
                    var criticalFindings = r.SunNetFindings.Where(f => f.Risk == SunNetRisk.Critical).ToList();
                    var highFindings     = r.SunNetFindings.Where(f => f.Risk == SunNetRisk.High).ToList();
                    var mediumFindings   = r.SunNetFindings.Where(f => f.Risk == SunNetRisk.Medium).ToList();

                    if (criticalFindings.Count > 0)
                    {
                        LogMessage($"  🔴 CRITICAL ({criticalFindings.Count}):");
                        foreach (var f in criticalFindings)
                        {
                            LogMessage($"    ⛔ {f.Value}");
                            LogMessage($"       [{f.Context}]");
                            LogMessage($"       ℹ {f.Detail}");
                        }
                    }
                    if (highFindings.Count > 0)
                    {
                        LogMessage($"  🟠 HIGH ({highFindings.Count}):");
                        foreach (var f in highFindings)
                        {
                            LogMessage($"    ⚠ {f.Value}");
                            LogMessage($"       [{f.Context}]");
                            LogMessage($"       ℹ {f.Detail}");
                        }
                    }
                    if (mediumFindings.Count > 0)
                    {
                        LogMessage($"  🟡 MEDIUM ({mediumFindings.Count}):");
                        foreach (var f in mediumFindings)
                        {
                            LogMessage($"    • {f.Value}");
                            LogMessage($"       [{f.Context}]");
                        }
                    }
                }

                LogMessage("\n──────────────────────────────────────────");
                LogMessage($"✅ الإجمالي: {total} عنصر مُستخرج");
                LogMessage("──────────────────────────────────────────\n");

                SaveNetworkReport(r);
            });
        }

        /// <summary>
        /// يحفظ تقرير النتائج كملف نصي في مجلد المخرجات.
        /// </summary>
        private void SaveNetworkReport(NetworkExtractResult r)
        {
            try
            {
                string apkName    = Path.GetFileNameWithoutExtension(
                    _sourceApkPath ?? "unknown");
                string timestamp  = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string reportPath = Path.Combine(_outputDir,
                    $"network_report_{apkName}_{timestamp}.txt");

                var sb = new StringBuilder();
                sb.AppendLine("════════════════════════════════════════════════════════════");
                sb.AppendLine(" Network Info Extractor Report");
                sb.AppendLine(" Aleppo University Research Project — Android 36 (API 36)");
                sb.AppendLine($" Date  : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($" APK   : {_sourceApkPath}");
                sb.AppendLine("════════════════════════════════════════════════════════════");
                sb.AppendLine();

                // ملخص إحصائي
                sb.AppendLine("[Summary]");
                sb.AppendLine($"  URLs              : {r.Urls.Count}");
                sb.AppendLine($"  API Endpoints     : {r.ApiEndpoints.Count}");
                sb.AppendLine($"  WebSockets        : {r.WebSockets.Count}");
                sb.AppendLine($"  IP Addresses      : {r.IpAddresses.Count}");
                sb.AppendLine($"  Firebase          : {r.Firebase.Count}");
                sb.AppendLine($"  Cloud Services    : {r.CloudServices.Count}");
                sb.AppendLine($"  Bots & Webhooks   : {r.TokensAndWebhooks.Count}");
                sb.AppendLine($"  Crypto Wallets    : {r.CryptoWallets.Count}");
                sb.AppendLine($"  Deep Links        : {r.DeepLinks.Count}");
                sb.AppendLine($"  Secrets/Keys      : {r.Secrets.Count}");
                sb.AppendLine($"  MAC Addresses     : {r.MacAddresses.Count}");
                sb.AppendLine($"  Android 36 APIs   : {r.Android36Protocols.Count}");
                sb.AppendLine();

                void Sec(string title, List<(string val, string ctx)> items)
                {
                    if (items.Count == 0) return;
                    sb.AppendLine($"[{title}] ({items.Count})");
                    foreach (var (v, c) in items)
                        sb.AppendLine($"  {v}  |  {c}");
                    sb.AppendLine();
                }

                Sec("URLs",                      r.Urls);
                Sec("API Endpoints",             r.ApiEndpoints);
                Sec("WebSockets",                r.WebSockets);
                Sec("IP Addresses",              r.IpAddresses);
                Sec("Firebase",                  r.Firebase);
                Sec("Cloud Services",            r.CloudServices);
                Sec("Bots & Webhooks",           r.TokensAndWebhooks);
                Sec("Crypto Wallets",            r.CryptoWallets);
                Sec("Deep Links",                r.DeepLinks);
                Sec("Secrets/Keys",              r.Secrets);
                Sec("MAC Addresses",             r.MacAddresses);

                // Android 36 Protocols — 3-tuple (val, ctx, detail)
                if (r.Android36Protocols.Count > 0)
                {
                    sb.AppendLine($"[Android 36 Protocols] ({r.Android36Protocols.Count})");
                    foreach (var (v, c, d) in r.Android36Protocols)
                        sb.AppendLine($"  {v}  |  {c}  |  {d}");
                    sb.AppendLine();
                }

                File.WriteAllText(reportPath, sb.ToString(), Encoding.UTF8);
                LogMessage($"💾 التقرير محفوظ: {Path.GetFileName(reportPath)}");
            }
            catch (Exception ex)
            {
                LogMessage($"  ⚠ فشل حفظ التقرير: {ex.Message}");
            }
        }

        #region Android 36 API Profiler — Real Research Feature

        // ══════════════════════════════════════════════════════════════════════
        // Android 36 API Compatibility Profiler
        // ══════════════════════════════════════════════════════════════════════
        // يعتمد هذا المحلل على:
        //   1. dexdump.exe من build-tools/36.1.0 لاستخراج كل class/method references
        //   2. aapt2.exe من build-tools/36.1.0 لتفريغ بيانات APK الجديدة (badging كامل)
        //   3. قراءة شجرة android-36/android/ لبناء قاعدة بيانات APIs الجديدة في Android 16
        //   4. مطابقة حقيقية بين APIs المستخدمة في APK وAPIs الجديدة
        // النتيجة: تقرير بحثي دقيق يوضح مدى جاهزية التطبيق لـ Android 16
        // ══════════════════════════════════════════════════════════════════════

        private async void BtnApiProfiler_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_sourceApkPath) || !File.Exists(_sourceApkPath))
            {
                WpfMessageBox.Show("اختر ملف APK أولاً.", "تنبيه",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            btnApiProfiler.IsEnabled = false;
            SetStatus("جاري Android 36 API Profiling...");
            LogMessage("\n╔══════════════════════════════════════════════════╗");
            LogMessage("║  🔬 Android 36 API Compatibility Profiler         ║");
            LogMessage("║  Aleppo University Research Project — API 36      ║");
            LogMessage("╚══════════════════════════════════════════════════╝");
            LogMessage($"   APK: {Path.GetFileName(_sourceApkPath)}");
            LogMessage($"   build-tools: 36.1.0 | android-36 SDK sources");

            string profilerWorkDir = Path.Combine(_workDir, $"profiler_{Guid.NewGuid():N}");

            try
            {
                var report = await Task.Run(() => RunAndroid36Profiling(profilerWorkDir));
                DisplayApiProfilerReport(report);
            }
            catch (Exception ex)
            {
                LogMessage($"❌ خطأ في API Profiler: {ex.Message}");
                SetStatus("خطأ");
            }
            finally
            {
                try { if (Directory.Exists(profilerWorkDir)) Directory.Delete(profilerWorkDir, true); } catch { }
                btnApiProfiler.IsEnabled = true;
                SetStatus("اكتمل API Profiling");
            }
        }

        /// <summary>
        /// الخطوات:
        ///   1. استخراج ملفات DEX من APK مباشرة (بدون apktool) للسرعة
        ///   2. تحليل كل DEX بـ dexdump.exe ← استخراج class references الكاملة
        ///   3. تشغيل aapt2 dump badging ← قراءة permissions, features, SDK info
        ///   4. فحص android-36/android/ ← بناء قاعدة بيانات APIs الجديدة في Android 16
        ///   5. مطابقة + تصنيف + توليد تقرير بحثي
        /// </summary>
        private Android36ProfilerReport RunAndroid36Profiling(string workDir)
        {
            var report = new Android36ProfilerReport();
            Directory.CreateDirectory(workDir);

            // ─── Step 1: استخراج ملفات DEX من APK مباشرة ─────────────────────
            LogMessage("\n📦 [1/5] استخراج ملفات DEX من APK...");
            ExtractDexFromApk(workDir, report);

            // ─── Step 2: تحليل DEX بـ dexdump.exe الحقيقي ──────────────────────
            LogMessage("🔬 [2/5] تحليل DEX بـ dexdump.exe (build-tools/36.1.0)...");
            AnalyzeDexWithDexdump(workDir, report);

            // ─── Step 3: aapt2 dump badging ─────────────────────────────────────
            LogMessage("📋 [3/5] تحليل APK metadata بـ aapt2...");
            AnalyzeWithAapt2(report);

            // ─── Step 4: بناء قاعدة بيانات APIs الجديدة من android-36/android/ ─
            LogMessage("📚 [4/5] قراءة android-36 SDK sources لاستخراج Android 16 APIs...");
            BuildAndroid36ApiDatabase(report);

            // ─── Step 5: مطابقة APIs ──────────────────────────────────────────────
            LogMessage("🔗 [5/5] مطابقة APIs المستخدمة مع Android 16 APIs الجديدة...");
            MatchApisAgainstAndroid36(report);

            return report;
        }

        // ══ Step 1: DEX Extraction ══════════════════════════════════════════════

        private void ExtractDexFromApk(string workDir, Android36ProfilerReport report)
        {
            try
            {
                using var zip = System.IO.Compression.ZipFile.OpenRead(_sourceApkPath!);
                int dexCount = 0;
                long totalDexSize = 0;

                foreach (var entry in zip.Entries
                    .Where(e => e.Name.StartsWith("classes") && e.Name.EndsWith(".dex"))
                    .OrderBy(e => e.Name))
                {
                    string destPath = Path.Combine(workDir, entry.Name);
                    entry.ExtractToFile(destPath, overwrite: true);
                    totalDexSize += entry.Length;
                    dexCount++;
                    report.DexFiles.Add(destPath);
                    LogMessage($"  ✓ {entry.Name} ({entry.Length / 1024.0:F1} KB)");
                }

                report.DexCount     = dexCount;
                report.TotalDexSize = totalDexSize;

                LogMessage($"  📊 إجمالي: {dexCount} ملف DEX | {totalDexSize / 1024.0 / 1024.0:F2} MB");
            }
            catch (Exception ex)
            {
                LogMessage($"  ❌ فشل استخراج DEX: {ex.Message}");
            }
        }

        // ══ Step 2: dexdump Real Analysis ══════════════════════════════════════

        private void AnalyzeDexWithDexdump(string workDir, Android36ProfilerReport report)
        {
            if (!File.Exists(_dexdumpPath))
            {
                LogMessage($"  ❌ dexdump.exe غير موجود: {_dexdumpPath}");
                return;
            }

            if (!report.DexFiles.Any())
            {
                LogMessage("  ⚠ لا توجد ملفات DEX للتحليل");
                return;
            }

            // مجموعة لتجميع كل class references من جميع DEX
            var allClassRefs    = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var allMethodRefs   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var allStringConsts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var dexPath in report.DexFiles)
            {
                AnalyzeSingleDex(dexPath, allClassRefs, allMethodRefs, allStringConsts, report);
            }

            report.AllClassReferences  = allClassRefs;
            report.AllMethodReferences = allMethodRefs;
            report.AllStringConstants  = allStringConsts;

            LogMessage($"  📊 Class References: {allClassRefs.Count:N0}");
            LogMessage($"  📊 Method References: {allMethodRefs.Count:N0}");
        }

        private void AnalyzeSingleDex(
            string dexPath,
            HashSet<string> classRefs,
            HashSet<string> methodRefs,
            HashSet<string> stringConsts,
            Android36ProfilerReport report)
        {
            try
            {
                // تشغيل dexdump -l plain -a (أسرع من -f وأغنى بالمراجع)
                var psi = new ProcessStartInfo
                {
                    FileName               = _dexdumpPath,
                    Arguments              = $"-d \"{dexPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                using var proc = Process.Start(psi)!;

                // نقرأ الخرج بشكل متدفق لتوفير الذاكرة
                string line;
                while ((line = proc.StandardOutput.ReadLine()) != null)
                {
                    line = line.Trim();

                    // استخراج Class references: كل ما يبدأ بـ L و ينتهي بـ ;
                    // مثال: Landroid/os/ProfilingManager; أو Landroid/net/L2capNetworkSpecifier;
                    foreach (Match m in Regex.Matches(line, @"L(android/[a-zA-Z0-9_$/]+);"))
                    {
                        // تحويل من bytecode path إلى Java-style: android/os/ProfilingManager
                        classRefs.Add(m.Groups[1].Value);
                    }

                    // من سطور invoke-*: نستخرج method references
                    // مثال: invoke-virtual {v0}, Landroid/os/PowerManager$WakeLock;->acquire(J)V
                    var methMatch = Regex.Match(line,
                        @"L(android/[a-zA-Z0-9_$/]+);->(\w+)\(");
                    if (methMatch.Success)
                    {
                        methodRefs.Add($"{methMatch.Groups[1].Value}->{methMatch.Groups[2].Value}");
                    }

                    // const-string داخل bytecode
                    var strMatch = Regex.Match(line,
                        @"const-string.*?[""](https?://[^""]{4,256})[""]");
                    if (strMatch.Success)
                        stringConsts.Add(strMatch.Groups[1].Value);
                }

                proc.WaitForExit(60_000);

                LogMessage($"  [{Path.GetFileName(dexPath)}] "
                         + $"{classRefs.Count} classes | {methodRefs.Count} methods");
            }
            catch (Exception ex)
            {
                report.Errors.Add($"dexdump error on {Path.GetFileName(dexPath)}: {ex.Message}");
                LogMessage($"  ⚠ {ex.Message}");
            }
        }

        // ══ Step 3: aapt2 Real Analysis ════════════════════════════════════════

        private void AnalyzeWithAapt2(Android36ProfilerReport report)
        {
            if (!File.Exists(_aapt2Path))
            {
                LogMessage($"  ⚠ aapt2.exe غير موجود، سيتم استخدام aapt...");
                AnalyzeWithAapt(report);
                return;
            }

            try
            {
                // aapt2 dump badging — يوفر معلومات أغنى من aapt
                var psi = new ProcessStartInfo
                {
                    FileName               = _aapt2Path,
                    Arguments              = $"dump badging \"{_sourceApkPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                using var proc = Process.Start(psi)!;
                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(30_000);

                ParseAaptBadgingOutput(output, report);
                LogMessage($"  ✓ aapt2: package={report.PackageName}, minSdk={report.MinSdk}, targetSdk={report.TargetSdk}");
            }
            catch (Exception ex)
            {
                report.Errors.Add($"aapt2 error: {ex.Message}");
                AnalyzeWithAapt(report); // fallback
            }
        }

        private void AnalyzeWithAapt(Android36ProfilerReport report)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = _aaptPath,
                    Arguments              = $"dump badging \"{_sourceApkPath}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                    StandardOutputEncoding = Encoding.UTF8
                };
                using var proc = Process.Start(psi)!;
                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(30_000);
                ParseAaptBadgingOutput(output, report);
            }
            catch (Exception ex)
            {
                report.Errors.Add($"aapt error: {ex.Message}");
            }
        }

        private static void ParseAaptBadgingOutput(string output, Android36ProfilerReport report)
        {
            // Package info
            var pkg     = Regex.Match(output, @"package: name='([^']+)'");
            var verName = Regex.Match(output, @"versionName='([^']+)'");
            var verCode = Regex.Match(output, @"versionCode='([^']+)'");
            var minSdk  = Regex.Match(output, @"sdkVersion:'(\d+)'");
            var tgtSdk  = Regex.Match(output, @"targetSdkVersion:'(\d+)'");
            var appName = Regex.Match(output, @"application-label:'([^']+)'");

            if (pkg.Success)     report.PackageName = pkg.Groups[1].Value;
            if (verName.Success) report.VersionName = verName.Groups[1].Value;
            if (verCode.Success) report.VersionCode = verCode.Groups[1].Value;
            if (minSdk.Success  && int.TryParse(minSdk.Groups[1].Value,  out int min)) report.MinSdk    = min;
            if (tgtSdk.Success  && int.TryParse(tgtSdk.Groups[1].Value,  out int tgt)) report.TargetSdk = tgt;
            if (appName.Success) report.AppName     = appName.Groups[1].Value;

            // Permissions
            foreach (Match m in Regex.Matches(output, @"uses-permission: name='([^']+)'"))
                report.DeclaredPermissions.Add(m.Groups[1].Value);

            // Features
            foreach (Match m in Regex.Matches(output, @"uses-feature: name='([^']+)'"))
                report.DeclaredFeatures.Add(m.Groups[1].Value);

            // Native ABIs
            foreach (Match m in Regex.Matches(output, @"native-code: '([^']+)'"))
                report.NativeAbis.Add(m.Groups[1].Value);
        }

        // ══ Step 4: Android 36 API Database Builder ═════════════════════════════
        // يقرأ شجرة android-36/android/ مباشرة لاستخراج APIs الجديدة الحصرية
        // ═══════════════════════════════════════════════════════════════════════

        private void BuildAndroid36ApiDatabase(Android36ProfilerReport report)
        {
            string basePath  = AppDomain.CurrentDomain.BaseDirectory;
            string android36 = Path.Combine(basePath, "res", "ResearchPayloadTools", "android-36", "android");

            if (!Directory.Exists(android36))
            {
                LogMessage($"  ❌ مجلد android-36 غير موجود: {android36}");
                return;
            }

            // APIs الحصرية الجديدة في Android 16 (API 36)
            // المصدر: Android 16 release notes + قراءة ملفات Java المصدرية
            var android16ExclusiveApis = new Dictionary<string, ApiInfo>
            {
                // ── Profiling API (جديد كلياً في API 36) ────────────────────────
                ["android/os/ProfilingManager"]           = new("ProfilingManager",          "Profiling", "API 36 — Java Heap Dump, Stack Sampling, System Trace"),
                ["android/os/ProfilingResult"]            = new("ProfilingResult",            "Profiling", "API 36 — نتيجة طلب Profiling"),
                ["android/os/ProfilingTrigger"]           = new("ProfilingTrigger",           "Profiling", "API 36 — System-triggered profiling"),

                // ── Network L2CAP/BLE (جديد في API 36) ──────────────────────────
                ["android/net/L2capNetworkSpecifier"]     = new("L2capNetworkSpecifier",      "Network",   "API 36 — BLE L2CAP شبكة IPv6 فوق Bluetooth"),
                ["android/net/LocalNetworkConfig"]        = new("LocalNetworkConfig",         "Network",   "API 36 — تهيئة الشبكة المحلية"),
                ["android/net/LocalNetworkInfo"]          = new("LocalNetworkInfo",           "Network",   "API 36 — معلومات الشبكة المحلية"),
                ["android/net/MulticastRoutingConfig"]    = new("MulticastRoutingConfig",     "Network",   "API 36 — تهيئة توجيه Multicast"),
                ["android/net/DscpPolicy"]                = new("DscpPolicy",                 "Network",   "API 36 — DSCP QoS Traffic Policy"),
                ["android/net/IpSecTransformState"]       = new("IpSecTransformState",        "Network",   "API 36 — حالة IPSec Transform"),

                // ── Thread IoT Network (جديد في API 36) ─────────────────────────
                ["android/net/thread/ThreadNetworkController"] = new("ThreadNetworkController", "Thread IoT", "API 36 — Matter/Thread شبكات IoT"),
                ["android/net/thread/ActiveOperationalDataset"] = new("ActiveOperationalDataset","Thread IoT","API 36 — Thread network credentials"),
                ["android/net/thread/PendingOperationalDataset"] = new("PendingOperationalDataset","Thread IoT","API 36 — Thread network pending config"),
                ["android/net/thread/ThreadNetworkException"]    = new("ThreadNetworkException",  "Thread IoT","API 36 — Thread exceptions"),

                // ── VCN (Virtual Carrier Network - جديد في API36) ──────────────
                ["android/net/vcn/VcnManager"]            = new("VcnManager",                "VCN",       "API 36 — Virtual Carrier Network"),
                ["android/net/vcn/VcnConfig"]             = new("VcnConfig",                 "VCN",       "API 36 — VCN Configuration"),
                ["android/net/vcn/VcnGatewayConnectionConfig"] = new("VcnGatewayConnectionConfig","VCN","API 36 — VCN Gateway Config"),

                // ── Connectivity Baklava (جديد في Baklava/API 36) ───────────────
                ["android/net/ConnectivityFrameworkInitializerBaklava"] = new("ConnectivityInitializerBaklava","Connectivity","API 36 — Baklava Connectivity init"),

                // ── Nearby Connections API (API 36) ──────────────────────────────
                ["android/nearby/NearbyManager"]          = new("NearbyManager",             "Nearby",    "API 36 — Nearby Connections API"),
                ["android/nearby/NearbyDevice"]           = new("NearbyDevice",              "Nearby",    "API 36 — جهاز قريب"),
                ["android/nearby/ScanRequest"]            = new("ScanRequest",               "Nearby",    "API 36 — طلب مسح Nearby"),
                ["android/nearby/ScanCallback"]           = new("ScanCallback",              "Nearby",    "API 36 — Nearby Scan Callback"),

                // ── UWB Ultra-Wideband (API 36) ──────────────────────────────────
                ["android/uwb/UwbManager"]                = new("UwbManager",               "UWB",        "API 36 — Ultra-Wideband ranging"),
                ["android/uwb/RangingSession"]            = new("RangingSession",            "UWB",        "API 36 — UWB Ranging Session"),
                ["android/uwb/UwbAddress"]                = new("UwbAddress",               "UWB",        "API 36 — UWB Address"),

                // ── Ranging API (API 36) ──────────────────────────────────────────
                ["android/ranging/RangingManager"]        = new("RangingManager",            "Ranging",    "API 36 — Ranging API جديد"),
                ["android/ranging/RangingSession"]        = new("RangingSession",            "Ranging",    "API 36 — Ranging Session"),
                ["android/ranging/RangingCapabilities"]   = new("RangingCapabilities",       "Ranging",    "API 36 — Ranging Capabilities"),

                // ── App Functions (API 36) ────────────────────────────────────────
                ["android/app/appfunctions/AppFunctionManager"]  = new("AppFunctionManager", "AppFunctions","API 36 — App Functions"),
                ["android/app/appfunctions/ExecuteAppFunctionRequest"] = new("ExecuteAppFunctionRequest","AppFunctions","API 36 — Execute App Function"),

                // ── Contextual Search (API 36) ────────────────────────────────────
                ["android/app/contextualsearch/ContextualSearchManager"] = new("ContextualSearchManager","ContextualSearch","API 36 — Contextual Search API"),

                // ── On-Device Intelligence (API 36) ──────────────────────────────
                ["android/app/ondeviceintelligence/OnDeviceIntelligenceManager"] = new("OnDeviceIntelligenceManager","AIIntelligence","API 36 — On-Device AI Intelligence"),

                // ── Health Connect (API 36 update) ────────────────────────────────
                ["android/health/connect/HealthConnectManager"] = new("HealthConnectManager","HealthConnect","API 36 — Health Connect"),

                // ── Security / Credentials (API 36) ──────────────────────────────
                ["android/security/keystore/KeyProperties"]      = new("KeyProperties",      "Security",   "API 36 — Key Properties update"),
                ["android/credentials/CredentialManager"]        = new("CredentialManager",  "Credentials","API 36 — Credential Manager"),
                ["android/credentials/GetCredentialRequest"]     = new("GetCredentialRequest","Credentials","API 36 — Get Credential Request"),

                // ── CPU Headroom (جديد كلياً في API 36) ──────────────────────────
                ["android/os/CpuHeadroomParams"]          = new("CpuHeadroomParams",         "Performance","API 36 — CPU Headroom Parameters"),
                ["android/os/GpuHeadroomParams"]          = new("GpuHeadroomParams",         "Performance","API 36 — GPU Headroom Parameters"),

                // ── Scheduling (API 36) ───────────────────────────────────────────
                ["android/scheduling/RebootReadinessManager"] = new("RebootReadinessManager","Scheduling","API 36 — Reboot Readiness"),

                // ── Privacy Sandbox / Ad Services (API 36) ───────────────────────
                ["android/adservices/AdServicesFrameworkInitializer"] = new("AdServicesFrameworkInitializer","AdServices","API 36 — Ad Services"),

                // ── Federated Compute (API 36) ────────────────────────────────────
                ["android/federatedcompute/FederatedComputeManager"] = new("FederatedComputeManager","FederatedCompute","API 36 — Federated Compute"),
            };

            // فحص ملفات Java المصدرية لاستخراج @FlaggedApi الجديدة
            // هذه APIs مخفية في ملفات Java بـ @FlaggedApi annotation
            int javaFilesScanned = 0;
            try
            {
                foreach (var javaFile in Directory.EnumerateFiles(android36, "*.java", SearchOption.AllDirectories)
                    .Take(500)) // نحدد بـ 500 ملف لضمان السرعة
                {
                    javaFilesScanned++;
                    ScanJavaSourceForNewApis(javaFile, android36, android16ExclusiveApis, report);
                }
            }
            catch (Exception ex)
            {
                report.Errors.Add($"Java scan error: {ex.Message}");
            }

            report.Android36ApiDatabase = android16ExclusiveApis;
            LogMessage($"  ✓ تم فحص {javaFilesScanned} ملف Java مصدري");
            LogMessage($"  ✓ قاعدة بيانات APIs: {android16ExclusiveApis.Count} API حصرية في Android 16");
        }

        /// <summary>
        /// يفحص ملف Java مصدري للبحث عن @FlaggedApi و @SystemApi annotations
        /// التي تشير إلى APIs جديدة أو محدّثة في Android 16
        /// </summary>
        private static void ScanJavaSourceForNewApis(
            string javaPath,
            string android36Root,
            Dictionary<string, ApiInfo> apiDb,
            Android36ProfilerReport report)
        {
            try
            {
                string content = File.ReadAllText(javaPath, Encoding.UTF8);

                // استخراج package name من ملف Java
                var pkgMatch = Regex.Match(content, @"^package\s+([\w.]+);", RegexOptions.Multiline);
                if (!pkgMatch.Success) return;

                string javaPackage = pkgMatch.Groups[1].Value; // e.g. android.net.thread
                string className   = Path.GetFileNameWithoutExtension(javaPath);

                // تحويل Java package إلى bytecode path
                string bytecodePath = javaPackage.Replace('.', '/') + "/" + className;
                // e.g. android/net/thread/ThreadNetworkController

                // فقط APIs android.*
                if (!bytecodePath.StartsWith("android/")) return;

                // البحث عن @FlaggedApi — يشير لميزة جديدة/مخفية في Android 16
                bool hasFlaggedApi = content.Contains("@FlaggedApi");
                // البحث عن @SystemApi — API نظام (مهمة للبحث)
                bool hasSystemApi  = content.Contains("@SystemApi");
                // البحث عن كلمة Baklava (كودنيم Android 16)
                bool hasBaklava    = content.Contains("Baklava") || content.Contains("baklava");
                // البحث عن @AddedIn(ApiLevel.BAKLAVA) أو similar
                bool isNewInApi36  = Regex.IsMatch(content, @"ApiLevel\.(?:BAKLAVA|V)", RegexOptions.IgnoreCase)
                                  || Regex.IsMatch(content, @"Build\.VERSION_CODES\.BAKLAVA");

                if ((hasFlaggedApi || isNewInApi36 || hasBaklava) && !apiDb.ContainsKey(bytecodePath))
                {
                    string category = javaPackage.Length > 8 ? javaPackage[8..] : "core"; // بعد "android."
                    string flags    = (hasFlaggedApi ? "FlaggedAPI " : "") +
                                     (hasSystemApi  ? "SystemAPI " : "") +
                                     (hasBaklava   ? "Baklava " : "");
                    apiDb[bytecodePath] = new ApiInfo(className, category, $"API 36 [{flags.Trim()}]");
                    report.NewApisFoundInSources++;
                }
            }
            catch { /* تجاهل ملفات الخطأ */ }
        }

        // ══ Step 5: API Matching ═════════════════════════════════════════════════

        private void MatchApisAgainstAndroid36(Android36ProfilerReport report)
        {
            if (report.Android36ApiDatabase == null || !report.AllClassReferences.Any())
            {
                LogMessage("  ⚠ لا توجد بيانات كافية للمطابقة");
                return;
            }

            foreach (var (apiPath, apiInfo) in report.Android36ApiDatabase)
            {
                // مطابقة مباشرة: هل الـ APK يستخدم هذا الـ class؟
                if (report.AllClassReferences.Contains(apiPath))
                {
                    report.UsedAndroid36Apis.Add(new UsedApiEntry(apiPath, apiInfo, true));
                    continue;
                }

                // مطابقة جزئية: ابحث عن class يحتوي اسم الـ API كـ prefix
                // مثلاً: إذا كان APK يستخدم android/os/ProfilingManager$Builder
                bool foundPartial = report.AllClassReferences.Any(r =>
                    r.StartsWith(apiPath, StringComparison.OrdinalIgnoreCase) ||
                    apiPath.StartsWith(r.Replace("$", "/"), StringComparison.OrdinalIgnoreCase));

                if (foundPartial)
                {
                    report.UsedAndroid36Apis.Add(new UsedApiEntry(apiPath, apiInfo, false));
                }
            }

            // تحليل الـ Permissions لتصنيف إضافي
            AnalyzePermissionCompatibility(report);

            // حساب نسبة التوافق
            int totalApis = report.Android36ApiDatabase.Count;
            int usedApis  = report.UsedAndroid36Apis.Count;
            report.CompatibilityScore = totalApis > 0 ? (usedApis * 100.0 / totalApis) : 0;

            LogMessage($"  ✓ APIs مطابقة: {usedApis} من {totalApis}");
            LogMessage($"  ✓ نسبة استخدام Android 16 APIs: {report.CompatibilityScore:F1}%");
        }

        private static void AnalyzePermissionCompatibility(Android36ProfilerReport report)
        {
            // Permissions الجديدة في Android 16 وخطورتها البحثية
            var newPermissions36 = new Dictionary<string, string>
            {
                ["android.permission.USE_EXACT_ALARM"]           = "API 34+ - Exact Alarms",
                ["android.permission.READ_MEDIA_VISUAL_USER_SELECTED"] = "API 34 - Photo picker",
                ["android.permission.NEARBY_WIFI_DEVICES"]       = "API 33+ - Nearby WiFi",
                ["android.permission.BLUETOOTH_SCAN"]            = "API 31+ - BT Scanning",
                ["android.permission.BLUETOOTH_CONNECT"]         = "API 31+ - BT Connect",
                ["android.permission.BLUETOOTH_ADVERTISE"]       = "API 31+ - BT Advertise",
                ["android.permission.UWB_RANGING"]               = "API 31+ - UWB Ranging",
                ["android.permission.HIDE_OVERLAY_WINDOWS"]      = "API 31+ - Hide Overlays",
                ["android.permission.MANAGE_MEDIA"]              = "API 31+ - Media Management",
                ["android.permission.SCHEDULE_EXACT_ALARM"]      = "API 33+ - Schedule Alarms",
                ["android.permission.USE_BIOMETRIC"]             = "API 28+ - Biometric",
            };

            foreach (var (perm, note) in newPermissions36)
            {
                if (report.DeclaredPermissions.Contains(perm))
                    report.RelevantPermissions.Add((perm, note));
            }
        }

        // ══ Display Results ══════════════════════════════════════════════════════

        private void DisplayApiProfilerReport(Android36ProfilerReport r)
        {
            Dispatcher.Invoke(() =>
            {
                LogMessage("\n╔══════════════════════════════════════════════════╗");
                LogMessage("║   📊 Android 36 API Compatibility Report          ║");
                LogMessage("╚══════════════════════════════════════════════════╝");

                // معلومات الحزمة
                LogMessage($"\n📱 التطبيق: {r.AppName}");
                LogMessage($"   Package: {r.PackageName}");
                LogMessage($"   Version: {r.VersionName} (code: {r.VersionCode})");
                LogMessage($"   minSdk: {r.MinSdk}  |  targetSdk: {r.TargetSdk}");

                // حكم التوافق مع Android 16
                string sdkVerdict = r.TargetSdk switch
                {
                    >= 36 => "✅ يستهدف Android 16 (API 36) مباشرة",
                    >= 34 => "🟡 متوافق جزئياً — يحتاج رفع targetSdk إلى 36",
                    >= 31 => "🟠 يحتاج تحديث — minSdk قديم",
                    _     => "🔴 غير متوافق — يحتاج مراجعة شاملة"
                };
                LogMessage($"\n🎯 حالة Android 16: {sdkVerdict}");

                // DEX Stats
                LogMessage($"\n📦 DEX Analysis (dexdump 36.1.0):");
                LogMessage($"   ملفات DEX: {r.DexCount}  |  الحجم: {r.TotalDexSize / 1024.0 / 1024.0:F2} MB");
                LogMessage($"   Class References: {r.AllClassReferences.Count:N0}");
                LogMessage($"   Method References: {r.AllMethodReferences.Count:N0}");

                // Android 16 APIs المُكتشَفة
                if (r.UsedAndroid36Apis.Any())
                {
                    var directUse  = r.UsedAndroid36Apis.Where(x => x.IsDirectMatch).ToList();
                    var partialUse = r.UsedAndroid36Apis.Where(x => !x.IsDirectMatch).ToList();

                    LogMessage($"\n🔬 Android 16 APIs المُكتشَفة ({r.UsedAndroid36Apis.Count}):");

                    if (directUse.Any())
                    {
                        LogMessage($"\n  ✅ مطابقة مباشرة ({directUse.Count}):");
                        // نجمع بالـ Category
                        foreach (var grp in directUse.GroupBy(x => x.Info.Category).OrderBy(g => g.Key))
                        {
                            LogMessage($"    [{grp.Key}]");
                            foreach (var api in grp)
                                LogMessage($"      • {api.Info.ClassName} — {api.Info.Description}");
                        }
                    }

                    if (partialUse.Any())
                    {
                        LogMessage($"\n  🔍 مطابقة جزئية ({partialUse.Count}):");
                        foreach (var grp in partialUse.GroupBy(x => x.Info.Category).OrderBy(g => g.Key))
                        {
                            LogMessage($"    [{grp.Key}]");
                            foreach (var api in grp)
                                LogMessage($"      ~ {api.Info.ClassName} — {api.Info.Description}");
                        }
                    }
                }
                else
                {
                    LogMessage("\n  ℹ لم يُكتشف استخدام صريح لـ Android 16 APIs الجديدة");
                    LogMessage("    (التطبيق يعمل على Android 16 لكن لا يستخدم APIs الجديدة بعد)");
                }

                // Permissions البحثية
                if (r.RelevantPermissions.Any())
                {
                    LogMessage($"\n🔑 Permissions ذات صلة بـ Android 16 ({r.RelevantPermissions.Count}):");
                    foreach (var (perm, note) in r.RelevantPermissions)
                        LogMessage($"  • {perm.Split('.').Last()} — {note}");
                }

                // Native ABIs
                if (r.NativeAbis.Any())
                    LogMessage($"\n📱 Native ABIs: {string.Join(", ", r.NativeAbis)}");

                // الأخطاء
                if (r.Errors.Any())
                {
                    LogMessage($"\n⚠ تحذيرات ({r.Errors.Count}):");
                    foreach (var err in r.Errors.Take(5))
                        LogMessage($"  • {err}");
                }

                // نسبة التوافق
                LogMessage($"\n📈 نسبة استخدام Android 16 APIs الحصرية: {r.CompatibilityScore:F1}%");
                LogMessage($"   APIs الجديدة المُكتشَفة من android-36 sources: {r.NewApisFoundInSources}");
                LogMessage("──────────────────────────────────────────────────");
                LogMessage("✅ تم إنجاز Android 36 API Compatibility Profiling");
                LogMessage("──────────────────────────────────────────────────\n");

                // حفظ التقرير
                SaveApiProfilerReport(r);
            });
        }

        private void SaveApiProfilerReport(Android36ProfilerReport r)
        {
            try
            {
                string apkName    = Path.GetFileNameWithoutExtension(_sourceApkPath ?? "unknown");
                string timestamp  = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string reportPath = Path.Combine(_outputDir,
                    $"android36_profiler_{apkName}_{timestamp}.txt");

                var sb = new StringBuilder();
                sb.AppendLine("════════════════════════════════════════════════════");
                sb.AppendLine($" Android 36 API Compatibility Report");
                sb.AppendLine($" Aleppo University Research Project");
                sb.AppendLine($" Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($" APK:  {_sourceApkPath}");
                sb.AppendLine("════════════════════════════════════════════════════");
                sb.AppendLine();
                sb.AppendLine($"[Package Info]");
                sb.AppendLine($"  App Name   : {r.AppName}");
                sb.AppendLine($"  Package    : {r.PackageName}");
                sb.AppendLine($"  Version    : {r.VersionName} (code: {r.VersionCode})");
                sb.AppendLine($"  MinSdk     : {r.MinSdk}");
                sb.AppendLine($"  TargetSdk  : {r.TargetSdk}");
                sb.AppendLine();
                sb.AppendLine($"[DEX Analysis — dexdump 36.1.0]");
                sb.AppendLine($"  DEX Files  : {r.DexCount}");
                sb.AppendLine($"  Total Size : {r.TotalDexSize / 1024.0 / 1024.0:F2} MB");
                sb.AppendLine($"  Class Refs : {r.AllClassReferences.Count:N0}");
                sb.AppendLine($"  Method Refs: {r.AllMethodReferences.Count:N0}");
                sb.AppendLine();
                sb.AppendLine($"[Android 16 APIs Detected] ({r.UsedAndroid36Apis.Count})");
                foreach (var api in r.UsedAndroid36Apis.OrderBy(x => x.Info.Category))
                    sb.AppendLine($"  [{(api.IsDirectMatch ? "DIRECT" : "PARTIAL")}] {api.ApiPath}  —  {api.Info.Description}");
                sb.AppendLine();
                sb.AppendLine($"[Permissions]");
                foreach (var p in r.DeclaredPermissions.OrderBy(x => x))
                    sb.AppendLine($"  {p}");
                sb.AppendLine();
                sb.AppendLine($"[Features]");
                foreach (var f in r.DeclaredFeatures.OrderBy(x => x))
                    sb.AppendLine($"  {f}");
                sb.AppendLine();
                sb.AppendLine($"[Compatibility Score] {r.CompatibilityScore:F1}%");
                sb.AppendLine($"[New FlaggedAPIs Found in android-36 sources] {r.NewApisFoundInSources}");

                File.WriteAllText(reportPath, sb.ToString(), Encoding.UTF8);
                LogMessage($"💾 تقرير API Profiler محفوظ: {Path.GetFileName(reportPath)}");
            }
            catch (Exception ex)
            {
                LogMessage($"  ⚠ فشل حفظ تقرير Profiler: {ex.Message}");
            }
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // DEX Entropy & Obfuscation Fingerprinter — ميزة بحثية حصرية Android 36
        // ══════════════════════════════════════════════════════════════════════
        //
        // الميزة تعتمد بالكامل على الأدوات الحقيقية الموجودة:
        //   1. dexdump.exe (build-tools/36.1.0) — تحليل DEX bytecode الحقيقي
        //   2. ZipFile — استخراج DEX من APK مباشرة
        //   3. android-36/android/ sources — قراءة @FlaggedApi الحقيقية
        //   4. Shannon Entropy — خوارزمية رياضية حقيقية على String pool
        //
        // ما تكشفه الميزة:
        //   A. Obfuscation Level: مستوى التشفير (None / Light / ProGuard / R8 / DexGuard)
        //   B. Class Name Entropy: هل الأسماء مشفرة؟ (a, b, c vs ActivityManager)
        //   C. String Pool Entropy: Shannon entropy للـ const-strings
        //   D. Method Density: متوسط الميثودز لكل كلاس (مؤشر ProGuard)
        //   E. Android 36 FlaggedAPI Usage: APIs الجديدة المخفية في Android 16
        //   F. DEX Mutation Detection: هل الـ DEX مُعدَّل؟ (anti-tamper)
        //   G. Research Fingerprint Hash: بصمة فريدة للتطبيق قابلة للمقارنة
        // ══════════════════════════════════════════════════════════════════════

        #region DEX Entropy & Obfuscation Fingerprinter

        private async void BtnEntropyFingerprint_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_sourceApkPath) || !File.Exists(_sourceApkPath))
            {
                WpfMessageBox.Show("اختر ملف APK أولاً.", "تنبيه",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            btnEntropyFingerprint.IsEnabled = false;
            SetStatus("جاري DEX Entropy Fingerprinting...");
            LogMessage("\n╔══════════════════════════════════════════════════════════╗");
            LogMessage("║  🧪 DEX Entropy & Obfuscation Fingerprinter              ║");
            LogMessage("║  Aleppo University Research — Android 36 (API 36)        ║");
            LogMessage("╚══════════════════════════════════════════════════════════╝");
            LogMessage($"   APK: {Path.GetFileName(_sourceApkPath)}");
            LogMessage($"   dexdump: build-tools/36.1.0 | Android 36 FlaggedAPI sources");

            string workDir = Path.Combine(_workDir, $"entropy_{Guid.NewGuid():N}");

            try
            {
                var report = await Task.Run(() => RunEntropyFingerprinting(workDir));
                DisplayEntropyReport(report);
            }
            catch (Exception ex)
            {
                LogMessage($"❌ خطأ في Entropy Fingerprinter: {ex.Message}");
                SetStatus("خطأ");
            }
            finally
            {
                try { if (Directory.Exists(workDir)) Directory.Delete(workDir, true); } catch { }
                btnEntropyFingerprint.IsEnabled = true;
                SetStatus("اكتمل DEX Entropy Fingerprinting");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  المحرك الرئيسي للتحليل
        // ════════════════════════════════════════════════════════════════════

        private EntropyFingerprintReport RunEntropyFingerprinting(string workDir)
        {
            var report = new EntropyFingerprintReport();
            Directory.CreateDirectory(workDir);
            report.ApkPath = _sourceApkPath!;
            report.ApkName = Path.GetFileNameWithoutExtension(_sourceApkPath!);

            // ─── Step 1: استخراج DEX مباشرة من ZIP (بدون apktool — أسرع) ───
            LogMessage("\n📦 [1/6] استخراج ملفات DEX من APK...");
            ExtractDexForEntropy(workDir, report);

            if (!report.DexFiles.Any())
            {
                LogMessage("  ❌ لا توجد ملفات DEX — تأكد من صحة APK");
                return report;
            }

            // ─── Step 2: تحليل DEX بـ dexdump.exe الحقيقي ──────────────────
            LogMessage($"🔬 [2/6] تشغيل dexdump.exe (build-tools/36.1.0) على {report.DexFiles.Count} ملف...");
            RunDexdumpForEntropy(workDir, report);

            // ─── Step 3: حساب Shannon Entropy للـ String Pool ───────────────
            LogMessage("📐 [3/6] حساب Shannon Entropy للـ String Pool...");
            ComputeStringPoolEntropy(report);

            // ─── Step 4: تحليل أسماء الكلاسات (Obfuscation Detection) ───────
            LogMessage("🔍 [4/6] فحص أسماء الكلاسات لكشف ProGuard/R8/DexGuard...");
            AnalyzeClassNameObfuscation(report);

            // ─── Step 5: فحص Android 36 @FlaggedAPI من المصادر الحقيقية ─────
            LogMessage("⚡ [5/6] قراءة @FlaggedApi من android-36 sources...");
            ScanFlaggedApisUsage(report);

            // ─── Step 6: بناء بصمة DEX الرقمية (Research Fingerprint) ────────
            LogMessage("🆔 [6/6] بناء بصمة DEX الرقمية...");
            BuildDexFingerprint(report);

            return report;
        }

        // ════════════════════════════════════════════════════════════════════
        //  Step 1: استخراج DEX
        // ════════════════════════════════════════════════════════════════════

        private static void ExtractDexForEntropy(string workDir, EntropyFingerprintReport report)
        {
            try
            {
                using var zip = System.IO.Compression.ZipFile.OpenRead(report.ApkPath);
                foreach (var entry in zip.Entries
                    .Where(e => e.Name.StartsWith("classes") && e.Name.EndsWith(".dex"))
                    .OrderBy(e => e.Name))
                {
                    string dest = Path.Combine(workDir, entry.Name);
                    entry.ExtractToFile(dest, overwrite: true);
                    report.DexFiles.Add((dest, entry.Length));
                    report.TotalDexBytes += entry.Length;
                }
            }
            catch (Exception ex)
            {
                report.Errors.Add($"DEX extraction: {ex.Message}");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  Step 2: dexdump الحقيقي — استخراج Class/Method/String data
        // ════════════════════════════════════════════════════════════════════

        private void RunDexdumpForEntropy(string workDir, EntropyFingerprintReport report)
        {
            if (!File.Exists(_dexdumpPath))
            {
                report.Errors.Add("dexdump.exe غير موجود");
                LogMessage($"  ⚠ dexdump.exe غير موجود: {_dexdumpPath}");
                return;
            }

            foreach (var (dexPath, dexSize) in report.DexFiles)
            {
                try
                {
                    // -d: disassemble DEX bytecode (يعطي class names + method signatures + const-strings)
                    var psi = new ProcessStartInfo
                    {
                        FileName               = _dexdumpPath,
                        Arguments              = $"-d \"{dexPath}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError  = true,
                        UseShellExecute        = false,
                        CreateNoWindow         = true,
                        StandardOutputEncoding = Encoding.UTF8
                    };

                    using var proc = Process.Start(psi)!;

                    // نقرأ بشكل streaming لتوفير الذاكرة
                    string? line;
                    int methodsInCurrentClass = 0;
                    string  currentClass      = "";

                    while ((line = proc.StandardOutput.ReadLine()) != null)
                    {
                        line = line.Trim();
                        if (string.IsNullOrEmpty(line)) continue;

                        // ── استخراج Class Name ─────────────────────────────
                        // مثال: "  Class #0 header:"  ثم "  class_idx      : 0"
                        // أو في bytecode:  ".class public Lcom/example/MainActivity;"
                        var classMatch = Regex.Match(line, @"\.class\s+[\w\s]*L([^;]+);");
                        if (classMatch.Success)
                        {
                            if (!string.IsNullOrEmpty(currentClass))
                                report.MethodsPerClass.Add(methodsInCurrentClass);

                            currentClass = classMatch.Groups[1].Value; // e.g. com/example/MainActivity
                            report.AllClassNames.Add(currentClass);
                            methodsInCurrentClass = 0;
                            continue;
                        }

                        // ── استخراج Method Name ────────────────────────────
                        // مثال: ".method public constructor <init>()V"
                        if (line.StartsWith(".method"))
                        {
                            var methMatch = Regex.Match(line, @"\.method\s+[\w\s]*(\S+)\(");
                            if (methMatch.Success)
                            {
                                report.AllMethodNames.Add(methMatch.Groups[1].Value);
                                methodsInCurrentClass++;
                            }
                            continue;
                        }

                        // ── استخراج const-string (String Pool) ───────────
                        // مثال:  const-string v0, "https://api.example.com"
                        var strMatch = Regex.Match(line,
                            @"const-string(?:/jumbo)?\s+\w+,\s+""([^""]{1,512})""");
                        if (strMatch.Success)
                        {
                            string val = strMatch.Groups[1].Value;
                            report.StringPool.Add(val);
                            report.StringFrequency.TryGetValue(val, out int freq);
                            report.StringFrequency[val] = freq + 1;
                            continue;
                        }

                        // ── استخراج android.* Class References ───────────
                        // نستخدمها لمطابقة @FlaggedApi
                        foreach (Match m in Regex.Matches(line, @"L(android/[a-zA-Z0-9_$/]+);"))
                            report.AndroidApiRefs.Add(m.Groups[1].Value);

                        // ── كشف Reflection (مؤشر Anti-Tamper/Anti-Debug) ─
                        if (line.Contains("getDeclaredMethod") ||
                            line.Contains("getDeclaredField") ||
                            line.Contains("forName"))
                            report.ReflectionCallCount++;

                        // ── كشف native methods (JNI) ──────────────────────
                        if (line.Contains(".method") && line.Contains("native"))
                            report.NativeMethodCount++;

                        // ── كشف Encrypted String Patterns ─────────────────
                        // R8/DexGuard يُشفّر الـ strings في invoke-static محددة
                        if (Regex.IsMatch(line,
                            @"invoke-static.*decrypt|invoke-static.*decode|invoke-static.*deobfusc",
                            RegexOptions.IgnoreCase))
                            report.EncryptedStringCallCount++;
                    }

                    // إضافة آخر كلاس
                    if (!string.IsNullOrEmpty(currentClass))
                        report.MethodsPerClass.Add(methodsInCurrentClass);

                    proc.WaitForExit(90_000); // timeout 1.5 دقيقة
                    report.DexdumpExitCodes.Add(proc.ExitCode);

                    LogMessage($"  ✓ {Path.GetFileName(dexPath)}: " +
                               $"{report.AllClassNames.Count} class | " +
                               $"{report.AllMethodNames.Count} method | " +
                               $"{report.StringPool.Count} strings");
                }
                catch (Exception ex)
                {
                    report.Errors.Add($"dexdump({Path.GetFileName(dexPath)}): {ex.Message}");
                    LogMessage($"  ⚠ {ex.Message}");
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  Step 3: Shannon Entropy الحقيقية للـ String Pool
        // ════════════════════════════════════════════════════════════════════
        // Shannon entropy = -Σ p(x) * log2(p(x))
        // قيمة عالية (>4.5) = strings عشوائية = تشفير مرجح
        // قيمة منخفضة (<3.0) = strings قابلة للقراءة = بدون تشفير
        // ════════════════════════════════════════════════════════════════════

        private static void ComputeStringPoolEntropy(EntropyFingerprintReport report)
        {
            if (!report.StringPool.Any()) return;

            // 1. Shannon Entropy على الـ character level
            var allChars = string.Join("", report.StringPool);
            if (allChars.Length == 0) return;

            var charFreq = new Dictionary<char, int>();
            foreach (char c in allChars)
            {
                charFreq.TryGetValue(c, out int f);
                charFreq[c] = f + 1;
            }

            double entropy = 0.0;
            double total   = allChars.Length;
            foreach (int freq in charFreq.Values)
            {
                double p = freq / total;
                entropy -= p * Math.Log2(p);
            }
            report.StringPoolCharEntropy = entropy;

            // 2. Unique string ratio (تكرار الـ strings — مؤشر ProGuard)
            report.UniqueStringRatio = report.StringPool.Count > 0
                ? (double)report.StringFrequency.Count / report.StringPool.Count
                : 0;

            // 3. قياس متوسط طول الـ strings
            report.AvgStringLength = report.StringPool.Any()
                ? report.StringPool.Average(s => s.Length)
                : 0;

            // 4. كشف Base64-encoded strings
            report.Base64StringCount = report.StringPool
                .Count(s => s.Length >= 20 &&
                            Regex.IsMatch(s, @"^[A-Za-z0-9+/]{20,}={0,2}$"));

            // 5. كشف Hex-encoded strings (DexGuard signature)
            report.HexStringCount = report.StringPool
                .Count(s => s.Length >= 16 &&
                            Regex.IsMatch(s, @"^[0-9a-fA-F]{16,}$"));
        }

        // ════════════════════════════════════════════════════════════════════
        //  Step 4: تحليل أسماء الكلاسات لكشف نوع Obfuscator
        // ════════════════════════════════════════════════════════════════════

        private static void AnalyzeClassNameObfuscation(EntropyFingerprintReport report)
        {
            if (!report.AllClassNames.Any()) return;

            // استخراج اسم الكلاس البسيط (آخر جزء بعد /)
            var simpleNames = report.AllClassNames
                .Select(c => c.Split('/').LastOrDefault() ?? c)
                .ToList();

            int total = simpleNames.Count;
            if (total == 0) return;

            // 1. كلاسات ذات اسم قصير جداً (a, b, c, aa, ab) — مؤشر ProGuard/R8
            int shortNames = simpleNames.Count(n => n.Length <= 2 &&
                                                     Regex.IsMatch(n, @"^[a-z][a-z0-9]?$"));
            report.ShortClassNameRatio = (double)shortNames / total;

            // 2. كلاسات بأسماء طويلة منطقية (ActivityManager, NetworkHelper)
            int descriptiveNames = simpleNames.Count(n => n.Length > 6 &&
                                                           n.Any(char.IsUpper));
            report.DescriptiveClassNameRatio = (double)descriptiveNames / total;

            // 3. كشف DexGuard: أسماء رموز Unicode غير لاتينية
            int unicodeNames = simpleNames.Count(n =>
                n.Any(c => c > 127 || (c < 'A' && c != '$' && c != '_')));
            report.UnicodeClassNameCount = unicodeNames;

            // 4. Shannon Entropy على أسماء الكلاسات
            var nameChars = string.Join("", simpleNames);
            if (nameChars.Length > 0)
            {
                var freq = new Dictionary<char, int>();
                foreach (char c in nameChars)
                {
                    freq.TryGetValue(c, out int f);
                    freq[c] = f + 1;
                }
                double ent = 0.0;
                double t   = nameChars.Length;
                foreach (int f in freq.Values)
                {
                    double p = f / t;
                    ent -= p * Math.Log2(p);
                }
                report.ClassNameEntropy = ent;
            }

            // 5. تحديد نوع Obfuscator بناءً على المؤشرات
            report.ObfuscationLevel = DetermineObfuscationLevel(report);
        }

        private static ObfuscationLevel DetermineObfuscationLevel(EntropyFingerprintReport report)
        {
            // DexGuard: Unicode names أو String encryption calls عالية
            if (report.UnicodeClassNameCount > 5 || report.EncryptedStringCallCount > 20)
                return ObfuscationLevel.DexGuard;

            // R8 Full Mode: نسبة أسماء قصيرة عالية جداً + Entropy مرتفعة
            if (report.ShortClassNameRatio > 0.60 &&
                report.StringPoolCharEntropy > 4.5)
                return ObfuscationLevel.R8Full;

            // ProGuard / R8 Standard: نسبة أسماء قصيرة متوسطة
            if (report.ShortClassNameRatio > 0.30)
                return ObfuscationLevel.ProGuard;

            // Light Obfuscation: بعض التشفير لكن معظم الأسماء واضحة
            if (report.ShortClassNameRatio > 0.10 ||
                report.StringPoolCharEntropy > 4.0)
                return ObfuscationLevel.Light;

            // لا تشفير
            return ObfuscationLevel.None;
        }

        // ════════════════════════════════════════════════════════════════════
        //  Step 5: كشف @FlaggedApi من android-36 sources الحقيقية
        // ════════════════════════════════════════════════════════════════════
        // يقرأ ملفات Java الحقيقية من android-36/android/
        // ويبحث عن:
        //   @FlaggedApi("android.xxx.flags.xxx")
        //   @SystemApi
        //   @AddedIn(ApiLevel.BAKLAVA)
        // ثم يُطابقها مع الـ class references المستخرجة بـ dexdump
        // ════════════════════════════════════════════════════════════════════

        private void ScanFlaggedApisUsage(EntropyFingerprintReport report)
        {
            string basePath  = AppDomain.CurrentDomain.BaseDirectory;
            string android36 = Path.Combine(basePath, "res", "ResearchPayloadTools", "android-36", "android");

            if (!Directory.Exists(android36))
            {
                report.Errors.Add("android-36 sources غير موجود");
                LogMessage($"  ⚠ مجلد android-36 غير موجود: {android36}");
                return;
            }

            int filesScanned  = 0;
            int flaggedFound  = 0;

            try
            {
                // نقرأ فقط أهم المجلدات الحصرية في Android 36
                var priorityDirs = new[]
                {
                    Path.Combine(android36, "ranging"),        // UWB Ranging — جديد كلياً
                    Path.Combine(android36, "nearby"),         // Nearby Connections
                    Path.Combine(android36, "uwb"),            // Ultra-Wideband
                    Path.Combine(android36, "os"),             // CpuHeadroomParams, GpuHeadroomParams
                    Path.Combine(android36, "net"),            // L2capNetworkSpecifier, Thread
                    Path.Combine(android36, "app"),            // OnDeviceIntelligence, AppFunctions
                    Path.Combine(android36, "scheduling"),     // RebootReadinessManager
                    Path.Combine(android36, "federatedcompute"),// Federated Learning
                    Path.Combine(android36, "health"),         // Health Connect
                    Path.Combine(android36, "credentials"),    // Credential Manager
                    Path.Combine(android36, "aconfig"),        // Feature Flags
                };

                foreach (var dir in priorityDirs.Where(Directory.Exists))
                {
                    foreach (var javaFile in Directory.EnumerateFiles(dir, "*.java",
                        SearchOption.AllDirectories).Take(200))
                    {
                        filesScanned++;
                        ScanSingleJavaFileForFlags(javaFile, android36, report, ref flaggedFound);
                    }
                }
            }
            catch (Exception ex)
            {
                report.Errors.Add($"FlaggedAPI scan: {ex.Message}");
            }

            LogMessage($"  ✓ فُحص {filesScanned} ملف Java مصدري من android-36");
            LogMessage($"  ✓ {flaggedFound} @FlaggedApi/Baklava API وُجدت في APK");
        }

        private static void ScanSingleJavaFileForFlags(
            string javaFile,
            string android36Root,
            EntropyFingerprintReport report,
            ref int flaggedFound)
        {
            try
            {
                string content = File.ReadAllText(javaFile, Encoding.UTF8);

                // استخراج معلومات الكلاس
                var pkgMatch = Regex.Match(content, @"^package\s+([\w.]+);", RegexOptions.Multiline);
                if (!pkgMatch.Success) return;

                string javaPackage = pkgMatch.Groups[1].Value;   // e.g. android.ranging
                string className   = Path.GetFileNameWithoutExtension(javaFile);
                string apiPath     = javaPackage.Replace('.', '/') + "/" + className;
                // e.g. android/ranging/RangingSession

                if (!apiPath.StartsWith("android/")) return;

                // تحقق: هل APK يستخدم هذا الـ class؟
                bool apkUsesApi = report.AndroidApiRefs.Contains(apiPath) ||
                    report.AndroidApiRefs.Any(r =>
                        r.StartsWith(apiPath, StringComparison.OrdinalIgnoreCase));

                // هل هذا الـ class يحتوي @FlaggedApi أو Baklava markers؟
                bool hasFlaggedApi = content.Contains("@FlaggedApi");
                bool hasBaklava    = content.Contains("Baklava") ||
                                     content.Contains("@AddedIn(ApiLevel.BAKLAVA)") ||
                                     content.Contains("Build.VERSION_CODES.BAKLAVA");
                bool hasSystemApi  = content.Contains("@SystemApi");

                // استخراج اسم الـ flag من @FlaggedApi("android.xxx.flags.yyy")
                string flagName = "";
                var flagMatch = Regex.Match(content,
                    @"@FlaggedApi\(""([^""]+)""\)");
                if (flagMatch.Success) flagName = flagMatch.Groups[1].Value;

                if ((hasFlaggedApi || hasBaklava) && !report.FlaggedApisInSdk.ContainsKey(apiPath))
                {
                    string category = javaPackage.Length > 8
                        ? javaPackage.Substring(8) // بعد "android."
                        : "core";

                    var entry = new FlaggedApiEntry
                    {
                        ApiPath    = apiPath,
                        ClassName  = className,
                        Category   = category,
                        FlagName   = flagName,
                        IsBaklava  = hasBaklava,
                        IsSystemApi= hasSystemApi,
                        ApkUsesIt  = apkUsesApi
                    };

                    report.FlaggedApisInSdk[apiPath] = entry;

                    if (apkUsesApi)
                    {
                        report.FlaggedApisUsedByApk.Add(entry);
                        flaggedFound++;
                    }
                }
            }
            catch { /* تجاهل */ }
        }

        // ════════════════════════════════════════════════════════════════════
        //  Step 6: بصمة DEX الرقمية (Research Fingerprint)
        // ════════════════════════════════════════════════════════════════════
        // تُنشئ hash فريد للتطبيق مبني على:
        //   - عدد الكلاسات + الميثودز
        //   - توزيع أسماء الكلاسات
        //   - String Entropy
        //   - توقيع الـ Obfuscation
        // النتيجة: تعريف فريد يمكن مقارنته عبر إصدارات مختلفة من نفس التطبيق
        // ════════════════════════════════════════════════════════════════════

        private static void BuildDexFingerprint(EntropyFingerprintReport report)
        {
            // مكونات البصمة
            var components = new StringBuilder();
            components.Append($"CLASS_COUNT:{report.AllClassNames.Count}|");
            components.Append($"METHOD_COUNT:{report.AllMethodNames.Count}|");
            components.Append($"DEX_COUNT:{report.DexFiles.Count}|");
            components.Append($"STR_ENTROPY:{report.StringPoolCharEntropy:F3}|");
            components.Append($"CLASS_ENTROPY:{report.ClassNameEntropy:F3}|");
            components.Append($"SHORT_RATIO:{report.ShortClassNameRatio:F3}|");
            components.Append($"OBFUSC:{report.ObfuscationLevel}|");
            components.Append($"NATIVE_METHODS:{report.NativeMethodCount}|");
            components.Append($"REFLECTION:{report.ReflectionCallCount}|");

            // أضف أسماء الـ namespace الأولى كجزء من البصمة
            var topPackages = report.AllClassNames
                .Select(c => c.Contains('/') ? c[..c.IndexOf('/')] : c)
                .GroupBy(p => p)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => g.Key);
            components.Append($"TOP_PKG:{string.Join(",", topPackages)}|");

            // SHA-256 hash للبصمة
            using var sha = System.Security.Cryptography.SHA256.Create();
            byte[] hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(components.ToString()));
            report.ResearchFingerprintHash = Convert.ToHexString(hashBytes)[..32]; // أول 32 حرف

            report.FingerprintComponents = components.ToString();
        }

        // ════════════════════════════════════════════════════════════════════
        //  عرض النتائج
        // ════════════════════════════════════════════════════════════════════

        private void DisplayEntropyReport(EntropyFingerprintReport r)
        {
            Dispatcher.Invoke(() =>
            {
                LogMessage("\n╔══════════════════════════════════════════════════════════╗");
                LogMessage("║             📊 تقرير DEX Entropy Fingerprinter           ║");
                LogMessage("╚══════════════════════════════════════════════════════════╝");

                // ── معلومات أساسية ─────────────────────────────────────────
                LogMessage($"\n📦 ملفات DEX:");
                foreach (var (path, size) in r.DexFiles)
                    LogMessage($"   • {Path.GetFileName(path)}: {size / 1024.0:F1} KB");
                LogMessage($"   الحجم الكلي: {r.TotalDexBytes / 1024.0 / 1024.0:F2} MB");

                // ── إحصاءات الكود ──────────────────────────────────────────
                LogMessage($"\n📐 إحصاءات الكود (dexdump 36.1.0):");
                LogMessage($"   Classes : {r.AllClassNames.Count:N0}");
                LogMessage($"   Methods : {r.AllMethodNames.Count:N0}");
                LogMessage($"   Strings : {r.StringPool.Count:N0}  (فريد: {r.StringFrequency.Count:N0})");
                if (r.MethodsPerClass.Any())
                {
                    double avgMeth = r.MethodsPerClass.Average();
                    int    maxMeth = r.MethodsPerClass.Max();
                    LogMessage($"   Avg Methods/Class: {avgMeth:F1}  |  Max: {maxMeth}");
                }
                LogMessage($"   Native Methods: {r.NativeMethodCount}");
                LogMessage($"   Reflection Calls: {r.ReflectionCallCount}");
                if (r.EncryptedStringCallCount > 0)
                    LogMessage($"   ⚠ Encrypted String Decryption Calls: {r.EncryptedStringCallCount}");

                // ── Shannon Entropy ─────────────────────────────────────────
                LogMessage($"\n📈 Shannon Entropy Analysis:");
                string entropyRating = r.StringPoolCharEntropy switch
                {
                    > 5.0  => "🔴 مرتفعة جداً — تشفير قوي مرجح",
                    > 4.5  => "🟠 مرتفعة — R8 Full Mode أو DexGuard",
                    > 4.0  => "🟡 متوسطة — ProGuard أو R8 Standard",
                    > 3.0  => "🟢 منخفضة-متوسطة — تشفير بسيط أو بدونه",
                    _       => "🟢 منخفضة — لا تشفير واضح"
                };
                LogMessage($"   String Pool Entropy  : {r.StringPoolCharEntropy:F4} bits  → {entropyRating}");
                LogMessage($"   Class Name Entropy   : {r.ClassNameEntropy:F4} bits");
                LogMessage($"   Avg String Length    : {r.AvgStringLength:F1} chars");
                LogMessage($"   Unique String Ratio  : {r.UniqueStringRatio:P1}");
                if (r.Base64StringCount > 0)
                    LogMessage($"   Base64 Strings       : {r.Base64StringCount} (مرشح للتشفير)");
                if (r.HexStringCount > 0)
                    LogMessage($"   Hex Strings          : {r.HexStringCount} (DexGuard signature)");

                // ── Obfuscation Detection ───────────────────────────────────
                string detectedTool = r.ObfuscationLevel switch
                {
                    ObfuscationLevel.DexGuard  => "🔴 DexGuard (Commercial — Enterprise)",
                    ObfuscationLevel.R8Full    => "🟠 R8 Full Mode (Google — تشفير كامل)",
                    ObfuscationLevel.ProGuard  => "🟡 ProGuard / R8 Standard",
                    ObfuscationLevel.Light     => "🟢 تشفير خفيف (خيارات محدودة)",
                    ObfuscationLevel.None      => "⚪ بدون تشفير (Debug Build)",
                    _                           => "❔ غير معروف"
                };

                LogMessage($"\n🔍 Obfuscation Detection:");
                LogMessage($"   الأداة المكتشفة      : {detectedTool}");
                LogMessage($"   Short Names Ratio    : {r.ShortClassNameRatio:P1}  (ProGuard مؤشر > 30%)");
                LogMessage($"   Descriptive Names    : {r.DescriptiveClassNameRatio:P1}");
                if (r.UnicodeClassNameCount > 0)
                    LogMessage($"   ⚠ Unicode Class Names: {r.UnicodeClassNameCount} (DexGuard مؤكّد)");

                // ── Android 36 @FlaggedApi ─────────────────────────────────
                LogMessage($"\n⚡ Android 36 @FlaggedApi (من android-36 sources):");
                LogMessage($"   إجمالي APIs مع @FlaggedApi في SDK: {r.FlaggedApisInSdk.Count}");
                LogMessage($"   APIs مستخدمة في هذا APK          : {r.FlaggedApisUsedByApk.Count}");

                if (r.FlaggedApisUsedByApk.Any())
                {
                    LogMessage($"\n   ✅ @FlaggedApi المستخدمة في التطبيق:");
                    foreach (var api in r.FlaggedApisUsedByApk
                        .OrderBy(a => a.Category)
                        .Take(20))
                    {
                        string badges = (api.IsBaklava ? "[Baklava]" : "") +
                                        (api.IsSystemApi ? "[SystemAPI]" : "");
                        string flagInfo = !string.IsNullOrEmpty(api.FlagName)
                            ? $"\n       flag: {api.FlagName}"
                            : "";
                        LogMessage($"      • [{api.Category}] {api.ClassName} {badges}{flagInfo}");
                    }
                }
                else
                {
                    LogMessage("   ℹ التطبيق لا يستخدم @FlaggedApi من Android 36 حتى الآن");
                }

                // ── البصمة الرقمية ─────────────────────────────────────────
                LogMessage($"\n🆔 DEX Research Fingerprint:");
                LogMessage($"   Hash: {r.ResearchFingerprintHash}");
                LogMessage($"   (يمكن استخدامه لمقارنة إصدارات التطبيق أو اكتشاف التلاعب)");

                // ── الأخطاء ───────────────────────────────────────────────
                if (r.Errors.Any())
                {
                    LogMessage($"\n⚠ تحذيرات ({r.Errors.Count}):");
                    foreach (var err in r.Errors.Take(3))
                        LogMessage($"   • {err}");
                }

                LogMessage("\n──────────────────────────────────────────────────────────");
                LogMessage("✅ اكتمل DEX Entropy Fingerprinting بنجاح");
                LogMessage("──────────────────────────────────────────────────────────\n");

                // حفظ التقرير
                SaveEntropyReport(r);
            });
        }

        private void SaveEntropyReport(EntropyFingerprintReport r)
        {
            try
            {
                string timestamp  = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string reportPath = Path.Combine(_outputDir,
                    $"entropy_fingerprint_{r.ApkName}_{timestamp}.txt");

                var sb = new StringBuilder();
                sb.AppendLine("════════════════════════════════════════════════════════════");
                sb.AppendLine(" DEX Entropy & Obfuscation Fingerprinter Report");
                sb.AppendLine(" Aleppo University Research Project — Android 36 (API 36)");
                sb.AppendLine($" Date  : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($" APK   : {r.ApkPath}");
                sb.AppendLine($" Tool  : dexdump.exe build-tools/36.1.0");
                sb.AppendLine("════════════════════════════════════════════════════════════");
                sb.AppendLine();
                sb.AppendLine("[DEX Files]");
                foreach (var (p, s) in r.DexFiles)
                    sb.AppendLine($"  {Path.GetFileName(p)}  |  {s / 1024.0:F1} KB");
                sb.AppendLine();
                sb.AppendLine("[Code Statistics]");
                sb.AppendLine($"  Classes            : {r.AllClassNames.Count:N0}");
                sb.AppendLine($"  Methods            : {r.AllMethodNames.Count:N0}");
                sb.AppendLine($"  String Pool        : {r.StringPool.Count:N0}");
                sb.AppendLine($"  Unique Strings     : {r.StringFrequency.Count:N0}");
                sb.AppendLine($"  Native Methods     : {r.NativeMethodCount}");
                sb.AppendLine($"  Reflection Calls   : {r.ReflectionCallCount}");
                sb.AppendLine($"  Encrypted Str Calls: {r.EncryptedStringCallCount}");
                sb.AppendLine();
                sb.AppendLine("[Shannon Entropy]");
                sb.AppendLine($"  String Pool Entropy: {r.StringPoolCharEntropy:F6} bits");
                sb.AppendLine($"  Class Name Entropy : {r.ClassNameEntropy:F6} bits");
                sb.AppendLine($"  Avg String Length  : {r.AvgStringLength:F2}");
                sb.AppendLine($"  Unique String Ratio: {r.UniqueStringRatio:P2}");
                sb.AppendLine($"  Base64 Strings     : {r.Base64StringCount}");
                sb.AppendLine($"  Hex Strings        : {r.HexStringCount}");
                sb.AppendLine();
                sb.AppendLine("[Obfuscation Detection]");
                sb.AppendLine($"  Detected Tool      : {r.ObfuscationLevel}");
                sb.AppendLine($"  Short Names Ratio  : {r.ShortClassNameRatio:P2}");
                sb.AppendLine($"  Descriptive Ratio  : {r.DescriptiveClassNameRatio:P2}");
                sb.AppendLine($"  Unicode Names      : {r.UnicodeClassNameCount}");
                sb.AppendLine();
                sb.AppendLine($"[Android 36 @FlaggedApi]");
                sb.AppendLine($"  Total in SDK            : {r.FlaggedApisInSdk.Count}");
                sb.AppendLine($"  Used by APK             : {r.FlaggedApisUsedByApk.Count}");
                foreach (var api in r.FlaggedApisUsedByApk)
                    sb.AppendLine($"  [{api.Category}] {api.ClassName}  |  flag:{api.FlagName}  |  Baklava:{api.IsBaklava}");
                sb.AppendLine();
                sb.AppendLine("[DEX Research Fingerprint]");
                sb.AppendLine($"  Hash        : {r.ResearchFingerprintHash}");
                sb.AppendLine($"  Components  : {r.FingerprintComponents}");

                File.WriteAllText(reportPath, sb.ToString(), Encoding.UTF8);
                LogMessage($"💾 تقرير Entropy محفوظ: {Path.GetFileName(reportPath)}");
            }
            catch (Exception ex)
            {
                LogMessage($"  ⚠ فشل حفظ التقرير: {ex.Message}");
            }
        }

        #endregion // DEX Entropy & Obfuscation Fingerprinter

        // ══════════════════════════════════════════════════════════════════════
        // 🕵️ C2 Panel Detector — كاشف لوحات التحكم والاتصال الخفي
        // ══════════════════════════════════════════════════════════════════════
        //
        // يعتمد على الأدوات الحقيقية الموجودة:
        //   1. dexdump.exe (build-tools/36.1.0) — استخراج const-strings من DEX
        //   2. apktool (موجود مسبقاً) — فك الـ APK للوصول لملفات Smali
        //   3. android-36/android/net/ — APIs الشبكية الجديدة في Android 36
        //
        // ما يكشفه:
        //   A. IP/Host الخادم (C2 Server)
        //   B. المنفذ (Port) التحكم
        //   C. مفاتيح الاتصال (Token / AES Key / DeviceID / AuthKey)
        //   D. نوع البروتوكول (TCP/WebSocket/HTTP/MQTT/gRPC/DNS)
        //   E. Android 36 Covert Channels (L2CAP BLE، Thread IoT، Nearby)
        //   F. درجة خطورة (Confidence Score) لكل عنصر مكتشف
        // ══════════════════════════════════════════════════════════════════════

        #region C2 Panel Detector

        private async void BtnC2Detector_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_sourceApkPath) || !File.Exists(_sourceApkPath))
            {
                WpfMessageBox.Show("اختر ملف APK أولاً.", "تنبيه",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            btnC2Detector.IsEnabled = false;
            SetStatus("جاري C2 Panel Detection...");
            LogMessage("\n╔══════════════════════════════════════════════════════════╗");
            LogMessage("║  🕵️ C2 Panel Detector — كاشف لوحات التحكم الخفية       ║");
            LogMessage("║  Aleppo University Research — Android 36 (API 36)        ║");
            LogMessage("╚══════════════════════════════════════════════════════════╝");
            LogMessage($"   APK: {Path.GetFileName(_sourceApkPath)}");
            LogMessage($"   الأدوات: dexdump 36.1.0 + apktool + android-36 net APIs");

            string workDir = Path.Combine(_workDir, $"c2_{Guid.NewGuid():N}");

            try
            {
                var report = await Task.Run(() => RunC2Detection(workDir));
                DisplayC2Report(report);
            }
            catch (Exception ex)
            {
                LogMessage($"❌ خطأ في C2 Detector: {ex.Message}");
                SetStatus("خطأ");
            }
            finally
            {
                try { if (Directory.Exists(workDir)) Directory.Delete(workDir, true); } catch { }
                btnC2Detector.IsEnabled = true;
                SetStatus("اكتمل C2 Panel Detection");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  المحرك الرئيسي للكشف
        // ════════════════════════════════════════════════════════════════════

        private C2DetectionReport RunC2Detection(string workDir)
        {
            var report = new C2DetectionReport
            {
                ApkPath = _sourceApkPath!,
                ApkName = Path.GetFileNameWithoutExtension(_sourceApkPath!)
            };
            Directory.CreateDirectory(workDir);

            // ─── Step 1: استخراج DEX واستخدام dexdump الحقيقي ───────────────
            LogMessage("\n📦 [1/5] استخراج DEX وتشغيل dexdump.exe (build-tools/36.1.0)...");
            var rawStrings = ExtractStringsViaDexdump(workDir, report);

            // ─── Step 2: فك الـ APK بـ apktool للوصول لملفات Smali ───────────
            LogMessage("🔨 [2/5] فك APK بـ apktool للوصول لـ Smali...");
            string decompDir = DecompileForC2(workDir, report);

            // ─── Step 3: مسح الـ Smali للـ Sockets والـ Connections ──────────
            LogMessage("🔍 [3/5] مسح Smali لكشف Socket connections وC2 patterns...");
            ScanSmaliForC2(decompDir, rawStrings, report);

            // ─── Step 4: مسح الـ Resources والـ Assets ───────────────────────
            LogMessage("📄 [4/5] مسح Resources وAssets للـ Config files...");
            ScanResourcesForC2(decompDir, report);

            // ─── Step 5: كشف Android 36 Covert Channels ──────────────────────
            LogMessage("⚡ [5/5] كشف Android 36 Covert Channels (L2CAP، Thread، Nearby)...");
            DetectAndroid36CovertChannels(rawStrings, decompDir, report);

            return report;
        }

        // ════════════════════════════════════════════════════════════════════
        //  Step 1: استخراج Strings من DEX عبر dexdump الحقيقي
        // ════════════════════════════════════════════════════════════════════

        private List<string> ExtractStringsViaDexdump(string workDir, C2DetectionReport report)
        {
            var strings = new List<string>();

            if (!File.Exists(_dexdumpPath))
            {
                report.Errors.Add("dexdump.exe غير موجود");
                return strings;
            }

            try
            {
                // استخراج DEX مباشرة من APK ZIP
                using var zip = System.IO.Compression.ZipFile.OpenRead(report.ApkPath);
                var dexEntries = zip.Entries
                    .Where(e => e.Name.StartsWith("classes") && e.Name.EndsWith(".dex"))
                    .OrderBy(e => e.Name)
                    .ToList();

                foreach (var entry in dexEntries)
                {
                    string dexPath = Path.Combine(workDir, entry.Name);
                    entry.ExtractToFile(dexPath, overwrite: true);

                    var psi = new ProcessStartInfo
                    {
                        FileName               = _dexdumpPath,
                        Arguments              = $"-d \"{dexPath}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError  = true,
                        UseShellExecute        = false,
                        CreateNoWindow         = true,
                        StandardOutputEncoding = Encoding.UTF8
                    };

                    using var proc = Process.Start(psi)!;

                    // ✅ إصلاح: قراءة stderr بـ Task منفصل لمنع deadlock
                    var stderrConsumer = proc.StandardError.ReadToEndAsync();
                    int dexStringsBefore = strings.Count;

                    string? line;
                    while ((line = proc.StandardOutput.ReadLine()) != null)
                    {
                        line = line.TrimStart();
                        if (line.Length < 15) continue;

                        // ✅ إصلاح Regex: نطابق الصيغة الدقيقة من dexdump:
                        //   "const-string v0, "value""   أو
                        //   "const-string/jumbo p1, "value""
                        // Register name: [vp]\d+ (v0..v255, p0..p255)
                        var m = Regex.Match(line,
                            @"^const-string(?:/jumbo)?\s+[vp]\d+,\s+""(.*)""\s*$");
                        if (m.Success)
                        {
                            string val = m.Groups[1].Value;
                            if (val.Length >= 2)
                            {
                                strings.Add(val);
                                AnalyzeRawStringForC2(val, $"DEX/{entry.Name}", report);
                            }
                        }
                    }
                    proc.WaitForExit(90_000);
                    _ = stderrConsumer.Result; // تأكد من إنهاء stderr

                    // ✅ إصلاح: نُظهر عدد strings هذا DEX تحديداً (وليس المجموع الكلي)
                    int addedFromThisDex = strings.Count - dexStringsBefore;
                    LogMessage($"  ✓ {entry.Name}: {addedFromThisDex:N0} string" +
                               $" (المجموع: {strings.Count:N0})");
                }

            }
            catch (Exception ex)
            {
                report.Errors.Add($"dexdump: {ex.Message}");
                LogMessage($"  ⚠ {ex.Message}");
            }

            LogMessage($"  📊 إجمالي الـ strings: {strings.Count:N0}");
            return strings;
        }

        // ════════════════════════════════════════════════════════════════════
        //  تحليل String مباشر لكشف C2 indicators
        // ════════════════════════════════════════════════════════════════════

        private static void AnalyzeRawStringForC2(
            string val, string source, C2DetectionReport report)
        {
            if (string.IsNullOrWhiteSpace(val)) return;

            // ─── 1. IP Address مباشر (IPv4) ─────────────────────────────────
            // مثال: "192.168.1.1", "185.220.101.5"
            var ipv4Match = Regex.Match(val,
                @"^(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})$");
            if (ipv4Match.Success)
            {
                string ip = ipv4Match.Groups[1].Value;
                if (!IsPrivateIp(ip) && !report.RawIps.Contains(ip))
                {
                    report.RawIps.Add(ip);
                    report.Findings.Add(new C2Finding
                    {
                        ChannelType   = C2ChannelType.TcpSocket,
                        Host          = ip,
                        Port          = "",
                        FullValue     = val,
                        Source        = source,
                        Context       = "IPv4 مباشر في const-string",
                        Confidence    = 75
                    });
                }
                return;
            }

            // ─── 2. IP:Port معاً ─────────────────────────────────────────────
            // مثال: "185.220.101.5:4444", "c2.attacker.com:8080"
            var hostPortMatch = Regex.Match(val,
                @"^([\w\.\-]{3,100})[:：](\d{2,5})$");
            if (hostPortMatch.Success)
            {
                string host = hostPortMatch.Groups[1].Value;
                string port = hostPortMatch.Groups[2].Value;
                if (int.TryParse(port, out int portNum) &&
                    portNum is > 0 and < 65536)
                {
                    bool isIp      = Regex.IsMatch(host, @"^\d{1,3}(\.\d{1,3}){3}$");
                    bool isDomain  = host.Contains('.') && host.Length > 4;

                    if (isIp || isDomain)
                    {
                        if (!report.RawIps.Contains(host)) report.RawIps.Add(host);
                        if (!report.RawPorts.Contains(port)) report.RawPorts.Add(port);

                        int confidence = isIp ? 85 : 70;
                        // منافذ شائعة للـ C2
                        if (portNum is 1337 or 4444 or 4445 or 9001 or 9002 or
                            8443 or 8080 or 7777 or 6667 or 6666 or 7771 or 5555 or 31337)
                            confidence = 95;

                        report.Findings.Add(new C2Finding
                        {
                            ChannelType = isIp ? C2ChannelType.TcpSocket : C2ChannelType.HttpC2,
                            Host        = host,
                            Port        = port,
                            FullValue   = val,
                            Source      = source,
                            Context     = $"Host:Port في const-string [{(isIp ? "IP" : "Domain")}]",
                            Confidence  = confidence
                        });
                    }
                }
                return;
            }

            // ─── 3. WebSocket URL ────────────────────────────────────────────
            // مثال: "ws://c2.evil.com:4444/cmd", "wss://api.something.net/ws"
            if (Regex.IsMatch(val, @"^wss?://", RegexOptions.IgnoreCase))
            {
                var wsMatch = Regex.Match(val,
                    @"^wss?://([\w\.\-]+)(?::(\d+))?(/.*)?$",
                    RegexOptions.IgnoreCase);
                if (wsMatch.Success)
                {
                    string host = wsMatch.Groups[1].Value;
                    string port = wsMatch.Groups[2].Value;
                    if (!report.RawIps.Contains(host)) report.RawIps.Add(host);
                    if (!string.IsNullOrEmpty(port) && !report.RawPorts.Contains(port))
                        report.RawPorts.Add(port);
                    report.Findings.Add(new C2Finding
                    {
                        ChannelType = C2ChannelType.WebSocket,
                        Host        = host,
                        Port        = port,
                        FullValue   = val,
                        Source      = source,
                        Context     = "WebSocket C2 Channel",
                        Confidence  = 90
                    });
                }
                return;
            }

            // ─── 4. MQTT Broker URL ──────────────────────────────────────────
            // مثال: "mqtt://broker.hivemq.com:1883"
            // Android 36: L2CAP/Thread IoT يستخدمان MQTT
            if (Regex.IsMatch(val, @"^mqtts?://", RegexOptions.IgnoreCase))
            {
                var mqttMatch = Regex.Match(val,
                    @"^mqtts?://([\w\.\-]+)(?::(\d+))?",
                    RegexOptions.IgnoreCase);
                if (mqttMatch.Success)
                {
                    string host = mqttMatch.Groups[1].Value;
                    string port = mqttMatch.Groups[2].Value;
                    if (!report.RawIps.Contains(host)) report.RawIps.Add(host);
                    if (!string.IsNullOrEmpty(port) && !report.RawPorts.Contains(port))
                        report.RawPorts.Add(port);
                    report.Findings.Add(new C2Finding
                    {
                        ChannelType = C2ChannelType.Mqtt,
                        Host        = host,
                        Port        = port.Length > 0 ? port : "1883",
                        FullValue   = val,
                        Source      = source,
                        Context     = "MQTT Broker — IoT C2 Channel (Android 36 Thread/L2CAP)",
                        Confidence  = 88
                    });
                }
                return;
            }

            // ─── 5. gRPC Endpoint ────────────────────────────────────────────
            // مثال: "grpc.example.com:443", "dns:///grpc.example.com:50051"
            if (val.Contains("grpc") || val.Contains(":50051") || val.Contains(":50052"))
            {
                var grpcMatch = Regex.Match(val,
                    @"([\w\.\-]+):(\d{4,5})", RegexOptions.IgnoreCase);
                if (grpcMatch.Success)
                {
                    string host = grpcMatch.Groups[1].Value;
                    string port = grpcMatch.Groups[2].Value;
                    report.Findings.Add(new C2Finding
                    {
                        ChannelType = C2ChannelType.Grpc,
                        Host        = host,
                        Port        = port,
                        FullValue   = val,
                        Source      = source,
                        Context     = "gRPC Endpoint (Advanced RAT)",
                        Confidence  = 80
                    });
                }
                return;
            }

            // ─── 6. AES/XOR Key (مفتاح تشفير الاتصال) ──────────────────────
            // AES-128: string طولها 16 بالضبط من Hex أو Base64
            // AES-256: string طولها 32 بالضبط
            if (Regex.IsMatch(val, @"^[0-9A-Fa-f]{32}$") ||  // AES-128 hex
                Regex.IsMatch(val, @"^[0-9A-Fa-f]{64}$") ||  // AES-256 hex
                Regex.IsMatch(val, @"^[A-Za-z0-9+/]{24}={0,2}$")) // AES-128 base64
            {
                if (!report.RawKeys.Contains(val))
                {
                    report.RawKeys.Add(val);
                    report.Findings.Add(new C2Finding
                    {
                        ChannelType   = C2ChannelType.Unknown,
                        Host          = "",
                        Port          = "",
                        ConnectionKey = val,
                        FullValue     = val,
                        Source        = source,
                        Context       = "مفتاح AES محتمل (طول " + val.Length + " — HEX/Base64)",
                        Confidence    = 65
                    });
                }
                return;
            }

            // ─── 7. Telegram Bot Token (C2 عبر Telegram) ─────────────────────
            // مثال: "6543210987:AAGZ9TKFkfpLYq7zEfYnMOPQRSTUVWXYZaD"
            if (Regex.IsMatch(val, @"^\d{8,10}:[A-Za-z0-9_\-]{35}$"))
            {
                report.Findings.Add(new C2Finding
                {
                    ChannelType   = C2ChannelType.HttpC2,
                    Host          = "api.telegram.org",
                    Port          = "443",
                    ConnectionKey = val,
                    FullValue     = val,
                    Source        = source,
                    Context       = "Telegram Bot Token (C2 عبر Telegram API)",
                    Confidence    = 95
                });
                return;
            }

            // ─── 8. Discord Webhook (C2 عبر Discord) ─────────────────────────
            if (val.Contains("discord.com/api/webhooks/") ||
                val.Contains("discordapp.com/api/webhooks/"))
            {
                report.Findings.Add(new C2Finding
                {
                    ChannelType   = C2ChannelType.HttpC2,
                    Host          = "discord.com",
                    Port          = "443",
                    ConnectionKey = val,
                    FullValue     = val,
                    Source        = source,
                    Context       = "Discord Webhook C2",
                    Confidence    = 92
                });
                return;
            }

            // ─── 9. Ngrok / Serveo Tunnels (C2 عبر Tunnel) ───────────────────
            if (Regex.IsMatch(val, @"\.(ngrok(\-free)?\.app|trycloudflare\.com|serveo\.net)",
                RegexOptions.IgnoreCase))
            {
                var tunnelMatch = Regex.Match(val,
                    @"(https?://[\w\.\-]+(?::\d+)?)", RegexOptions.IgnoreCase);
                if (tunnelMatch.Success)
                {
                    report.Findings.Add(new C2Finding
                    {
                        ChannelType = C2ChannelType.HttpC2,
                        Host        = tunnelMatch.Groups[1].Value,
                        Port        = val.Contains(":") ? val.Split(':').LastOrDefault() ?? "443" : "443",
                        FullValue   = val,
                        Source      = source,
                        Context     = "Tunnel C2 (Ngrok/Cloudflare/Serveo) — حماية هوية Server",
                        Confidence  = 88
                    });
                }
                return;
            }

            // ─── 10. DNS Tunneling Pattern ────────────────────────────────────
            // مثال: ".dnscat.example.com", طلبات TXT records
            if (Regex.IsMatch(val, @"(dnscat|dns-exfil|dns-c2|\.exfil\.|dns\.tunnel)",
                RegexOptions.IgnoreCase))
            {
                report.Findings.Add(new C2Finding
                {
                    ChannelType = C2ChannelType.DnsC2,
                    Host        = val,
                    FullValue   = val,
                    Source      = source,
                    Context     = "DNS Tunneling C2 — يخترق Firewalls عبر DNS queries",
                    Confidence  = 90
                });
                return;
            }

            // ─── 11. Device Auth Token (مفتاح تسجيل الجهاز بلوحة التحكم) ──
            // مثال: "device_token", "auth_key", "secret_key", "device_id"
            if (Regex.IsMatch(val,
                @"(device[_\-]?(token|key|id|secret)|auth[_\-]?(key|token)|" +
                @"secret[_\-]?(key|token|code)|panel[_\-]?(pass|key|secret)|" +
                @"connection[_\-]?(key|token|id)|bot[_\-]?(token|key))",
                RegexOptions.IgnoreCase) && val.Length < 120)
            {
                if (!report.RawKeys.Contains(val))
                    report.RawKeys.Add(val);
            }
        }

        // ═══ Helper: هل IP خاص (private)؟ ════════════════════════════════════
        private static bool IsPrivateIp(string ip)
        {
            if (!System.Net.IPAddress.TryParse(ip, out var addr)) return true;
            byte[] b = addr.GetAddressBytes();
            return b[0] == 10 ||
                   (b[0] == 172 && b[1] >= 16 && b[1] <= 31) ||
                   (b[0] == 192 && b[1] == 168) ||
                   b[0] == 127;
        }

        // ════════════════════════════════════════════════════════════════════
        //  Step 2: فك APK بـ apktool للوصول لـ Smali
        // ════════════════════════════════════════════════════════════════════

        private string DecompileForC2(string workDir, C2DetectionReport report)
        {
            string decompDir = Path.Combine(workDir, "decompiled");
            try
            {
                // ✅ إصلاح deadlock: يجب قراءة stdout+stderr بشكل متوازٍ
                // إذا لم نقرأها، يمتلئ الـ buffer ويخمد apktool للأبد
                string apktoolArgs = $"d \"{report.ApkPath}\" -o \"{decompDir}\" -f --frame-tag 36";
                var psi = new ProcessStartInfo
                {
                    FileName               = _apktoolPath,
                    Arguments              = apktoolArgs,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                using var proc = Process.Start(psi)!;

                // قراءة stdout و stderr بالتوازي لمنع deadlock
                var stdoutTask = proc.StandardOutput.ReadToEndAsync();
                var stderrTask = proc.StandardError.ReadToEndAsync();

                bool exited = proc.WaitForExit(150_000); // 2.5 دقيقة
                string stdout = stdoutTask.Result;
                string stderr = stderrTask.Result;

                if (!exited)
                {
                    try { proc.Kill(true); } catch { }
                    report.Errors.Add("apktool timeout — تجاوز 150 ثانية");
                    LogMessage("  ⚠ apktool timeout — سيتم الاستمرار بـ DEX strings فقط");
                }
                else if (Directory.Exists(decompDir))
                {
                    bool hasSmali = Directory.EnumerateFiles(decompDir, "*.smali",
                        SearchOption.AllDirectories).Any();
                    LogMessage($"  ✓ تم فك APK بنجاح → {Path.GetFileName(decompDir)}" +
                               (hasSmali ? " (يحتوي Smali)" : " (بدون Smali — قد يكون APK محميًا)"));
                    if (!string.IsNullOrWhiteSpace(stderr) && stderr.Contains("ERROR"))
                        LogMessage($"  ⚠ apktool تحذير: {stderr.Split('\n')[0].Trim()}");
                }
                else
                {
                    string hint = stderr.Length > 0
                        ? stderr.Split('\n')[0].Trim()
                        : stdout.Split('\n').FirstOrDefault(l => l.Contains("ERROR") || l.Contains("Exception"))
                          ?? "(لا رسالة خطأ)";
                    LogMessage($"  ⚠ apktool لم يُنشئ مخرجات: {hint}");
                    report.Errors.Add($"apktool لا مخرجات: {hint}");
                }
            }
            catch (Exception ex)
            {
                report.Errors.Add($"apktool: {ex.Message}");
                LogMessage($"  ⚠ apktool: {ex.Message} — سيتم الاستمرار بدون Smali");
            }
            return decompDir;
        }

        // ════════════════════════════════════════════════════════════════════
        //  Step 3: مسح ملفات Smali
        // ════════════════════════════════════════════════════════════════════

        private void ScanSmaliForC2(
            string decompDir, List<string> rawStrings, C2DetectionReport report)
        {
            if (!Directory.Exists(decompDir))
            {
                // Fallback: نعيد فحص rawStrings فقط
                LogMessage("  ℹ Smali غير متاح — سيتم الاكتفاء بتحليل DEX strings");
                return;
            }

            int smaliFiles = 0;
            int c2Hits     = 0;

            try
            {
                foreach (var smaliFile in Directory.EnumerateFiles(decompDir, "*.smali",
                    SearchOption.AllDirectories).Take(5000))
                {
                    smaliFiles++;
                    c2Hits += ScanSingleSmaliForC2(smaliFile, report);
                }
            }
            catch (Exception ex)
            {
                report.Errors.Add($"Smali scan: {ex.Message}");
            }

            LogMessage($"  ✓ فُحص {smaliFiles:N0} ملف Smali | {c2Hits} إشارة C2");
        }


        // ════════════════════════════════════════════════════════════════════
        //  ✅ ParseSmaliConstStrings — Helper موحّد لاستخراج const-strings
        //  يقرأ ملف Smali سطراً بسطر ويُرجع (value, context)
        //  context = "ClassName.methodName" للتشخيص الدقيق
        //  يدعم: const-string v0, "value" و const-string/jumbo p1, "value"
        // ════════════════════════════════════════════════════════════════════
        private static List<(string value, string context)> ParseSmaliConstStrings(string smaliContent)
        {
            var results       = new List<(string, string)>();
            string currentClass  = "";
            string currentMethod = "";

            foreach (string rawLine in smaliContent.Split('\n'))
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith('#')) continue;

                // تتبع اسم الكلاس
                if (line.StartsWith(".class "))
                {
                    var cm = Regex.Match(line, @"\.class\s+[\w\s]*L([^;]+);");
                    if (cm.Success) currentClass = cm.Groups[1].Value.Split('/').Last();
                    continue;
                }

                // تتبع اسم الميثود
                if (line.StartsWith(".method "))
                {
                    var mm = Regex.Match(line, @"\.method\s+[\w\s]+(\S+)\(");
                    if (mm.Success) currentMethod = mm.Groups[1].Value;
                    continue;
                }
                if (line == ".end method") { currentMethod = ""; continue; }

                // ✅ الصيغة الحقيقية لـ const-string في Smali:
                //   const-string v0, "some value here"
                //   const-string/jumbo p1, "another value"
                // ملاحظة: الـ register يبدأ بـ v أو p متبوعاً برقم
                var sm = Regex.Match(line,
                    @"^const-string(?:/jumbo)?\s+[vp]\d+,\s+""(.*)""\s*$");
                if (sm.Success)
                {
                    string val = sm.Groups[1].Value;
                    string ctx = string.IsNullOrEmpty(currentMethod)
                        ? currentClass
                        : $"{currentClass}.{currentMethod}";
                    results.Add((val, ctx));
                }
            }
            return results;
        }

        private static int ScanSingleSmaliForC2(string smaliPath, C2DetectionReport report)
        {
            int hits = 0;
            try
            {
                string content  = File.ReadAllText(smaliPath, Encoding.UTF8);
                string fileName = Path.GetFileName(smaliPath);

                // ✅ استخراج كل الـ strings بـ parser الصحيح مرة واحدة
                var allStrings = ParseSmaliConstStrings(content);

                // ─── كشف Socket مباشر (java.net.Socket) ────────────────────
                if (content.Contains("Ljava/net/Socket;") ||
                    content.Contains("Ljava/net/ServerSocket;"))
                {
                    // ✅ إصلاح: نبحث في allStrings الصحيحة بدلاً من Regex الخاطئة
                    var ipStrings = allStrings
                        .Where(s => Regex.IsMatch(s.value,
                            @"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$"))
                        .ToList();

                    var portStrings = allStrings
                        .Where(s => Regex.IsMatch(s.value, @"^\d{2,5}$") &&
                                    int.TryParse(s.value, out int pn) && pn is > 0 and < 65536)
                        .ToList();

                    foreach (var (ipVal, ipCtx) in ipStrings)
                    {
                        if (IsPrivateIp(ipVal)) continue;
                        if (!report.RawIps.Contains(ipVal)) report.RawIps.Add(ipVal);

                        string port = portStrings.FirstOrDefault().value ?? "";
                        if (!string.IsNullOrEmpty(port) && !report.RawPorts.Contains(port))
                            report.RawPorts.Add(port);

                        report.Findings.Add(new C2Finding
                        {
                            ChannelType = C2ChannelType.TcpSocket,
                            Host        = ipVal,
                            Port        = port,
                            FullValue   = string.IsNullOrEmpty(port) ? ipVal : $"{ipVal}:{port}",
                            Source      = $"Smali/{fileName}",
                            Context     = $"java.net.Socket — TCP C2 connection [{ipCtx}]",
                            Confidence  = 85
                        });
                        hits++;
                    }
                }

                // ─── كشف OkHttp / Retrofit / Volley (HTTP C2) ───────────────
                if (content.Contains("Lokhttp3/OkHttpClient;") ||
                    content.Contains("Lretrofit2/Retrofit;") ||
                    content.Contains("Lokhttp3/Request;") ||
                    content.Contains("Lcom/android/volley/"))
                {
                    foreach (var (urlVal, urlCtx) in allStrings)
                    {
                        // ✅ فلترة دقيقة: نأخذ فقط URLs حقيقية بـ http/https
                        if (!Regex.IsMatch(urlVal, @"^https?://[\w.\-]",
                            RegexOptions.IgnoreCase)) continue;
                        if (urlVal.Length < 10 || urlVal.Length > 300) continue;

                        var urlParsed = Regex.Match(urlVal,
                            @"https?://([\w.\-]+)(?::(\d+))?");
                        if (!urlParsed.Success) continue;

                        string host = urlParsed.Groups[1].Value;
                        string port = urlParsed.Groups[2].Value;
                        if (!report.RawDomains.Contains(host))
                            report.RawDomains.Add(host);

                        report.Findings.Add(new C2Finding
                        {
                            ChannelType = urlVal.StartsWith("https", StringComparison.OrdinalIgnoreCase)
                                          ? C2ChannelType.Https : C2ChannelType.HttpC2,
                            Host        = host,
                            Port        = port.Length > 0 ? port
                                        : urlVal.StartsWith("https", StringComparison.OrdinalIgnoreCase)
                                          ? "443" : "80",
                            FullValue   = urlVal,
                            Source      = $"Smali/{fileName}",
                            Context     = $"OkHttp/Retrofit Base URL [{urlCtx}]",
                            Confidence  = 72
                        });
                        hits++;
                    }
                }

                // ─── كشف WebSocket (OkHttp WebSocket) ───────────────────────
                if (content.Contains("Lokhttp3/WebSocket;") ||
                    content.Contains("Lokhttp3/WebSocketListener;"))
                {
                    foreach (var (wsVal, wsCtx) in allStrings)
                    {
                        if (!Regex.IsMatch(wsVal, @"^wss?://", RegexOptions.IgnoreCase)) continue;

                        var wsM = Regex.Match(wsVal,
                            @"^wss?://([\w.\-]+)(?::(\d+))?(/.*)?$", RegexOptions.IgnoreCase);
                        if (!wsM.Success) continue;

                        string wsHost = wsM.Groups[1].Value;
                        string wsPort = wsM.Groups[2].Value;
                        if (!report.RawIps.Contains(wsHost)) report.RawIps.Add(wsHost);

                        report.Findings.Add(new C2Finding
                        {
                            ChannelType = C2ChannelType.WebSocket,
                            Host        = wsHost,
                            Port        = wsPort.Length > 0 ? wsPort : "443",
                            FullValue   = wsVal,
                            Source      = $"Smali/{fileName}",
                            Context     = $"WebSocket C2 Channel [{wsCtx}]",
                            Confidence  = 90
                        });
                        hits++;
                    }
                }

                // ─── كشف Reverse Shell Pattern ──────────────────────────────
                if (content.Contains("Ljava/lang/Runtime;->exec") ||
                    content.Contains("Ljava/lang/ProcessBuilder;"))
                {
                    foreach (var (sVal, sCtx) in allStrings)
                    {
                        if (Regex.IsMatch(sVal,
                            @"(/bin/(sh|bash|cmd|sh)|nc\s+[\d.\w]+\s+\d+|cmd\.exe)",
                            RegexOptions.IgnoreCase))
                        {
                            report.Findings.Add(new C2Finding
                            {
                                ChannelType = C2ChannelType.ReverseShell,
                                Host        = "Runtime.exec",
                                FullValue   = sVal,
                                Source      = $"Smali/{fileName}",
                                Context     = $"Reverse Shell عبر Runtime.exec() — خطر شديد [{sCtx}]",
                                Confidence  = 95
                            });
                            hits++;
                            break;
                        }
                    }
                }

                // ─── كشف Static Key/Token Fields ────────────────────────────
                var keyFields = Regex.Matches(content,
                    @"\.field\s+(?:private\s+)?(?:public\s+)?(?:protected\s+)?" +
                    @"(?:static\s+)?(?:final\s+)?" +
                    @"(?:KEY|TOKEN|SECRET|AUTH|PASSWORD|PASSWD|DEVICE_ID|CLIENT_ID|API_KEY|ACCESS_KEY)" +
                    @"[^:]*:Ljava/lang/String;",
                    RegexOptions.IgnoreCase | RegexOptions.Multiline);

                foreach (Match kf in keyFields)
                {
                    // ✅ ابحث عن أقرب const-string بعد هذا الـ field declaration
                    string afterField = content.Length > kf.Index + kf.Length + 400
                        ? content.Substring(kf.Index + kf.Length, 400)
                        : content.Substring(kf.Index + kf.Length);

                    var valM = Regex.Match(afterField,
                        @"const-string(?:/jumbo)?\s+[vp]\d+,\s+""([^""]{8,128})""");
                    if (!valM.Success) continue;

                    string keyVal = valM.Groups[1].Value;
                    if (!report.RawKeys.Contains(keyVal))
                    {
                        report.RawKeys.Add(keyVal);
                        report.Findings.Add(new C2Finding
                        {
                            ChannelType   = C2ChannelType.Unknown,
                            ConnectionKey = keyVal,
                            FullValue     = $"{kf.Value.Trim()} = \"{keyVal}\"",
                            Source        = $"Smali/{fileName}",
                            Context       = "Static Key/Token field في Smali",
                            Confidence    = 80
                        });
                        hits++;
                    }
                }
            }
            catch { /* تجاهل ملفات المشكلة */ }

            return hits;
        }



        // ════════════════════════════════════════════════════════════════════
        //  Step 4: مسح Resources وAssets
        // ════════════════════════════════════════════════════════════════════

        private void ScanResourcesForC2(string decompDir, C2DetectionReport report)
        {
            if (!Directory.Exists(decompDir)) return;

            // ملفات الـ Config التي يخفيها RAT developers
            var configPatterns = new[] { "*.json", "*.xml", "*.cfg", "*.ini",
                                          "*.properties", "*.yaml", "*.yml",
                                          "*.conf", "*.txt" };
            int filesScanned = 0;

            foreach (var pattern in configPatterns)
            {
                foreach (var file in Directory.EnumerateFiles(decompDir, pattern,
                    SearchOption.AllDirectories).Take(300))
                {
                    filesScanned++;
                    ScanConfigFileForC2(file, report);
                }
            }
            LogMessage($"  ✓ فُحص {filesScanned} ملف Config/Resource");
        }

        private static void ScanConfigFileForC2(string filePath, C2DetectionReport report)
        {
            try
            {
                string content = File.ReadAllText(filePath, Encoding.UTF8);
                string fileName = Path.GetRelativePath(
                    Path.GetDirectoryName(filePath)!, filePath);

                // IP:Port في Config files
                foreach (Match m in Regex.Matches(content,
                    @"[""']?((?:\d{1,3}\.){3}\d{1,3})[""']?\s*[,:\|]\s*[""']?(\d{2,5})[""']?"))
                {
                    string ip   = m.Groups[1].Value;
                    string port = m.Groups[2].Value;
                    if (!IsPrivateIp(ip) && int.TryParse(port, out int pn) && pn is > 0 and < 65536)
                    {
                        if (!report.RawIps.Contains(ip)) report.RawIps.Add(ip);
                        if (!report.RawPorts.Contains(port)) report.RawPorts.Add(port);
                        report.Findings.Add(new C2Finding
                        {
                            ChannelType = C2ChannelType.TcpSocket,
                            Host        = ip,
                            Port        = port,
                            FullValue   = $"{ip}:{port}",
                            Source      = $"Config/{Path.GetFileName(filePath)}",
                            Context     = "IP:Port في ملف Config",
                            Confidence  = 80
                        });
                    }
                }

                // Server URL في JSON Config
                foreach (Match m in Regex.Matches(content,
                    @"[""'](server|host|c2|panel|endpoint|base_url|api_url|url)[""']\s*:\s*[""'](https?://[^""']+)[""']",
                    RegexOptions.IgnoreCase))
                {
                    string url = m.Groups[2].Value;
                    string key = m.Groups[1].Value;
                    var urlParsed = Regex.Match(url,
                        @"https?://([\w\.\-]+)(?::(\d+))?");
                    if (urlParsed.Success)
                    {
                        string host = urlParsed.Groups[1].Value;
                        string port = urlParsed.Groups[2].Value;
                        if (!report.RawDomains.Contains(host)) report.RawDomains.Add(host);
                        report.Findings.Add(new C2Finding
                        {
                            ChannelType = C2ChannelType.HttpC2,
                            Host        = host,
                            Port        = port.Length > 0 ? port : "443",
                            FullValue   = url,
                            Source      = $"Config/{Path.GetFileName(filePath)}",
                            Context     = $"JSON Config — key: \"{key}\"",
                            Confidence  = 85
                        });
                    }
                }

                // Token/Key في Config
                foreach (Match m in Regex.Matches(content,
                    @"[""'](token|key|secret|password|auth|pass|device_key|connection_key)[""']\s*:\s*[""']([^""']{6,256})[""']",
                    RegexOptions.IgnoreCase))
                {
                    string keyVal = m.Groups[2].Value;
                    if (!report.RawKeys.Contains(keyVal))
                    {
                        report.RawKeys.Add(keyVal);
                        report.Findings.Add(new C2Finding
                        {
                            ChannelType   = C2ChannelType.Unknown,
                            ConnectionKey = keyVal,
                            FullValue     = m.Value,
                            Source        = $"Config/{Path.GetFileName(filePath)}",
                            Context       = $"مفتاح اتصال في Config — key: \"{m.Groups[1].Value}\"",
                            Confidence    = 78
                        });
                    }
                }
            }
            catch { /* تجاهل */ }
        }

        // ════════════════════════════════════════════════════════════════════
        //  Step 5: كشف Android 36 Covert Channels
        //  مبني مباشرة على APIs من android-36/android/net/
        // ════════════════════════════════════════════════════════════════════

        private void DetectAndroid36CovertChannels(
            List<string> rawStrings, string decompDir, C2DetectionReport report)
        {
            // ── L2CAP BLE (android.net.L2capNetworkSpecifier — @FlaggedApi) ──
            // يُستخدم لإنشاء شبكة IPv6 مخفية عبر Bluetooth
            // المصدر: android-36/android/net/L2capNetworkSpecifier.java
            // PSM range: 0x80-0xFF (BLE dynamic PSM)
            bool usesL2cap =
                rawStrings.Any(s => s.Contains("L2capNetworkSpecifier") ||
                                    s.Contains("FLAG_IPV6_OVER_BLE") ||
                                    s.Contains("getPsm") ||
                                    s.Contains("ROLE_CLIENT")) ||
                (Directory.Exists(decompDir) &&
                 Directory.EnumerateFiles(decompDir, "*.smali", SearchOption.AllDirectories)
                          .Any(f =>
                          {
                              try { return File.ReadAllText(f).Contains("L2capNetworkSpecifier"); } catch { return false; }
                          }));

            if (usesL2cap)
            {
                report.Findings.Add(new C2Finding
                {
                    ChannelType = C2ChannelType.BluetoothL2cap,
                    Host        = "BLE L2CAP",
                    Port        = "PSM:0x80-0xFF",
                    FullValue   = "android.net.L2capNetworkSpecifier (API 36 @FlaggedApi)",
                    Source      = "Android 36 Covert Channel",
                    Context     = "IPv6 over BLE L2CAP — قناة اتصال مخفية عبر Bluetooth!\n" +
                                  "المصدر: android-36/android/net/L2capNetworkSpecifier.java\n" +
                                  "FLAG_IPV6_OVER_BLE — يتجاوز Firewalls ويُخفي الـ C2 traffic",
                    Confidence  = 85
                });
                LogMessage("  ⚠ Android 36 L2CAP BLE Channel مكتشف!");
            }

            // ── Thread IoT Network (android.net.thread — API 36) ──────────
            // يُستخدم لـ C2 عبر Matter/Thread Mesh network (Covert OOB Channel)
            // المصدر: android-36/android/net/thread/ThreadNetworkController.java
            // المصدر للبيانات: android-36/android/net/thread/ActiveOperationalDataset.java
            
            bool usesThread = false;
            string networkKey = "غير محدد";
            string threadDetails = "";

            // البحث المتقدم دخل ملفات Smali الخاصة بالـ APP فقط 
            if (Directory.Exists(decompDir))
            {
                 foreach(var f in Directory.EnumerateFiles(decompDir, "*.smali", SearchOption.AllDirectories))
                 {
                     try
                     {
                         string smaliContent = File.ReadAllText(f);
                         if (smaliContent.Contains("Landroid/net/thread/ThreadNetworkController;") ||
                             smaliContent.Contains("Landroid/net/thread/ActiveOperationalDataset;"))
                         {
                             usesThread = true;
                             // استخراج مفتاح الشبكة (16 Bytes -> 32 Hex chars)
                             // يبحث في كود Smali عن const-string التي يُرجح جداً أنها مفتاح Thread Network
                             var keyMatch = Regex.Match(smaliContent, @"const-string(?:/jumbo)?\s+\w+,\s+""([0-9a-fA-F]{32})""");
                             if (keyMatch.Success)
                             {
                                 networkKey = keyMatch.Groups[1].Value.ToUpper();
                                 threadDetails = $"Network Key Extracted: {networkKey}";
                             }
                             
                             // محاولة إيجاد اسم الـ Network Name إن وُجد
                             var nameMatch = Regex.Match(smaliContent, @"const-string(?:/jumbo)?\s+\w+,\s+""([a-zA-Z0-9_\-\.]{4,16})""");
                             if (nameMatch.Success && !Regex.IsMatch(nameMatch.Groups[1].Value, @"^[0-9]+$"))
                             {
                                  threadDetails += (threadDetails.Length > 0 ? " | " : "") + $"Net Name: {nameMatch.Groups[1].Value}";
                             }
                             
                             break;
                         }
                     }
                     catch { /* تجاهل الملفات المعطوبة */ }
                 }
            }

            // Fallback: بحث سطحي في الـ String Pool المستخرج 
            if (!usesThread)
            {
                usesThread = rawStrings.Any(s => s.Contains("ThreadNetworkController") ||
                                                 s.Contains("ActiveOperationalDataset"));
                if (usesThread)
                {
                    networkKey = rawStrings.FirstOrDefault(s => Regex.IsMatch(s, @"^[0-9A-Fa-f]{32}$")) ?? "غير محدد";
                    if (networkKey != "غير محدد") threadDetails = $"Possible Key: {networkKey.ToUpper()}";
                }
            }

            if (usesThread)
            {
                report.Findings.Add(new C2Finding
                {
                    ChannelType   = C2ChannelType.ThreadIot,
                    Host          = "Thread Mesh Network (Offline C2)",
                    Port          = "UDP:5683 (CoAP Over Thread)",
                    ConnectionKey = networkKey,
                    FullValue     = "android.net.thread.ThreadNetworkController (API 36)",
                    Source        = "Android 36 Covert Channel",
                    Context       = "🔒 Thread/Matter IoT Mesh C2 — قناة تحكم سرية جداً!\n" +
                                    "المصدر: android-36/android/net/thread/ThreadNetworkController.java\n" +
                                    "تستخدم هذه القناة أجهزة إنترنت الأشياء (IoT) المحيطة كجسر للاتصال بخادم الـ C2 دون المرور عبر شاشة الـ Wi-Fi أو بيانات الشريحة.\n" +
                                    (string.IsNullOrEmpty(threadDetails) ? "" : $"تفاصيل مسح Smali: {threadDetails}"),
                    Confidence    = 90
                });
                LogMessage("  🚨 Android 36 Thread IoT Mesh Channel تم اكتشافه باحترافية!");
            }

            // ── Nearby Connections API (android.nearby — API 36) ──────────
            // يُستخدم لـ C2 عبر Nearby Connections بدون إنترنت
            // المصدر: android-36/android/nearby/NearbyManager.java
            bool usesNearby =
                rawStrings.Any(s => s.Contains("NearbyManager") ||
                                    s.Contains("NearbyDevice") ||
                                    s.Contains("ScanRequest")) ||
                (Directory.Exists(decompDir) &&
                 Directory.EnumerateFiles(decompDir, "*.smali", SearchOption.AllDirectories)
                          .Any(f =>
                          {
                              try { return File.ReadAllText(f).Contains("NearbyManager"); } catch { return false; }
                          }));

            if (usesNearby)
            {
                report.Findings.Add(new C2Finding
                {
                    ChannelType = C2ChannelType.NearbyApi,
                    Host        = "Nearby Connections",
                    Port        = "P2P (WiFi/BT)",
                    FullValue   = "android.nearby.NearbyManager (API 36)",
                    Source      = "Android 36 Covert Channel",
                    Context     = "Nearby Connections C2 — P2P بدون إنترنت!\n" +
                                  "المصدر: android-36/android/nearby/NearbyManager.java\n" +
                                  "يُمكّن C2 بدون Network connection — صعب الاكتشاف",
                    Confidence  = 78
                });
                LogMessage("  ⚠ Android 36 Nearby API Channel مكتشف!");
            }

            int android36Found = (usesL2cap ? 1 : 0) + (usesThread ? 1 : 0) + (usesNearby ? 1 : 0);
            LogMessage($"  ✓ Android 36 Covert Channels: {android36Found} مكتشف");
        }

        // ════════════════════════════════════════════════════════════════════
        //  عرض النتائج
        // ════════════════════════════════════════════════════════════════════

        private void DisplayC2Report(C2DetectionReport r)
        {
            Dispatcher.Invoke(() =>
            {
                LogMessage("\n╔══════════════════════════════════════════════════════════╗");
                LogMessage("║         🕵️ تقرير C2 Panel Detection                     ║");
                LogMessage("╚══════════════════════════════════════════════════════════╝");

                // ── حكم عام ───────────────────────────────────────────────
                string verdict;
                string verdictIcon;
                if (r.ThreatScore >= 80)       { verdict = "خطر شديد — C2 Panel مؤكد"; verdictIcon = "🔴"; }
                else if (r.ThreatScore >= 60)  { verdict = "مشتبه به — إشارات C2 واضحة"; verdictIcon = "🟠"; }
                else if (r.ThreatScore >= 40)  { verdict = "مشتبه ضعيف — يحتاج تحقق"; verdictIcon = "🟡"; }
                else if (r.Findings.Any())     { verdict = "علامات تحذيرية — غير مؤكد"; verdictIcon = "🟢"; }
                else                            { verdict = "لا إشارات C2 واضحة"; verdictIcon = "✅"; }

                LogMessage($"\n{verdictIcon} الحكم: {verdict}");
                LogMessage($"   Threat Score: {r.ThreatScore}/100  |  الإشارات: {r.Findings.Count}");

                // ── الـ IPs والـ Hosts المكتشفة ────────────────────────────
                if (r.RawIps.Any() || r.RawDomains.Any())
                {
                    LogMessage($"\n🖥️  C2 Servers المكتشفة:");
                    foreach (var ip in r.RawIps.Distinct().Take(20))
                        LogMessage($"   📍 IP: {ip}");
                    foreach (var d in r.RawDomains.Distinct().Take(20))
                        LogMessage($"   🌐 Domain: {d}");
                }
                else
                    LogMessage("\n🖥️  لم تُكتشف IPs أو Domains C2");

                // ── المنافذ ────────────────────────────────────────────────
                if (r.RawPorts.Any())
                {
                    LogMessage($"\n📡 المنافذ ({r.RawPorts.Distinct().Count()}):");
                    LogMessage($"   {string.Join(" | ", r.RawPorts.Distinct().Take(15))}");
                }

                // ── مفاتيح الاتصال ─────────────────────────────────────────
                if (r.RawKeys.Any())
                {
                    LogMessage($"\n🔑 مفاتيح الاتصال ({r.RawKeys.Count}):");
                    foreach (var key in r.RawKeys.Take(10))
                    {
                        // إخفاء جزء من المفتاح (أمان)
                        string display = key.Length > 20
                            ? key[..8] + "..." + key[^6..]
                            : key;
                        LogMessage($"   🔐 {display}  (طول: {key.Length})");
                    }
                }

                // ── تفاصيل الإشارات مجمّعة حسب النوع ─────────────────────
                if (r.Findings.Any())
                {
                    LogMessage($"\n📋 تفاصيل الإشارات ({r.Findings.Count}) — مرتبة بالثقة:");

                    foreach (var grp in r.Findings
                        .OrderByDescending(f => f.Confidence)
                        .GroupBy(f => f.ChannelType))
                    {
                        string chIcon = grp.Key switch
                        {
                            C2ChannelType.TcpSocket      => "🔌",
                            C2ChannelType.WebSocket       => "🔌",
                            C2ChannelType.HttpC2          => "🌐",
                            C2ChannelType.Https           => "🔒",
                            C2ChannelType.Mqtt            => "📶",
                            C2ChannelType.Grpc            => "⚙️",
                            C2ChannelType.DnsC2           => "🔤",
                            C2ChannelType.BluetoothL2cap  => "📶",
                            C2ChannelType.ThreadIot        => "🌐",
                            C2ChannelType.NearbyApi       => "📡",
                            C2ChannelType.ReverseShell    => "💀",
                            _                              => "❓"
                        };

                        LogMessage($"\n  {chIcon} {grp.Key} ({grp.Count()}):");
                        foreach (var f in grp.Take(5))
                        {
                            string hostPort = string.IsNullOrEmpty(f.Host) ? "" :
                                             string.IsNullOrEmpty(f.Port) ? f.Host :
                                             $"{f.Host}:{f.Port}";
                            string keyInfo = string.IsNullOrEmpty(f.ConnectionKey) ? "" :
                                             $" | Key: {f.ConnectionKey[..Math.Min(12, f.ConnectionKey.Length)]}...";
                            LogMessage($"    [{f.Confidence}%] {hostPort}{keyInfo}");
                            LogMessage($"       📂 {f.Source}");
                            LogMessage($"       ℹ {f.Context.Split('\n')[0]}");
                        }
                    }
                }
                else
                {
                    LogMessage("\n✅ لا توجد إشارات C2 محددة في هذا التطبيق");
                }

                // ── Android 36 Covert Channels ──────────────────────────────
                var android36Findings = r.Findings.Where(f =>
                    f.ChannelType is C2ChannelType.BluetoothL2cap or
                                    C2ChannelType.ThreadIot or
                                    C2ChannelType.NearbyApi).ToList();
                if (android36Findings.Any())
                {
                    LogMessage($"\n⚡ تحذير Android 36 Covert Channels ({android36Findings.Count}):");
                    foreach (var f in android36Findings)
                        LogMessage($"   ⚠ {f.ChannelType}: {f.Context.Split('\n')[0]}");
                }

                // ── الأخطاء ───────────────────────────────────────────────
                if (r.Errors.Any())
                {
                    LogMessage($"\n⚠ تحذيرات ({r.Errors.Count}):");
                    foreach (var err in r.Errors.Take(3))
                        LogMessage($"   • {err}");
                }

                LogMessage("\n──────────────────────────────────────────────────────────");
                LogMessage("✅ اكتمل C2 Panel Detection بنجاح");
                LogMessage("──────────────────────────────────────────────────────────\n");

                // حفظ التقرير
                SaveC2Report(r);
            });
        }

        private void SaveC2Report(C2DetectionReport r)
        {
            try
            {
                string timestamp  = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string reportPath = Path.Combine(_outputDir,
                    $"c2_detection_{r.ApkName}_{timestamp}.txt");

                var sb = new StringBuilder();
                sb.AppendLine("════════════════════════════════════════════════════════════");
                sb.AppendLine(" C2 Panel Detection Report");
                sb.AppendLine(" Aleppo University Research Project — Android 36 (API 36)");
                sb.AppendLine($" Date  : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($" APK   : {r.ApkPath}");
                sb.AppendLine($" ThreatScore: {r.ThreatScore}/100  |  IsLikelyC2: {r.IsLikelyC2}");
                sb.AppendLine("════════════════════════════════════════════════════════════");
                sb.AppendLine();
                sb.AppendLine("[C2 Servers]");
                foreach (var ip in r.RawIps.Distinct())   sb.AppendLine($"  IP    : {ip}");
                foreach (var d  in r.RawDomains.Distinct()) sb.AppendLine($"  Domain: {d}");
                sb.AppendLine();
                sb.AppendLine("[Ports]");
                foreach (var p in r.RawPorts.Distinct())  sb.AppendLine($"  {p}");
                sb.AppendLine();
                sb.AppendLine("[Connection Keys]");
                foreach (var k in r.RawKeys)              sb.AppendLine($"  {k}");
                sb.AppendLine();
                sb.AppendLine("[Findings]");
                foreach (var f in r.Findings.OrderByDescending(x => x.Confidence))
                {
                    sb.AppendLine($"  [{f.Confidence}%] Type:{f.ChannelType}");
                    if (!string.IsNullOrEmpty(f.Host))         sb.AppendLine($"    Host : {f.Host}");
                    if (!string.IsNullOrEmpty(f.Port))         sb.AppendLine($"    Port : {f.Port}");
                    if (!string.IsNullOrEmpty(f.ConnectionKey)) sb.AppendLine($"    Key  : {f.ConnectionKey}");
                    sb.AppendLine($"    Src  : {f.Source}");
                    sb.AppendLine($"    Ctx  : {f.Context.Split('\n')[0]}");
                    sb.AppendLine();
                }

                File.WriteAllText(reportPath, sb.ToString(), Encoding.UTF8);
                LogMessage($"\U0001f4be تقرير C2 محفوظ: {Path.GetFileName(reportPath)}");
            }
            catch (Exception ex)
            {
                LogMessage($"  \u26a0 فشل حفظ تقرير C2: {ex.Message}");
            }
        }

        #endregion // C2 Panel Detector

        #region Preferences Tree Extractor

        private async void BtnPrefsExtractor_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_sourceApkPath) || !File.Exists(_sourceApkPath))
            {
                WpfMessageBox.Show("اختر ملف APK أولاً.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            btnPrefsExtractor.IsEnabled = false;
            SetStatus("جاري استخراج Preferences Tree...");
            LogMessage("\n══════════════════════════════════════════════════════════");
            LogMessage("🗂️ [استخراج Preferences Tree] — Android 36 (API 36)");
            LogMessage("══════════════════════════════════════════════════════════");

            string extractDir = Path.Combine(_workDir, $"prefs_{Guid.NewGuid():N}");
            var report = new PrefsDetectionReport { ApkName = Path.GetFileName(_sourceApkPath), ApkPath = _sourceApkPath };

            try
            {
                await Task.Run(() => RunPrefsExtraction(extractDir, report));
                DisplayPrefsReport(report);
            }
            catch (Exception ex)
            {
                LogMessage($"❌ خطأ في تحليل Preferences: {ex.Message}");
            }
            finally
            {
                try { if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true); } catch { }
                btnPrefsExtractor.IsEnabled = true;
                SetStatus("اكتمل الاستخراج");
            }
        }

        private void RunPrefsExtraction(string workDir, PrefsDetectionReport report)
        {
            LogMessage("📦 [1/3] فك APK لتحليل الـ Smali/XML...");
            
            // Start apktool decompilation
            using (var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName               = "cmd.exe",
                    Arguments              = $"/c \"\"{_apktoolPath}\" d \"{_sourceApkPath}\" -o \"{workDir}\" -f\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                    StandardOutputEncoding = Encoding.UTF8
                }
            })
            {
                proc.Start();
                proc.WaitForExit(180_000); 
            }

            if (!Directory.Exists(workDir))
            {
                report.Errors.Add("فشل فك التطبيق");
                return;
            }

            LogMessage("🔬 [2/3] تحليل Smali Codes لمعرفة الـ Preferences API الحقيقية...");

            int smaliFilesScanned = 0;
            var smaliDirs = new List<string>();
            string mainSmali = Path.Combine(workDir, "smali");
            if (Directory.Exists(mainSmali)) smaliDirs.Add(mainSmali);
            for (int i = 2; i <= 20; i++)
            {
                string sd = Path.Combine(workDir, $"smali_classes{i}");
                if (Directory.Exists(sd)) smaliDirs.Add(sd);
            }

            foreach (string sd in smaliDirs)
            {
                foreach (string file in Directory.EnumerateFiles(sd, "*.smali", SearchOption.AllDirectories))
                {
                    smaliFilesScanned++;
                    ScanSmaliForPrefs(file, report);
                }
            }
            LogMessage($"  ✓ تم فحص {smaliFilesScanned} ملف Smali");

            LogMessage("📂 [3/3] كشف SharedPreferences ومسارات التخزين والتصدير...");
            ScanResourcesForPrefs(workDir, report);
        }

        private void ScanSmaliForPrefs(string filePath, PrefsDetectionReport report)
        {
            string content;
            try { content = File.ReadAllText(filePath, Encoding.UTF8); }
            catch { return; }

            string fileName = Path.GetFileName(filePath);

            // 1. java.util.prefs.Preferences (android-36/java/util/prefs/Preferences.java)
            if (content.Contains("java/util/prefs/Preferences;->"))
            {
                // محاولة استخراج node path ("/" prefixed key)
                var nodeMatch = Regex.Match(content,
                    @"const-string(?:/jumbo)?\s+\w+,\s+""((?:/[\w\-\.]+)+)""");
                string nodePath = nodeMatch.Success ? nodeMatch.Groups[1].Value : "";

                report.Findings.Add(new PrefsFinding
                {
                    PrefsType  = "java.util.prefs.Preferences",
                    RiskLevel  = PrefRisk.High,
                    Source     = $"Smali/{fileName}",
                    Context    = "استخدام java.util.prefs الهرمي — غير مألوف في أندرويد، قد يخزن إعدادات خارج sandbox التطبيق",
                    FilePaths  = nodePath
                });
            }

            // 2. AbstractPreferences
            if (content.Contains("java/util/prefs/AbstractPreferences;"))
            {
                report.Findings.Add(new PrefsFinding
                {
                    PrefsType = "Ljava.util.prefs.AbstractPreferences",
                    RiskLevel = PrefRisk.High,
                    Source    = $"Smali/{fileName}",
                    Context   = "نوع Preferences مجرد — يُشير لتخصيص بنية تخزين خاصة بالتطبيق",
                    FilePaths = ""
                });
            }

            // 3. FileSystemPreferences & XmlSupport (android-36/java/util/prefs/)
            if (content.Contains("Ljava/util/prefs/FileSystemPreferences;") ||
                content.Contains("Ljava/util/prefs/XmlSupport;"))
            {
                report.Findings.Add(new PrefsFinding
                {
                    PrefsType = "java.util.prefs.FileSystemPreferences / XmlSupport",
                    RiskLevel = PrefRisk.Critical,
                    Source    = $"Smali/{fileName}",
                    Context   = "تخزين Preferences على نظام الملفات كـ XML — غير معتاد في Android الحديث ومشبوه جداً",
                    FilePaths = "java.util.prefs.*"
                });
            }

            // 4. SharedPreferences التقليدي
            if (content.Contains("android/content/SharedPreferences;->"))
            {
                // استخراج اسم الملف المستخدم في getSharedPreferences("name", ...)
                var spMatch = Regex.Match(content,
                    @"const-string(?:/jumbo)?\s+\w+,\s+""([\w\.\-]{2,80})""");
                string spName = spMatch.Success ? spMatch.Groups[1].Value : "";

                report.Findings.Add(new PrefsFinding
                {
                    PrefsType = "android.content.SharedPreferences",
                    RiskLevel = PrefRisk.Low,
                    Source    = $"Smali/{fileName}",
                    Context   = "SharedPreferences — تخزين المفضلة القياسي في Android",
                    FilePaths = spName
                });
            }

            // 5. EncryptedSharedPreferences (Jetpack Security — مشبوه إذا يخفي بيانات حساسة)
            if (content.Contains("Landroidx/security/crypto/EncryptedSharedPreferences;"))
            {
                var keyMatch = Regex.Match(content,
                    @"const-string(?:/jumbo)?\s+\w+,\s+""([\w\.\-]{2,80})""");
                string encFile = keyMatch.Success ? keyMatch.Groups[1].Value : "";

                report.Findings.Add(new PrefsFinding
                {
                    PrefsType = "androidx.security.crypto.EncryptedSharedPreferences",
                    RiskLevel = PrefRisk.Medium,
                    Source    = $"Smali/{fileName}",
                    Context   = "SharedPreferences مشفرة — تخزين بيانات حساسة (مفاتيح، tokens) بتشفير AES-256",
                    FilePaths = encFile
                });
            }

            // 6. Jetpack DataStore (Proto & Preferences) — Android حديث
            if (content.Contains("Landroidx/datastore/core/DataStore;") ||
                content.Contains("Landroidx/datastore/preferences/core/Preferences;"))
            {
                report.Findings.Add(new PrefsFinding
                {
                    PrefsType = "androidx.datastore (Jetpack DataStore)",
                    RiskLevel = PrefRisk.Low,
                    Source    = $"Smali/{fileName}",
                    Context   = "Jetpack DataStore — بديل حديث لـ SharedPreferences يدعم Kotlin Coroutines",
                    FilePaths = ""
                });
            }

            // 7. java.util.logging.SocketHandler (android-36/java/util/logging/SocketHandler.java)
            if (content.Contains("Ljava/util/logging/SocketHandler;->"))
            {
                // محاولة استخراج host:port
                var hostMatch = Regex.Match(content,
                    @"const-string(?:/jumbo)?\s+\w+,\s+""([\w\.\-]+)""");
                var portMatch = Regex.Match(content,
                    @"const-string(?:/jumbo)?\s+\w+,\s+""(\d{2,5})""");
                string endpoint = hostMatch.Success
                    ? hostMatch.Groups[1].Value + (portMatch.Success ? ":" + portMatch.Groups[1].Value : "")
                    : "";

                report.Findings.Add(new PrefsFinding
                {
                    PrefsType = "java.util.logging.SocketHandler",
                    RiskLevel = PrefRisk.Critical,
                    Source    = $"Smali/{fileName}",
                    Context   = "إرسال logs عبر TCP Socket — قناة تسريب بيانات محتملة\n" +
                                "المصدر: android-36/java/util/logging/SocketHandler.java",
                    FilePaths = endpoint
                });
            }

            // 8. Room Database (تخزين هيكلي — مشبوه إذا يخزن بيانات اتصال)
            if (content.Contains("Landroidx/room/RoomDatabase;") ||
                content.Contains("Landroidx/room/Room;"))
            {
                var dbMatch = Regex.Match(content,
                    @"const-string(?:/jumbo)?\s+\w+,\s+""([\w\.\-]+\.db)""");
                string dbName = dbMatch.Success ? dbMatch.Groups[1].Value : "";

                report.Findings.Add(new PrefsFinding
                {
                    PrefsType = "androidx.room.RoomDatabase",
                    RiskLevel = PrefRisk.Medium,
                    Source    = $"Smali/{fileName}",
                    Context   = "تخزين SQLite عبر Room — قاعدة بيانات هيكلية",
                    FilePaths = dbName
                });
            }
        }

        private void ScanResourcesForPrefs(string workDir, PrefsDetectionReport report)
        {
            // ── 1. res/xml/ — تحليل كامل لملفات PreferenceScreen ──────────────
            string resXmlDir = Path.Combine(workDir, "res", "xml");
            if (Directory.Exists(resXmlDir))
            {
                foreach (var xmlFile in Directory.EnumerateFiles(resXmlDir, "*.xml"))
                {
                    string content;
                    try { content = File.ReadAllText(xmlFile, Encoding.UTF8); }
                    catch { continue; }

                    bool isPreferenceFile =
                        content.Contains("<PreferenceScreen") ||
                        content.Contains("<PreferenceCategory") ||
                        content.Contains("<CheckBoxPreference") ||
                        content.Contains("<EditTextPreference") ||
                        content.Contains("<ListPreference") ||
                        content.Contains("<SwitchPreference") ||
                        content.Contains("<SeekBarPreference");

                    if (!isPreferenceFile) continue;

                    string xmlName = Path.GetFileName(xmlFile);

                    // — استخراج عنوان الشاشة الرئيسية
                    var screenTitle = Regex.Match(content,
                        @"<PreferenceScreen[^>]+android:title=""([^""]+)""");
                    string screenTitleStr = screenTitle.Success
                        ? screenTitle.Groups[1].Value : "";

                    // — استخراج كل مفاتيح الـ Preference الفعلية
                    // android:key="xxx" android:title="yyy" android:defaultValue="zzz"
                    var keyMatches = Regex.Matches(content,
                        @"android:key=""([^""]+)""");
                    var titleMatches = Regex.Matches(content,
                        @"android:title=""([^""]+)""");
                    var defaultMatches = Regex.Matches(content,
                        @"android:defaultValue=""([^""]+)""");

                    // — استخراج sensitive defaults (كلمات مرور / tokens / IPs)
                    var sensitiveDefaults = new List<string>();
                    foreach (Match dm in defaultMatches)
                    {
                        string dv = dm.Groups[1].Value;
                        // نبحث عن قيم مشبوهة كـ IPs أو tokens أو عبارات فارغة وهمية
                        if (Regex.IsMatch(dv, @"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}") ||
                            Regex.IsMatch(dv, @"^[A-Za-z0-9+/]{16,}={0,2}$") ||
                            Regex.IsMatch(dv, @"https?://") ||
                            dv.Length > 20)
                            sensitiveDefaults.Add(dv);
                    }

                    // بناء ملخص المفاتيح
                    var keys = keyMatches.Cast<Match>()
                        .Select(m => m.Groups[1].Value)
                        .Take(30)
                        .ToList();

                    string keysStr = keys.Count > 0
                        ? string.Join(", ", keys)
                        : "(لا مفاتيح مُعرَّفة)";

                    PrefRisk risk = sensitiveDefaults.Count > 0
                        ? PrefRisk.High : PrefRisk.Low;

                    string ctx = $"PreferenceScreen XML — {keys.Count} مفتاح" +
                                 (screenTitleStr.Length > 0
                                     ? $" — الشاشة: \"{screenTitleStr}\"" : "");

                    if (sensitiveDefaults.Count > 0)
                        ctx += $"\n⚠ قيم default مشبوهة: {string.Join(" | ", sensitiveDefaults.Take(5))}";

                    report.Findings.Add(new PrefsFinding
                    {
                        PrefsType = "XML PreferenceScreen",
                        RiskLevel = risk,
                        Source    = $"res/xml/{xmlName}",
                        Context   = ctx,
                        FilePaths = keysStr
                    });

                    LogMessage($"  ✓ res/xml/{xmlName}: {keys.Count} مفتاح" +
                               (sensitiveDefaults.Count > 0
                                   ? $" ⚠ {sensitiveDefaults.Count} قيمة مشبوهة" : ""));
                }
            }

            // ── 2. assets/ — ملفات إعدادات التطبيق ───────────────────────────
            string assetsDir = Path.Combine(workDir, "assets");
            if (Directory.Exists(assetsDir))
            {
                var configExts = new[] { "*.json", "*.properties", "*.yaml",
                                         "*.yml", "*.ini", "*.cfg", "*.conf", "*.xml" };
                foreach (var ext in configExts)
                {
                    foreach (var file in Directory.EnumerateFiles(
                        assetsDir, ext, SearchOption.AllDirectories).Take(50))
                    {
                        string content;
                        try { content = File.ReadAllText(file, Encoding.UTF8); }
                        catch { continue; }

                        string fileName = Path.GetRelativePath(assetsDir, file);

                        // البحث عن مفاتيح إعدادات (key=value أو "key": "value")
                        var configKeys = Regex.Matches(content,
                            @"(?:""([^""]{3,60})""\s*:\s*""([^""]{1,200})""|" +
                            @"([a-zA-Z_][a-zA-Z0-9_.]{2,59})\s*=\s*([^\n\r]{1,200}))");

                        if (configKeys.Count == 0) continue;

                        var sensitiveKeys = new List<(string key, string val)>();
                        foreach (Match m in configKeys)
                        {
                            string k = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[3].Value;
                            string v = m.Groups[2].Success ? m.Groups[2].Value : m.Groups[4].Value.Trim();

                            // نصفّي فقط المفاتيح الحساسة المحتملة
                            if (Regex.IsMatch(k,
                                @"(?i)(server|host|ip|port|url|endpoint|key|token|secret|password|auth|api)",
                                RegexOptions.IgnoreCase))
                                sensitiveKeys.Add((k, v));
                        }

                        if (sensitiveKeys.Count == 0) continue;

                        PrefRisk assetRisk = PrefRisk.Medium;
                        // خطورة عالية إذا كانت القيم تشبه IPs أو tokens
                        if (sensitiveKeys.Any(p =>
                            Regex.IsMatch(p.val, @"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}") ||
                            Regex.IsMatch(p.val, @"https?://") ||
                            Regex.IsMatch(p.val, @"^[A-Za-z0-9+/]{32,}={0,2}$")))
                            assetRisk = PrefRisk.Critical;

                        string keySummary = string.Join("; ",
                            sensitiveKeys.Take(5).Select(p =>
                                $"{p.key}={p.val[..Math.Min(40, p.val.Length)]}" +
                                (p.val.Length > 40 ? "..." : "")));

                        report.Findings.Add(new PrefsFinding
                        {
                            PrefsType = "Asset Config File",
                            RiskLevel = assetRisk,
                            Source    = $"assets/{fileName}",
                            Context   = $"ملف إعدادات في assets — {sensitiveKeys.Count} مفتاح حساس",
                            FilePaths = keySummary
                        });

                        LogMessage($"  ✓ assets/{fileName}: {sensitiveKeys.Count} مفتاح حساس" +
                                   (assetRisk == PrefRisk.Critical ? " 🔴" : " 🟡"));
                    }
                }
            }

            // ── 3. res/raw/ — ملفات خام قد تحتوي إعدادات مشفرة أو configs ───
            string rawDir = Path.Combine(workDir, "res", "raw");
            if (Directory.Exists(rawDir))
            {
                foreach (var file in Directory.EnumerateFiles(rawDir, "*.*")
                    .Where(f =>
                    {
                        string ext = Path.GetExtension(f).ToLowerInvariant();
                        return ext is ".json" or ".xml" or ".txt" or ".properties"
                                    or ".pb" or ".conf" or ".yaml";
                    }).Take(30))
                {
                    string content;
                    try { content = File.ReadAllText(file, Encoding.UTF8); }
                    catch { continue; }

                    // أي بيانات تبدو كـ config
                    bool hasUrls    = Regex.IsMatch(content, @"https?://");
                    bool hasIps     = Regex.IsMatch(content,
                        @"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b");
                    bool hasSecrets = Regex.IsMatch(content,
                        @"(?i)(key|token|secret|password|auth)\s*[=:]\s*\S{6,}");

                    if (!hasUrls && !hasIps && !hasSecrets) continue;

                    PrefRisk rawRisk = (hasIps || hasSecrets) ? PrefRisk.Critical : PrefRisk.High;

                    report.Findings.Add(new PrefsFinding
                    {
                        PrefsType = "res/raw Config",
                        RiskLevel = rawRisk,
                        Source    = $"res/raw/{Path.GetFileName(file)}",
                        Context   = $"ملف raw يحتوي بيانات حساسة" +
                                    (hasUrls    ? " + URLs" : "") +
                                    (hasIps     ? " + IPs" : "") +
                                    (hasSecrets ? " + Keys/Tokens" : ""),
                        FilePaths = ""
                    });

                    LogMessage($"  ✓ res/raw/{Path.GetFileName(file)}: بيانات " +
                               (rawRisk == PrefRisk.Critical ? "🔴 خطرة" : "🟠 مرتفعة"));
                }
            }
        }

        private void DisplayPrefsReport(PrefsDetectionReport r)
        {
            Dispatcher.Invoke(() =>
            {
                LogMessage("\n╔══════════════════════════════════════════════════════════╗");
                LogMessage("║         🗂️ تقرير Preferences Tree Extractor               ║");
                LogMessage("║         Aleppo University Research — Android 36            ║");
                LogMessage("╚══════════════════════════════════════════════════════════╝");
                LogMessage($"   APK: {r.ApkName}");

                if (r.Errors.Any())
                {
                    LogMessage("\n⚠ تحذيرات:");
                    foreach (var err in r.Errors) LogMessage($"   • {err}");
                }

                if (r.Findings.Any())
                {
                    // ── ملخص إحصائي ──────────────────────────────────────────
                    int critical = r.Findings.Count(f => f.RiskLevel == PrefRisk.Critical);
                    int high     = r.Findings.Count(f => f.RiskLevel == PrefRisk.High);
                    int medium   = r.Findings.Count(f => f.RiskLevel == PrefRisk.Medium);
                    int low      = r.Findings.Count(f => f.RiskLevel == PrefRisk.Low);

                    LogMessage($"\n📊 ملخص الاكتشافات:");
                    LogMessage($"   الإجمالي: {r.Findings.Count} إشارة في {r.Findings.Select(f => f.Source).Distinct().Count()} ملف Smali");
                    if (critical > 0) LogMessage($"   🔴 خطر شديد : {critical}");
                    if (high     > 0) LogMessage($"   🟠 خطر مرتفع: {high}");
                    if (medium   > 0) LogMessage($"   🟡 متوسط    : {medium}");
                    if (low      > 0) LogMessage($"   🟢 منخفض    : {low}");

                    // ── التفاصيل مجمّعة حسب نوع التخزين ────────────────────
                    var grouped = r.Findings
                        .GroupBy(f => f.PrefsType)
                        .OrderByDescending(g => (int)g.Max(f => f.RiskLevel));

                    foreach (var g in grouped)
                    {
                        string riskIcon = g.Max(f => f.RiskLevel) switch
                        {
                            PrefRisk.Critical => "🔴",
                            PrefRisk.High     => "🟠",
                            PrefRisk.Medium   => "🟡",
                            _                 => "🟢"
                        };

                        LogMessage($"\n{riskIcon} {g.Key}  ({g.Count()} إشارة)");
                        foreach (var f in g.Take(15))
                        {
                            LogMessage($"    📂 {f.Source}");
                            if (!string.IsNullOrEmpty(f.FilePaths))
                                LogMessage($"    📜 المعرّف: {f.FilePaths}");
                            LogMessage($"    ℹ {f.Context.Split('\n')[0]}");
                        }
                        if (g.Count() > 15)
                            LogMessage($"    ... و{g.Count() - 15} إشارة إضافية في التقرير");
                    }

                    // ── تحذيرات خاصة ─────────────────────────────────────────
                    if (r.UsesJavaUtilPrefs)
                    {
                        LogMessage("\n⚠️ [تحذير بحثي] java.util.prefs مكتشف!");
                        LogMessage("   هذا الـ API مصمم لـ Java Desktop وليس Android — وجوده مشبوه جداً.");
                        LogMessage("   المصدر: android-36/java/util/prefs/Preferences.java");
                    }
                    if (r.UsesSocketHandler)
                    {
                        LogMessage("\n🚨 [خطر] SocketHandler مكتشف — تسريب بيانات محتمل عبر TCP!");
                        LogMessage("   المصدر: android-36/java/util/logging/SocketHandler.java");
                    }
                    if (r.UsesEncryptedPrefs)
                    {
                        LogMessage("\n🔐 [ملاحظة] EncryptedSharedPreferences مكتشف — التطبيق يشفر بياناته المحلية.");
                    }
                }
                else
                {
                    LogMessage("\n✅ لم يُكتشف أي تخزين Preferences غير اعتيادي.");
                }

                LogMessage("\n──────────────────────────────────────────────────────────");
                LogMessage("✅ اكتمل تحليل Preferences بنجاح");
                LogMessage("──────────────────────────────────────────────────────────\n");

                // حفظ التقرير على القرص
                SavePrefsReport(r);
            });
        }

        private void SavePrefsReport(PrefsDetectionReport r)
        {
            try
            {
                string apkName    = Path.GetFileNameWithoutExtension(r.ApkPath);
                string timestamp  = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string reportPath = Path.Combine(_outputDir,
                    $"prefs_report_{apkName}_{timestamp}.txt");

                var sb = new StringBuilder();
                sb.AppendLine("════════════════════════════════════════════════════════════");
                sb.AppendLine(" Preferences Tree Extractor Report");
                sb.AppendLine(" Aleppo University Research Project — Android 36 (API 36)");
                sb.AppendLine($" Date  : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($" APK   : {r.ApkPath}");
                sb.AppendLine($" Finds : {r.Findings.Count}  |  java.util.prefs: {r.UsesJavaUtilPrefs}");
                sb.AppendLine("════════════════════════════════════════════════════════════");
                sb.AppendLine();

                sb.AppendLine("[Summary]");
                sb.AppendLine($"  Total Findings : {r.Findings.Count}");
                sb.AppendLine($"  Critical       : {r.Findings.Count(f => f.RiskLevel == PrefRisk.Critical)}");
                sb.AppendLine($"  High           : {r.Findings.Count(f => f.RiskLevel == PrefRisk.High)}");
                sb.AppendLine($"  Medium         : {r.Findings.Count(f => f.RiskLevel == PrefRisk.Medium)}");
                sb.AppendLine($"  Low            : {r.Findings.Count(f => f.RiskLevel == PrefRisk.Low)}");
                sb.AppendLine($"  Uses java.util.prefs      : {r.UsesJavaUtilPrefs}");
                sb.AppendLine($"  Uses SocketHandler (TCP)  : {r.UsesSocketHandler}");
                sb.AppendLine($"  Uses EncryptedSharedPrefs : {r.UsesEncryptedPrefs}");
                sb.AppendLine();

                foreach (var grp in r.Findings
                    .GroupBy(f => f.PrefsType)
                    .OrderByDescending(g => (int)g.Max(f => f.RiskLevel)))
                {
                    sb.AppendLine($"[{grp.Key}]  Risk:{grp.Max(f => f.RiskLevel)}  Count:{grp.Count()}");
                    foreach (var f in grp)
                    {
                        sb.AppendLine($"  Source   : {f.Source}");
                        if (!string.IsNullOrEmpty(f.FilePaths))
                            sb.AppendLine($"  ID/Path  : {f.FilePaths}");
                        sb.AppendLine($"  Context  : {f.Context.Split('\n')[0]}");
                        sb.AppendLine();
                    }
                }

                File.WriteAllText(reportPath, sb.ToString(), Encoding.UTF8);
                LogMessage($"💾 تقرير Preferences محفوظ: {Path.GetFileName(reportPath)}");
            }
            catch (Exception ex)
            {
                LogMessage($"  ⚠ فشل حفظ تقرير Preferences: {ex.Message}");
            }
        }



    // ══════════════════════════════════════════════════════════════════════════════
    // SIP Stack Security Analyzer
    // مبني على مصادر NIST JAIN-SIP من android-36/gov/nist/javax/sip/stack/
    //
    // المصادر المدروسة:
    //   IOHandler.java          → socket caching (TCP/TLS/UDP) + writeChunks + semaphore
    //   SIPTransactionStack.java→ dialog tables, congestion control, maxConnections, outboundProxy
    //   SIPConstants.java       → DEFAULT_PORT=5060, DEFAULT_TLS_PORT=5061, BRANCH_MAGIC_COOKIE
    //   MessageChannel.java     → Via headers, peer address/port extraction, transport abstraction
    //   TCPMessageChannel.java  → TCP socket re-use pattern, readTimeout (readTimeout=-1 = infinite)
    //   TLSMessageChannel.java  → SSL handshake, enabled protocols, HandshakeCompletedListener
    //   UDPMessageChannel.java  → UDP datagram parsing, SIP/2.0 message parsing
    //   SIPDialog.java          → Call-ID, From-tag, To-tag dialog session structure
    //   SIPClientTransaction.java → INVITE/REGISTER/SUBSCRIBE client patterns
    //   SIPServerTransaction.java → server tx state machine, mergeTable (fork detection)
    //   DefaultRouter.java      → outbound proxy routing, hop selection algorithm
    //   HopImpl.java            → host:port:transport Hop structure
    // ══════════════════════════════════════════════════════════════════════════════

    #region SIP Stack Security Analyzer

    // ── SIP data model fields (defined below as nested after class) ────────────
    // ── UI entry point ──────────────────────────────────────────────────────────

    /// <summary>
    /// تحليل أمني كامل للـ SIP Stack في APK.
    /// يُنفَّذ بالضغط على زر "🔐 SIP Stack Analyzer" في Footer.
    ///
    /// المنهجية:
    ///  1. فك APK بـ apktool (–no-src للسرعة، fallback كامل)
    ///  2. مسح جميع ملفات Smali بحثاً عن SIP signatures حقيقية
    ///     - Ljavax/sip/*, Lgov/nist/javax/sip/*, Landroid/net/sip/*
    ///     - SIP/2.0 strings, Via: SIP, sip: URI scheme
    ///     - BRANCH_MAGIC_COOKIE z9hG4bK (RFC3261)
    ///     - منافذ 5060/5061 (SIPConstants.DEFAULT_PORT / DEFAULT_TLS_PORT)
    ///     - outboundProxy, cacheServerConnections, maxConnections constants
    ///     - dialog hijacking indicators (extracting Call-ID, From-tag, To-tag raw)
    ///  3. مسح AndroidManifest + network_security_config
    ///  4. توليد تقرير بحثي دقيق + حفظه
    /// </summary>
    internal async void BtnSipAnalyzer_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_sourceApkPath) || !File.Exists(_sourceApkPath))
        {
            WpfMessageBox.Show("اختر ملف APK أولاً.", "تنبيه",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // تعطيل الزر أثناء التحليل
        if (sender is System.Windows.Controls.Button btn) btn.IsEnabled = false;
        SetStatus("جاري SIP Stack Security Analysis...");

        LogMessage("\n╔══════════════════════════════════════════════════════╗");
        LogMessage("║  🔐 SIP Stack Security Analyzer                       ║");
        LogMessage("║  Aleppo University Research — NIST JAIN-SIP / API 36  ║");
        LogMessage("╚══════════════════════════════════════════════════════╝");
        LogMessage($"   APK : {Path.GetFileName(_sourceApkPath)}");
        LogMessage($"   SIP : NIST javax.sip (android-36/gov/nist/javax/sip/)");
        LogMessage($"   Ref : DEFAULT_PORT=5060 | DEFAULT_TLS_PORT=5061 | BRANCH_MAGIC_COOKIE=z9hG4bK");

        string workDir = Path.Combine(_workDir, $"sip_{Guid.NewGuid():N}");

        try
        {
            var report = await Task.Run(() => RunSipStackAnalysis(workDir));
            DisplaySipReport(report);
        }
        catch (Exception ex)
        {
            LogMessage($"❌ SIP Analyzer error: {ex.Message}");
        }
        finally
        {
            try { if (Directory.Exists(workDir)) Directory.Delete(workDir, true); } catch { }
            if (sender is System.Windows.Controls.Button b) b.IsEnabled = true;
            SetStatus("اكتمل SIP Analysis");
        }
    }

    // ── core analysis engine ────────────────────────────────────────────────────

    private SipSecurityReport RunSipStackAnalysis(string workDir)
    {
        var report = new SipSecurityReport
        {
            ApkName = Path.GetFileNameWithoutExtension(_sourceApkPath ?? ""),
            ApkPath = _sourceApkPath ?? ""
        };

        // ─── Step 1: فك APK ──────────────────────────────────────────────────
        LogMessage("\n📦 [1/4] فك APK بـ apktool (--no-src أولاً للسرعة)...");
        RunApktoolSync($"d \"{_sourceApkPath}\" -o \"{workDir}\" -f --no-src");

        // fallback: إذا لم تُكتشف Smali نعيد بالكامل
        bool hasSmali = Directory.Exists(Path.Combine(workDir, "smali"));
        if (!hasSmali)
        {
            LogMessage("  ↩ Fallback: إعادة الفك بدون --no-src...");
            try { Directory.Delete(workDir, true); } catch { }
            RunApktoolSync($"d \"{_sourceApkPath}\" -o \"{workDir}\" -f");
        }

        LogMessage($"  ✓ فُكّ في: {workDir}");

        // ─── Step 2: جمع ملفات Smali ─────────────────────────────────────────
        LogMessage("\n🔬 [2/4] مسح ملفات Smali بحثاً عن NIST SIP patterns...");
        var smaliFiles = CollectSmaliFiles(workDir);
        LogMessage($"  ✓ {smaliFiles.Count} ملف Smali للمسح");

        int processed = 0;
        foreach (string sf in smaliFiles)
        {
            ScanSmaliForSip(sf, report);
            processed++;
        }
        LogMessage($"  ✓ تم فحص {processed} ملف");

        // ─── Step 3: مسح Manifest + network_security_config ──────────────────
        LogMessage("\n📂 [3/4] مسح AndroidManifest + network_security_config...");
        ScanManifestForSip(workDir, report);

        // ─── Step 4: حساب ThreatScore ─────────────────────────────────────────
        LogMessage("\n📊 [4/4] حساب مؤشر التهديد...");
        ComputeSipThreatScore(report);

        return report;
    }

    // ── smali file collection ───────────────────────────────────────────────────

    private List<string> CollectSmaliFiles(string workDir)
    {
        var files = new List<string>();
        string[] smaliRoots = { "smali" };
        var extras = Enumerable.Range(2, 19)
                               .Select(i => $"smali_classes{i}");

        foreach (string root in smaliRoots.Concat(extras))
        {
            string dir = Path.Combine(workDir, root);
            if (Directory.Exists(dir))
                files.AddRange(Directory.EnumerateFiles(dir, "*.smali",
                    SearchOption.AllDirectories));
        }
        return files;
    }

    // ── smali scanner ──────────────────────────────────────────────────────────

    /// <summary>
    /// يمسح ملف Smali واحد بحثاً عن كل patterns مرتبطة بـ NIST JAIN-SIP.
    ///
    /// المراجع الحقيقية المستخدمة:
    ///  - IOHandler.java     → Ljavax/net/ssl/SSLSocket, ConcurrentHashMap socket table
    ///  - SIPConstants.java  → DEFAULT_PORT=5060, DEFAULT_TLS_PORT=5061, BRANCH_MAGIC_COOKIE
    ///  - MessageChannel.java→ Via: SIP/2.0, transport=TCP|TLS|UDP
    ///  - SIPDialog.java     → Call-ID, From-tag, To-tag, CSeq
    ///  - DefaultRouter.java → outboundProxy string
    ///  - SIPTransactionStack→ maxConnections, serverTransactionTableHighwaterMark
    /// </summary>
    private void ScanSmaliForSip(string filePath, SipSecurityReport report)
    {
        string content;
        try { content = File.ReadAllText(filePath, Encoding.UTF8); }
        catch { return; }

        // اسم الكلاس للسياق
        var classM = Regex.Match(content, @"^\.class\s+\S+\s+(\S+)", RegexOptions.Multiline);
        string ctx = classM.Success
            ? classM.Groups[1].Value.Split('/').LastOrDefault()
              ?? Path.GetFileNameWithoutExtension(filePath)
            : Path.GetFileNameWithoutExtension(filePath);

        // ── A: هل يستخدم الملف مكتبات SIP أصلاً؟ ──────────────────────────
        bool isSipFile =
            content.Contains("Ljavax/sip/") ||
            content.Contains("Lgov/nist/javax/sip/") ||
            content.Contains("Landroid/net/sip/") ||
            content.Contains("SIP/2.0") ||
            content.Contains("sip:") ||
            content.Contains("sips:");

        if (!isSipFile) return;

        // تسجيل الملف كـ SIP file
        lock (report.SipSmaliFiles)
            report.SipSmaliFiles.Add($"{ctx} [{Path.GetFileName(filePath)}]");

        // ── B: SIP endpoints (sip: URI + port extraction) ────────────────────
        // sip:user@host:port;transport=tcp/tls/udp
        foreach (Match m in Regex.Matches(content,
            @"const-string(?:/jumbo)?\s+\w[\w\d]*,\s+""(sips?:[^""]{4,256})""",
            RegexOptions.Multiline))
        {
            string uri = m.Groups[1].Value;
            var ep = ParseSipUri(uri, ctx, Path.GetFileName(filePath));
            lock (report.Endpoints)
                report.Endpoints.Add(ep);
        }

        // ── C: Raw host+port pairs مرتبطة بـ SIP (port 5060 أو 5061) ─────────
        // في Smali: const-string vX, "192.168.1.1"  ثم const/16 vY, 0x13C4 (=5060)
        // أو: const-string vX, "proxy.voip.com" مع const-string vY, "5060"
        foreach (Match m in Regex.Matches(content,
            @"const-string(?:/jumbo)?\s+\w[\w\d]*,\s+""([a-zA-Z0-9][a-zA-Z0-9.\-]{2,100})""",
            RegexOptions.Multiline))
        {
            string val = m.Groups[1].Value;
            // هل يتبعه مباشرة ثابت يساوي 5060 أو 5061؟
            int idx = m.Index + m.Length;
            string nextChunk = content.Length > idx + 200
                ? content.Substring(idx, 200) : content.Substring(idx);

            bool hasSipPort = nextChunk.Contains("0x13C4")  // 5060
                           || nextChunk.Contains("0x13C5")  // 5061
                           || nextChunk.Contains(", 5060")
                           || nextChunk.Contains(", 5061")
                           || nextChunk.Contains("\"5060\"")
                           || nextChunk.Contains("\"5061\"");

            if (hasSipPort)
            {
                string transport = nextChunk.Contains("0x13C5") || nextChunk.Contains("5061")
                    ? "TLS" : "TCP/UDP";
                string port = transport == "TLS" ? "5061" : "5060";
                lock (report.Endpoints)
                    report.Endpoints.Add(new SipEndpoint
                    {
                        Host      = val,
                        Port      = port,
                        Transport = transport,
                        IsDefault = true,
                        Context   = ctx,
                        Source    = Path.GetFileName(filePath)
                    });
            }
        }

        // ── D: Via headers (يكشف SIP proxy chain) ────────────────────────────
        // Via: SIP/2.0/TCP proxy.example.com:5060;branch=z9hG4bK77ef4c2312983.1
        foreach (Match m in Regex.Matches(content,
            @"const-string(?:/jumbo)?\s+\w[\w\d]*,\s+""(Via:\s*SIP/2\.0[^""]{4,256})""",
            RegexOptions.Multiline))
        {
            lock (report.ViaHeaders)
                report.ViaHeaders.Add($"{m.Groups[1].Value}  [{ctx}]");
        }

        // ── E: BRANCH_MAGIC_COOKIE (z9hG4bK) — RFC 3261 §8.1.1.7 ────────────
        // IOHandler و MessageChannel يستخدمانه لتمييز transactions
        foreach (Match m in Regex.Matches(content,
            @"const-string(?:/jumbo)?\s+\w[\w\d]*,\s+""(z9[hH][gG]4[bB][kK][a-zA-Z0-9._\-]*)""",
            RegexOptions.Multiline))
        {
            lock (report.BranchParams)
                report.BranchParams.Add($"{m.Groups[1].Value}  [{ctx}]");
        }

        // ── F: Call-ID headers (session identifiers) ─────────────────────────
        // SIPDialog.java: dialogId = callId + ":" + fromTag + ":" + toTag
        foreach (Match m in Regex.Matches(content,
            @"const-string(?:/jumbo)?\s+\w[\w\d]*,\s+""(Call-ID|CallID|call-id|i):\s*([^""]{4,128})""",
            RegexOptions.Multiline))
        {
            lock (report.CallIds)
                report.CallIds.Add($"{m.Groups[2].Value.Trim()}  [{ctx}]");
        }

        // ── G: SIP Methods مستخدمة ────────────────────────────────────────────
        // SIPTransactionStack: dialogCreatingMethods = {INVITE, SUBSCRIBE, REFER}
        var sipMethodPattern = new[]
        {
            "INVITE","REGISTER","SUBSCRIBE","NOTIFY","REFER",
            "OPTIONS","MESSAGE","UPDATE","PRACK","PUBLISH","INFO","BYE","CANCEL","ACK"
        };
        foreach (string method in sipMethodPattern)
        {
            if (content.Contains($"\"{method}\"") || content.Contains($"/{method}\""))
                lock (report.SipMethods)
                    report.SipMethods.Add(method);
        }

        // ── H: outboundProxy (من SIPTransactionStack.outboundProxy field) ────
        foreach (Match m in Regex.Matches(content,
            @"const-string(?:/jumbo)?\s+\w[\w\d]*,\s+""(outboundProxy|javax\.sip\.OUTBOUND_PROXY|OUTBOUND_PROXY)""",
            RegexOptions.Multiline))
        {
            // القيمة في السطر التالي
            int idx2 = m.Index + m.Length;
            string after = content.Length > idx2 + 300 ? content.Substring(idx2, 300)
                                                       : content.Substring(idx2);
            var valM = Regex.Match(after,
                @"const-string(?:/jumbo)?\s+\w[\w\d]*,\s+""([^""]{4,256})""");
            string proxy = valM.Success ? valM.Groups[1].Value : "(dynamic)";
            lock (report.OutboundProxies)
                report.OutboundProxies.Add($"{proxy}  [set in {ctx}]");
        }

        // ── I: TLS/SSL protocols (IOHandler.java: sslsock.setEnabledProtocols) ─
        foreach (Match m in Regex.Matches(content,
            @"const-string(?:/jumbo)?\s+\w[\w\d]*,\s+""(TLS|TLSv1\.[0-9]|SSLv3|SSLv2Hello|TLSV1_3)""",
            RegexOptions.Multiline))
        {
            lock (report.TlsProtocols)
                report.TlsProtocols.Add($"{m.Groups[1].Value}  [{ctx}]");
        }

        // ── J: SIP Authorization headers (اعتراض credentials) ───────────────
        // WWW-Authenticate / Authorization / Proxy-Authorization
        foreach (Match m in Regex.Matches(content,
            @"const-string(?:/jumbo)?\s+\w[\w\d]*,\s+""((?:WWW-Authenticate|Authorization|Proxy-Authorization):[^""]{4,256})""",
            RegexOptions.Multiline))
        {
            lock (report.AuthHeaders)
                report.AuthHeaders.Add($"{m.Groups[1].Value}  [{ctx}]");
        }

        // ── K: congestion control thresholds (SIPTransactionStack constants) ──
        // serverTransactionTableHighwaterMark = 5000 (default)
        // clientTransactionTableHiwaterMark   = 1000 (default)
        // maxConnections = -1 (unlimited) or specific value
        var cgi = report.CongestionInfo;

        // serverTransactionTableHighwaterMark
        foreach (Match m in Regex.Matches(content,
            @"const(?:/4|/16|/high)?\s+\w+,\s+(5000|4000|3000|2000)\b"))
        {
            if (int.TryParse(m.Groups[1].Value, out int v))
            {
                if (v > 3000 && cgi.ServerTxHighWater < 0)
                    cgi.ServerTxHighWater = v;
                else if (v <= 3000 && cgi.ServerTxLowWater < 0)
                    cgi.ServerTxLowWater = v;
            }
        }

        // maxConnections
        foreach (Match m in Regex.Matches(content,
            @"javax\.sip\.MAX_CONNECTIONS|maxConnections"))
        {
            int idx3 = m.Index + m.Length;
            string af = content.Length > idx3 + 150 ? content.Substring(idx3, 150)
                                                    : content.Substring(idx3);
            var nM = Regex.Match(af, @"const(?:/4|/16)?\s+\w+,\s+(\d+)");
            if (nM.Success && int.TryParse(nM.Groups[1].Value, out int mc))
                cgi.MaxConnections = mc;
        }

        // cacheServerConnections / cacheClientConnections
        if (content.Contains("cacheServerConnections") || content.Contains("CACHE_SERVER_CONNECTIONS"))
            lock (report.SocketFlags)
                report.SocketFlags.Add($"cacheServerConnections active  [{ctx}]");

        if (content.Contains("cacheClientConnections") || content.Contains("CACHE_CLIENT_CONNECTIONS"))
            lock (report.SocketFlags)
                report.SocketFlags.Add($"cacheClientConnections active  [{ctx}]");

        // ── L: dialog hijacking indicators ───────────────────────────────────
        // SIPDialog.java: من يعرف Call-ID + From-tag + To-tag يستطيع inject dialog
        bool fromTagLeak  = Regex.IsMatch(content, @"from[-_]?tag|fromTag|FROM_TAG",
            RegexOptions.IgnoreCase);
        bool toTagLeak    = Regex.IsMatch(content, @"to[-_]?tag|toTag|TO_TAG",
            RegexOptions.IgnoreCase);
        bool callIdExpose = Regex.IsMatch(content, @"getCallId|CallIDHeader|call[-_]?id",
            RegexOptions.IgnoreCase);

        if (fromTagLeak && toTagLeak && callIdExpose)
        {
            lock (report.HijackingIndicators)
                report.HijackingIndicators.Add(
                    $"Dialog session triple exposed (Call-ID + From-tag + To-tag) in {ctx}");
        }
        else if (callIdExpose && (fromTagLeak || toTagLeak))
        {
            lock (report.HijackingIndicators)
                report.HijackingIndicators.Add(
                    $"Partial dialog session exposure in {ctx}");
        }

        // SIPDialog.mergeId — كشف خوارزمية دمج الdialogsالمتفرعة (fork detection bypass)
        if (content.Contains("getMergeId") || content.Contains("mergeId") ||
            content.Contains("mergeTable"))
        {
            lock (report.HijackingIndicators)
                report.HijackingIndicators.Add(
                    $"Dialog merge/fork table access detected — possible fork-detection bypass in {ctx}");
        }

        // ── M: Digest Authentication نقاط الضعف ─────────────────────────────
        // المرجع: android-36/gov/nist/javax/sip/header/AuthenticationHeader.java
        //         android-36/gov/nist/javax/sip/clientauthutils/AuthenticationHelperImpl.java
        //
        // WWW-Authenticate: Digest realm="...", qop, nonce, algorithm
        // Authorization:    Digest response="...", nc, cnonce
        //
        // نقاط الخطورة:
        //  • algorithm=MD5 (مُهمَل — CVE محتمل)
        //  • qop غائب  → replay attack ممكن
        //  • نص المرور بدون nonce hashing
        // ─────────────────────────────────────────────────────────────────────
        {
            // MD5 algorithm مُستخدَم (AuthenticationHeader: ALGORITHM param)
            // RFC 8760 يُلغي MD5 ويُوجب SHA-256 أو SHA-512
            if (Regex.IsMatch(content,
                @"const-string(?:/jumbo)?\s+\w[\w\d]*,\s+""(MD5|algorithm=""MD5"")""",
                RegexOptions.Multiline) ||
                content.Contains("\"MD5\"") && content.Contains("algorithm"))
            {
                lock (report.HijackingIndicators)
                    report.HijackingIndicators.Add(
                        $"Digest-MD5 algorithm detected in {ctx} — MD5 is deprecated (RFC 8760 mandates SHA-256) " +
                        $"[android-36/gov/nist/javax/sip/header/AuthenticationHeader.java]");
            }

            // كشف realm المضمَّن — قد يكشف بنية الشبكة الداخلية
            foreach (Match m in Regex.Matches(content,
                @"const-string(?:/jumbo)?\s+\w[\w\d]*,\s+""(realm=[^""]{4,128})""",
                RegexOptions.Multiline))
            {
                lock (report.AuthHeaders)
                    report.AuthHeaders.Add(
                        $"Digest realm exposed: {m.Groups[1].Value}  [{ctx}]");
            }

            // qop=auth-int غائب (يتيح replay attacks)
            bool hasDigest = content.Contains("WWW-Authenticate") || content.Contains("Authorization");
            bool hasQop    = content.Contains("qop") || content.Contains("auth-int");
            if (hasDigest && !hasQop)
            {
                lock (report.HijackingIndicators)
                    report.HijackingIndicators.Add(
                        $"Digest authentication without qop=auth-int — replay attack vector in {ctx} " +
                        $"[RFC3261 §22.4, AuthenticationHeader.java]");
            }
        }

        // ── N: Record-Route Hijacking (Via.java + RecordRoute.java) ──────────
        // المرجع: android-36/gov/nist/javax/sip/header/Via.java          (16 KB)
        //         android-36/gov/nist/javax/sip/header/RecordRoute.java   (2.9 KB)
        //         android-36/gov/nist/javax/sip/header/RouteList.java     (2.7 KB)
        //
        // Record-Route header يُحدد مسار جميع الطلبات داخل dialog.
        // إذا تمكن مهاجم من حقن Record-Route، يتحكم في كل dialog requests.
        //
        // الكشف:
        //  • استخراج Record-Route strings مُضمَّنة
        //  • RouteList operations (addFirst → يعني التطبيق يتحكم في route)
        //  • loose-routing (lr parameter) — مطلوب لـ RFC3261 compliance
        // ─────────────────────────────────────────────────────────────────────
        {
            // Record-Route header strings
            foreach (Match m in Regex.Matches(content,
                @"const-string(?:/jumbo)?\s+\w[\w\d]*,\s+""(Record-Route:\s*[^""]{4,256})""",
                RegexOptions.Multiline))
            {
                bool hasLr = m.Groups[1].Value.Contains(";lr");
                lock (report.ViaHeaders)
                    report.ViaHeaders.Add(
                        $"Record-Route: {m.Groups[1].Value.Trim()} " +
                        $"{(hasLr ? "[✓ loose-routing]" : "[⚠ no ;lr — strict routing]")}  [{ctx}]");
            }

            // RouteList manipulation (addFirst يعني التطبيق يحقن route)
            if (content.Contains("Lgov/nist/javax/sip/header/RouteList;->addFirst") ||
                content.Contains("Lgov/nist/javax/sip/header/RouteList;->add"))
            {
                lock (report.HijackingIndicators)
                    report.HijackingIndicators.Add(
                        $"RouteList manipulation detected in {ctx} — possible route injection " +
                        $"[android-36/gov/nist/javax/sip/header/RouteList.java]");
            }
        }

        // ── O: CSeq / re-INVITE / UPDATE Patterns (CSeq.java) ────────────────
        // المرجع: android-36/gov/nist/javax/sip/header/CSeq.java          (5.3 KB)
        //         android-36/gov/nist/javax/sip/stack/SIPClientTransaction.java (68 KB)
        //
        // CSeq يُرقّم كل transaction داخل dialog بالترتيب.
        // إذا APK يضبط CSeq وفق ترتيب غير متسق → possible session confusion.
        // re-INVITE (INVITE داخل dialog) يُستخدم لتعديل media session.
        //
        // الكشف:
        //  • const-string نصوص "re-INVITE" أو UPDATE
        //  • SIPClientTransaction: forked response (ClientTx يتحكم في forked INVITEs)
        //  • CSeq.setMethod("INVITE") بعد dialog establishment
        // ─────────────────────────────────────────────────────────────────────
        {
            // re-INVITE strings
            if (content.Contains("\"re-INVITE\"") || content.Contains("re-invite") ||
                Regex.IsMatch(content, @"const-string(?:/jumbo)?\s+\w[\w\d]*,\s+""(re-?INVITE|UPDATE|PRACK)""",
                    RegexOptions.Multiline | RegexOptions.IgnoreCase))
            {
                lock (report.SipMethods)
                {
                    report.SipMethods.Add("re-INVITE");
                    report.SipMethods.Add("UPDATE");
                }
                lock (report.HijackingIndicators)
                    report.HijackingIndicators.Add(
                        $"re-INVITE/UPDATE detected in {ctx} — mid-session media modification " +
                        $"[SIPClientTransaction.java, CSeq.java]");
            }

            // forked INVITE (SIPClientTransaction.forkMerge)
            if (content.Contains("forkMerge") || content.Contains("forkedInvite") ||
                content.Contains("Lgov/nist/javax/sip/stack/SIPClientTransaction;->merge"))
            {
                lock (report.HijackingIndicators)
                    report.HijackingIndicators.Add(
                        $"Forked INVITE handling in {ctx} — fork-merge algorithm exposes dialog state " +
                        $"[android-36/gov/nist/javax/sip/stack/SIPClientTransaction.java §forkMerge]");
            }
        }

        // ── P: Anonymous TLS Cipher Suites (SipStackImpl.java cipherSuites) ──
        // المرجع: android-36/gov/nist/javax/sip/SipStackImpl.java L469-478
        //
        // SipStackImpl يدعم بشكل افتراضي:
        //   "TLS_DH_anon_WITH_AES_128_CBC_SHA"   ← لا مصادقة للخادم!
        //   "SSL_DH_anon_WITH_3DES_EDE_CBC_SHA"  ← ANON = MiTM trivial
        //
        // إذا APK يُفعّل هذه العُدد → MiTM بدون أي شهادة ممكن.
        // RFC3261 §26.3.1 يُلزم بـ TLS_RSA_WITH_AES_128_CBC_SHA كحد أدنى.
        // ─────────────────────────────────────────────────────────────────────
        {
            var anonCiphers = new[]
            {
                "TLS_DH_anon_WITH_AES_128_CBC_SHA",
                "SSL_DH_anon_WITH_3DES_EDE_CBC_SHA",
                "TLS_ECDH_anon",
                "SSL_DH_anon"
            };

            foreach (string cipher in anonCiphers)
            {
                if (content.Contains($"\"{cipher}\"") || content.Contains(cipher.Replace("_", "-")))
                {
                    lock (report.TlsProtocols)
                        report.TlsProtocols.Add(
                            $"⛔ ANON Cipher: {cipher}  [{ctx}] — MiTM trivial, no server auth!");

                    lock (report.HijackingIndicators)
                        report.HijackingIndicators.Add(
                            $"Anonymous TLS cipher active: {cipher} — disables certificate verification " +
                            $"[android-36/gov/nist/javax/sip/SipStackImpl.java L469, RFC3261 §26.3.1]");
                }
            }

            // weak cipher: 3DES
            if (content.Contains("3DES") || content.Contains("3des"))
            {
                lock (report.TlsProtocols)
                    report.TlsProtocols.Add(
                        $"⚠ 3DES cipher detected in {ctx} — SWEET32 attack (CVE-2016-2183)");
            }

            // الأمثل: TLS_RSA_WITH_AES_256_GCM_SHA384 أو TLS_AES_256_GCM_SHA384 (TLS 1.3)
            bool hasStrongCipher = content.Contains("AES_256_GCM") || content.Contains("TLS_AES_") ||
                                   content.Contains("TLS1_3") || content.Contains("TLSv1.3");
            if (hasStrongCipher)
            {
                lock (report.SocketFlags)
                    report.SocketFlags.Add(
                        $"Strong cipher/TLS 1.3 detected in {ctx} ✅ [SipStackImpl.java setEnabledCipherSuites]");
            }
        }

        // ── Q: SDP Media Negotiation Leakage (UDPMessageChannel + SDP body) ──
        // المرجع: android-36/gov/nist/javax/sip/stack/UDPMessageChannel.java (35 KB)
        //
        // INVITE body يحتوي SDP (Session Description Protocol — RFC 4566).
        // إذا APK يُضمّن SDP: يكشف عن:
        //  • c= (connection field) — عنوان IP للـ media
        //  • m= (media field)     — نوع وبروتوكول الوسائط (audio/video/application)
        //  • a=rtpmap            — codec المستخدَم
        //  • a=crypto            — SRTP keys (إذا مشفَّرة)
        // ─────────────────────────────────────────────────────────────────────
        {
            // SDP connection field (c=IN IP4 x.x.x.x)
            foreach (Match m in Regex.Matches(content,
                @"const-string(?:/jumbo)?\s+\w[\w\d]*,\s+""(c=IN IP[46]\s+[^\s""]{4,64})""",
                RegexOptions.Multiline))
            {
                lock (report.Endpoints)
                    report.Endpoints.Add(new SipEndpoint
                    {
                        Host      = m.Groups[1].Value.Split(' ').LastOrDefault() ?? "?",
                        Port      = "RTP/dynamic",
                        Transport = "RTP/RTCP",
                        Context   = $"SDP c= field [{ctx}]",
                        Source    = Path.GetFileName(filePath)
                    });
            }

            // SDP media field (m=audio port RTP/AVP)
            foreach (Match m in Regex.Matches(content,
                @"const-string(?:/jumbo)?\s+\w[\w\d]*,\s+""(m=(?:audio|video|application)\s+\d+\s+RTP[^\s""]{0,60})""",
                RegexOptions.Multiline))
            {
                lock (report.SocketFlags)
                    report.SocketFlags.Add(
                        $"SDP media descriptor: {m.Groups[1].Value}  [{ctx}]");
            }

            // SRTP key في SDP — a=crypto line (تسريب مفتاح التشفير)
            foreach (Match m in Regex.Matches(content,
                @"const-string(?:/jumbo)?\s+\w[\w\d]*,\s+""(a=crypto:[^""]{8,256})""",
                RegexOptions.Multiline))
            {
                lock (report.AuthHeaders)
                    report.AuthHeaders.Add(
                        $"🔴 SRTP crypto key in SDP: {m.Groups[1].Value[..Math.Min(60, m.Groups[1].Value.Length)]}... [{ctx}]");
            }

            // UDPMessageChannel direct usage (plaintext SIP over UDP)
            if (content.Contains("Lgov/nist/javax/sip/stack/UDPMessageChannel;"))
            {
                lock (report.SocketFlags)
                    report.SocketFlags.Add(
                        $"UDPMessageChannel used directly in {ctx} — SIP messages unencrypted " +
                        $"[android-36/gov/nist/javax/sip/stack/UDPMessageChannel.java]");
            }
        }

        // ── R: MESSAGE Method Exfiltration (SIP IM / data channel) ──────────
        // المرجع: android-36/gov/nist/javax/sip/stack/SIPServerTransaction.java (73 KB)
        //
        // SIP MESSAGE method (RFC 3428) يُرسل نص عادي مباشرةً ضمن SIP session.
        // يُستخدم أحياناً في RAT/spyware كـ covert data exfiltration channel:
        //  - لا يتطلب RTP session
        //  - يمر عبر port 5060 (الذي يُسمح به في firewalls)
        //  - يمكن تضمين binary data مُشفّرة بـ Base64
        // ─────────────────────────────────────────────────────────────────────
        {
            bool usesMessage = content.Contains("\"MESSAGE\"") ||
                               content.Contains("SIPServerTransaction") &&
                               content.Contains("MESSAGE");

            if (usesMessage)
            {
                lock (report.SipMethods)
                    report.SipMethods.Add("MESSAGE");

                // هل يُرسل محتوى Base64 (بيانات مُشفّرة)?
                bool hasBase64Body = Regex.IsMatch(content,
                    @"Content-Type:\s*application/octet-stream|Content-Transfer-Encoding:\s*base64",
                    RegexOptions.IgnoreCase);

                lock (report.HijackingIndicators)
                    report.HijackingIndicators.Add(
                        $"SIP MESSAGE method detected in {ctx}" +
                        (hasBase64Body ? " — binary/Base64 body (possible covert exfil channel)" : "") +
                        $" [RFC3428, SIPServerTransaction.java — covert C2 channel via port 5060]");
            }
        }

        // ── S: SUBSCRIBE/NOTIFY Event Spoofing ───────────────────────────────
        // المرجع: android-36/gov/nist/javax/sip/stack/SIPTransactionStack.java (91 KB)
        //         android-36/gov/nist/javax/sip/header/Event.java (4.8 KB)
        //
        // SUBSCRIBE/NOTIFY يُنشئ event subscription (مثل presence, message-summary).
        // إذا APK لا يتحقق من Event header → spoofed NOTIFY:
        //  • event=presence  → كشف حالة المستخدم
        //  • event=reg       → تتبع registration state
        //  • event=refer     → إعادة توجيه مكالمات خفية
        // ─────────────────────────────────────────────────────────────────────
        {
            var suspiciousEvents = new[]
            {
                ("presence",        "كشف حالة المستخدم — privacy violation"),
                ("reg",             "تتبع registration state"),
                ("refer",           "إعادة توجيه مكالمات خفية"),
                ("message-summary", "الاطلاع على ملخص الرسائل"),
                ("dialog",          "مراقبة dialog state — RFC6665")
            };

            foreach (var (evtName, evtRisk) in suspiciousEvents)
            {
                if (Regex.IsMatch(content,
                    $@"const-string(?:/jumbo)?\s+\w[\w\d]*,\s+""{evtName}""",
                    RegexOptions.Multiline | RegexOptions.IgnoreCase))
                {
                    lock (report.SipMethods)
                    {
                        report.SipMethods.Add("SUBSCRIBE");
                        report.SipMethods.Add("NOTIFY");
                    }
                    lock (report.HijackingIndicators)
                        report.HijackingIndicators.Add(
                            $"SUBSCRIBE event='{evtName}' detected in {ctx} — {evtRisk} " +
                            $"[android-36/gov/nist/javax/sip/header/Event.java, SIPTransactionStack.java]");
                }
            }
        }

        // ── T: SIP REFER Method — Call Transfer & Clickjacking ───────────────
        // المرجع: android-36/gov/nist/javax/sip/header/ReferTo.java (5 KB)
        //         android-36/gov/nist/javax/sip/SipStackExt.java
        //
        // REFER method (RFC 3515) يُحوّل مكالمة لجهة ثالثة.
        // يُستخدم في:
        //  • Call Transfer الشرعي
        //  • SIP Clickjacking Attack (تحويل لـ premium number)
        //  • Replaces header (SipStackExt.getReplacesDialog) — dialog injection
        // ─────────────────────────────────────────────────────────────────────
        {
            bool usesRefer = content.Contains("\"REFER\"") ||
                             content.Contains("Lgov/nist/javax/sip/header/ReferTo;");

            if (usesRefer)
            {
                lock (report.SipMethods)
                    report.SipMethods.Add("REFER");

                // Refer-To: هل يُحوّل لـ PSTN premium? (TEL URI)
                foreach (Match m in Regex.Matches(content,
                    @"const-string(?:/jumbo)?\s+\w[\w\d]*,\s+""(Refer-To:\s*<tel:[^""]{4,128})""",
                    RegexOptions.Multiline))
                {
                    lock (report.HijackingIndicators)
                        report.HijackingIndicators.Add(
                            $"REFER to TEL URI: {m.Groups[1].Value} — possible premium/PSTN hijack [{ctx}]");
                }

                // Replaces header (SipStackExt — dialog hijacking)
                if (content.Contains("Replaces") ||
                    content.Contains("Lgov/nist/javax/sip/SipStackExt;->getReplacesDialog"))
                {
                    lock (report.HijackingIndicators)
                        report.HijackingIndicators.Add(
                            $"Replaces header in REFER context in {ctx} — dialog hijacking via " +
                            $"SipStackExt.getReplacesDialog() [android-36/gov/nist/javax/sip/SipStackExt.java §getReplacesDialog]");
                }
            }
        }
    }

    // ── manifest scanner ───────────────────────────────────────────────────────

    private void ScanManifestForSip(string workDir, SipSecurityReport report)
    {
        // AndroidManifest.xml — uses-permission SIP
        string manifestPath = Path.Combine(workDir, "AndroidManifest.xml");
        if (File.Exists(manifestPath))
        {
            string manifest = File.ReadAllText(manifestPath, Encoding.UTF8);

            // SIP permissions
            foreach (Match m in Regex.Matches(manifest,
                @"android\.permission\.(USE_SIP|MANAGE_SIP|INTERNET|CHANGE_NETWORK_STATE|ACCESS_WIFI_STATE)",
                RegexOptions.IgnoreCase))
            {
                lock (report.SocketFlags)
                    report.SocketFlags.Add($"Permission: {m.Value}");
            }

            // uses-feature SIP
            if (manifest.Contains("android.software.sip") ||
                manifest.Contains("android.hardware.sip.voip"))
                lock (report.SocketFlags)
                    report.SocketFlags.Add("Feature declared: android.software.sip / sip.voip");
        }

        // network_security_config.xml — كشف cleartext traffic للـ SIP
        foreach (string nsFile in Directory.EnumerateFiles(workDir, "network_security_config.xml",
            SearchOption.AllDirectories))
        {
            string ns = File.ReadAllText(nsFile, Encoding.UTF8);
            if (ns.Contains("cleartextTrafficPermitted=\"true\""))
                lock (report.HijackingIndicators)
                    report.HijackingIndicators.Add(
                        "⚠️ cleartextTrafficPermitted=true — SIP يُرسل بدون TLS (بيانات مكشوفة)");

            // domain pinning
            if (Regex.IsMatch(ns, @"<pin-set", RegexOptions.IgnoreCase))
                lock (report.SocketFlags)
                    report.SocketFlags.Add("Certificate Pinning مُفعَّل في network_security_config");
        }
    }

    // ── SIP URI parser ─────────────────────────────────────────────────────────

    /// <summary>
    /// يُحلّل SIP URI حقيقي ويستخرج المكوّنات.
    /// البنية (RFC 3261 §19.1): sip:user@host:port;transport=tcp
    ///
    /// مبني على SIPConstants.DEFAULT_PORT=5060 , DEFAULT_TLS_PORT=5061
    /// وبنية HopImpl.java التي تحتوي host/port/transport كـ fields منفصلة.
    /// </summary>
    private SipEndpoint ParseSipUri(string uri, string ctx, string source)
    {
        var ep = new SipEndpoint { Context = ctx, Source = source };

        // هل هو SIPS (مشفّر بـ TLS دائماً)؟
        bool isSips = uri.StartsWith("sips:", StringComparison.OrdinalIgnoreCase);
        ep.Transport = isSips ? "TLS" : "TCP/UDP";

        // استخراج transport parameter أولاً
        var transportM = Regex.Match(uri, @";transport=(\w+)", RegexOptions.IgnoreCase);
        if (transportM.Success)
            ep.Transport = transportM.Groups[1].Value.ToUpper();

        // إزالة scheme (sip: أو sips:)
        string rest = Regex.Replace(uri, @"^sips?:", "", RegexOptions.IgnoreCase);

        // إزالة user info (user@)
        int atIdx = rest.IndexOf('@');
        if (atIdx >= 0) rest = rest.Substring(atIdx + 1);

        // إزالة parameters (;...)
        int semicolon = rest.IndexOf(';');
        if (semicolon >= 0) rest = rest.Substring(0, semicolon);

        // استخراج host:port
        var hostPortM = Regex.Match(rest, @"^([a-zA-Z0-9.\-_]+)(?::(\d{1,5}))?$");
        if (hostPortM.Success)
        {
            ep.Host = hostPortM.Groups[1].Value;
            ep.Port = hostPortM.Groups[2].Success
                ? hostPortM.Groups[2].Value
                : (isSips ? "5061" : "5060");
        }
        else
        {
            ep.Host = rest;
            ep.Port = isSips ? "5061" : "5060";
        }

        // هل المنفذ افتراضي SIP؟
        ep.IsDefault = ep.Port == "5060" || ep.Port == "5061";

        return ep;
    }

    // ── threat scoring ────────────────────────────────────────────────────────

    /// <summary>
    /// حساب ThreatScore مبني على المعطيات الحقيقية لـ NIST SIP stack.
    ///
    /// معايير التسجيل:
    ///  • ClearText SIP (بدون TLS) على port 5060 = +25
    ///  • endpoints خارجية غير مشفّرة = +15 لكل واحد
    ///  • dialog session triple exposed = +20 لكل حالة
    ///  • BRANCH_MAGIC_COOKIE raw hardcoded = +10 لكل واحد
    ///  • outboundProxy مُشيَّر صراحةً = +10
    ///  • Call-ID raw hardcoded = +15
    ///  • cacheServerConnections (يتيح connection reuse attack) = +5
    ///  • clearTextTrafficPermitted = +30
    ///  • TLS weak protocols (TLSv1.0/SSLv3) = +20
    ///  • SipMethods حساسة (INVITE/REGISTER بدون auth) = +10
    /// </summary>
    private void ComputeSipThreatScore(SipSecurityReport report)
    {
        int score = 0;

        // endpoints غير مشفّرة
        int plainTextEndpoints = report.Endpoints.Count(ep =>
            !ep.IsEncrypted && ep.Port == "5060");
        score += Math.Min(plainTextEndpoints * 15, 45);

        // endpoints TLS (يؤثر إيجابياً — نخفض النتيجة)
        int tlsEndpoints = report.Endpoints.Count(ep => ep.IsEncrypted);
        if (tlsEndpoints > 0 && plainTextEndpoints == 0) score -= 10;

        // dialog session exposure
        score += Math.Min(report.HijackingIndicators.Count * 20, 60);

        // cleartext check
        bool hasClearText = report.HijackingIndicators.Any(h =>
            h.Contains("cleartextTrafficPermitted"));
        if (hasClearText) score += 30;

        // BRANCH_MAGIC_COOKIE hardcoded (يشير لكتابة SIP stack يدوياً)
        score += Math.Min(report.BranchParams.Count * 10, 30);

        // Call-ID raw
        score += Math.Min(report.CallIds.Count * 15, 30);

        // outboundProxy explicit
        score += Math.Min(report.OutboundProxies.Count * 10, 20);

        // Socket caching (يُتيح connection reuse attacks)
        if (report.SocketFlags.Any(f => f.Contains("cacheServer"))) score += 5;

        // weak TLS
        bool weakTls = report.TlsProtocols.Any(t =>
            t.Contains("SSLv3") || t.Contains("TLSv1.0") || t.Contains("TLSv1.1"));
        if (weakTls) score += 20;

        // Auth headers exposed (credential leak)
        score += Math.Min(report.AuthHeaders.Count * 15, 30);

        // SIP methods حساسة مع REGISTER
        if (report.SipMethods.Contains("REGISTER")) score += 10;
        if (report.SipMethods.Contains("INVITE"))   score += 5;

        report.ThreatScore = Math.Clamp(score, 0, 100);
    }

    // ── display result ─────────────────────────────────────────────────────────

    private void DisplaySipReport(SipSecurityReport r)
    {
        Dispatcher.Invoke(() =>
        {
            LogMessage("\n╔════════════════════════════════════════════════════════════╗");
            LogMessage("║   📊 نتائج SIP Stack Security Analyzer                      ║");
            LogMessage("║   NIST JAIN-SIP / android-36 / Aleppo University Research   ║");
            LogMessage("╚════════════════════════════════════════════════════════════╝");
            LogMessage($"   APK : {r.ApkName}");
            LogMessage($"   مستوى التهديد : {r.ThreatLevel}  (Score={r.ThreatScore}/100)");

            // ملخص
            LogMessage($"\n📋 ملخص سريع:");
            if (r.SipSmaliFiles.Count > 0)
                LogMessage($"  📄 ملفات Smali تحتوي SIP   : {r.SipSmaliFiles.Count}");
            if (r.Endpoints.Count > 0)
                LogMessage($"  🌐 SIP Endpoints           : {r.Endpoints.Count}");
            if (r.OutboundProxies.Count > 0)
                LogMessage($"  🔀 Outbound Proxies        : {r.OutboundProxies.Count}");
            if (r.ViaHeaders.Count > 0)
                LogMessage($"  📡 Via Headers             : {r.ViaHeaders.Count}");
            if (r.BranchParams.Count > 0)
                LogMessage($"  🔑 Branch Params (z9hG4bK) : {r.BranchParams.Count}");
            if (r.SipMethods.Count > 0)
                LogMessage($"  📞 SIP Methods             : {string.Join(", ", r.SipMethods.OrderBy(m => m))}");
            if (r.TlsProtocols.Count > 0)
                LogMessage($"  🔐 TLS Protocols           : {r.TlsProtocols.Count} مُكتشف");
            if (r.AuthHeaders.Count > 0)
                LogMessage($"  🔒 Auth Headers (leak risk): {r.AuthHeaders.Count}");
            if (r.CallIds.Count > 0)
                LogMessage($"  📲 Call-IDs                : {r.CallIds.Count}");
            if (r.HijackingIndicators.Count > 0)
                LogMessage($"  ⚠️  Hijacking Indicators    : {r.HijackingIndicators.Count}");

            // SIP Endpoints
            if (r.Endpoints.Count > 0)
            {
                LogMessage($"\n🌐 SIP Endpoints ({r.Endpoints.Count}):");
                foreach (var ep in r.Endpoints.DistinctBy(x => $"{x.Host}:{x.Port}:{x.Transport}"))
                {
                    string enc = ep.IsEncrypted ? "🔐 Encrypted" : "⚠️  Plain";
                    string def = ep.IsDefault   ? " [default port]" : "";
                    LogMessage($"  • {ep.Transport}://{ep.Host}:{ep.Port} — {enc}{def}");
                    LogMessage($"    └─ [{ep.Context}] ← {ep.Source}");
                }
            }

            // Outbound Proxies
            if (r.OutboundProxies.Count > 0)
            {
                LogMessage($"\n🔀 Outbound SIP Proxies ({r.OutboundProxies.Count}):");
                foreach (var p in r.OutboundProxies)
                    LogMessage($"  • {p}");
            }

            // Via Headers
            if (r.ViaHeaders.Count > 0)
            {
                LogMessage($"\n📡 Via Headers (SIP proxy chain) ({r.ViaHeaders.Count}):");
                foreach (var v in r.ViaHeaders.Take(10))
                    LogMessage($"  • {v}");
                if (r.ViaHeaders.Count > 10)
                    LogMessage($"  ... و {r.ViaHeaders.Count - 10} أخرى");
            }

            // Branch Params
            if (r.BranchParams.Count > 0)
            {
                LogMessage($"\n🔑 Branch Parameters z9hG4bK ({r.BranchParams.Count}) — RFC3261 §8.1.1.7:");
                foreach (var b in r.BranchParams.Take(5))
                    LogMessage($"  • {b}");
            }

            // TLS
            if (r.TlsProtocols.Count > 0)
            {
                LogMessage($"\n🔐 TLS Protocols ({r.TlsProtocols.Count}):");
                var distinct = r.TlsProtocols
                    .Select(t => t.Split(' ')[0])
                    .Distinct().ToList();
                bool hasWeak = distinct.Any(d => d.Contains("SSLv3") || d.Contains("TLSv1.0") || d.Contains("TLSv1.1"));
                foreach (string proto in distinct)
                {
                    bool weak = proto.Contains("SSLv3") || proto.Contains("TLSv1.0") || proto.Contains("TLSv1.1");
                    LogMessage($"  • {proto}  {(weak ? "⚠️ WEAK — يجب الترقية إلى TLS 1.3" : "✅")}");
                }
            }

            // Auth Headers
            if (r.AuthHeaders.Count > 0)
            {
                LogMessage($"\n🔒 Auth Headers (credential exposure risk) ({r.AuthHeaders.Count}):");
                foreach (var a in r.AuthHeaders.Take(5))
                    LogMessage($"  • {a}");
            }

            // Call IDs
            if (r.CallIds.Count > 0)
            {
                LogMessage($"\n📲 Call-IDs (session identifiers) ({r.CallIds.Count}):");
                foreach (var c in r.CallIds.Take(5))
                    LogMessage($"  • {c}");
            }

            // Hijacking Indicators
            if (r.HijackingIndicators.Count > 0)
            {
                LogMessage($"\n⚠️  Dialog Hijacking Indicators ({r.HijackingIndicators.Count}):");
                foreach (var h in r.HijackingIndicators)
                    LogMessage($"  ⛔ {h}");
                LogMessage("  ℹ️  المرجع: SIPDialog.java — تثليث (Call-ID + From-tag + To-tag) يُتيح session injection");
            }

            // Socket Flags
            if (r.SocketFlags.Count > 0)
            {
                LogMessage($"\n🔌 Socket & Transport Flags ({r.SocketFlags.Count}):");
                foreach (var f in r.SocketFlags.Distinct())
                    LogMessage($"  • {f}");
            }

            // Congestion info
            var cgi = r.CongestionInfo;
            if (cgi.MaxConnections > 0 || cgi.ServerTxHighWater > 0)
            {
                LogMessage("\n📊 Congestion Control (SIPTransactionStack):");
                if (cgi.MaxConnections  > 0) LogMessage($"  maxConnections     = {cgi.MaxConnections}");
                if (cgi.ServerTxHighWater > 0) LogMessage($"  serverTx HighWater = {cgi.ServerTxHighWater}");
                if (cgi.ServerTxLowWater  > 0) LogMessage($"  serverTx LowWater  = {cgi.ServerTxLowWater}");
            }

            // SIP Smali Files
            if (r.SipSmaliFiles.Count > 0)
            {
                LogMessage($"\n📄 ملفات Smali تحتوي SIP references ({r.SipSmaliFiles.Count}):");
                foreach (var f in r.SipSmaliFiles.Take(15))
                    LogMessage($"  • {f}");
                if (r.SipSmaliFiles.Count > 15)
                    LogMessage($"  ... و {r.SipSmaliFiles.Count - 15} ملف إضافي");
            }

            LogMessage("\n──────────────────────────────────────────────────────");
            LogMessage($"✅ انتهى التحليل | ThreatScore = {r.ThreatScore}/100 | {r.ThreatLevel}");
            LogMessage("──────────────────────────────────────────────────────\n");

            // حفظ التقرير
            SaveSipReport(r);
        });
    }

    // ── report file ────────────────────────────────────────────────────────────

    private void SaveSipReport(SipSecurityReport r)
    {
        try
        {
            string t    = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string path = Path.Combine(_outputDir, $"sip_security_{r.ApkName}_{t}.txt");

            var sb = new StringBuilder();
            sb.AppendLine("════════════════════════════════════════════════════════════════");
            sb.AppendLine(" SIP Stack Security Analyzer Report");
            sb.AppendLine(" Aleppo University Research Project — NIST JAIN-SIP / Android 36");
            sb.AppendLine($" Date  : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($" APK   : {r.ApkPath}");
            sb.AppendLine($" Score : {r.ThreatScore}/100  Level: {r.ThreatLevel}");
            sb.AppendLine("════════════════════════════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine("[References]");
            sb.AppendLine("  SIPConstants.DEFAULT_PORT     = 5060");
            sb.AppendLine("  SIPConstants.DEFAULT_TLS_PORT = 5061");
            sb.AppendLine("  SIPConstants.BRANCH_MAGIC_COOKIE = z9hG4bK  (RFC3261 §8.1.1.7)");
            sb.AppendLine("  IOHandler: TCP/TLS socket caching + 8KB writeChunks + semaphore");
            sb.AppendLine("  SIPTransactionStack: dialog/tx tables, congestion thresholds, outboundProxy");
            sb.AppendLine("  SIPDialog: Call-ID + From-tag + To-tag = session identifier triple");
            sb.AppendLine("  MessageChannel: Via headers, transport abstraction, peer address/port");
            sb.AppendLine();
            sb.AppendLine("[SIP Smali Files]");
            r.SipSmaliFiles.ForEach(f => sb.AppendLine($"  {f}"));
            sb.AppendLine();
            sb.AppendLine("[SIP Endpoints]");
            foreach (var ep in r.Endpoints.DistinctBy(x => $"{x.Host}:{x.Port}:{x.Transport}"))
                sb.AppendLine($"  {ep.Transport}://{ep.Host}:{ep.Port}  Encrypted={ep.IsEncrypted}  [{ep.Context}]");
            sb.AppendLine();
            sb.AppendLine("[Outbound Proxies]");
            r.OutboundProxies.ForEach(p => sb.AppendLine($"  {p}"));
            sb.AppendLine();
            sb.AppendLine("[Via Headers]");
            r.ViaHeaders.ForEach(v => sb.AppendLine($"  {v}"));
            sb.AppendLine();
            sb.AppendLine("[Branch Params z9hG4bK]");
            r.BranchParams.ForEach(b => sb.AppendLine($"  {b}"));
            sb.AppendLine();
            sb.AppendLine("[SIP Methods]");
            sb.AppendLine($"  {string.Join(", ", r.SipMethods.OrderBy(m => m))}");
            sb.AppendLine();
            sb.AppendLine("[TLS Protocols]");
            r.TlsProtocols.ForEach(t2 => sb.AppendLine($"  {t2}"));
            sb.AppendLine();
            sb.AppendLine("[Auth Headers]");
            r.AuthHeaders.ForEach(a => sb.AppendLine($"  {a}"));
            sb.AppendLine();
            sb.AppendLine("[Call-IDs]");
            r.CallIds.ForEach(c => sb.AppendLine($"  {c}"));
            sb.AppendLine();
            sb.AppendLine("[Dialog Hijacking Indicators]");
            r.HijackingIndicators.ForEach(h => sb.AppendLine($"  {h}"));
            sb.AppendLine();
            sb.AppendLine("[Socket Flags]");
            r.SocketFlags.Distinct().ToList().ForEach(f => sb.AppendLine($"  {f}"));

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            LogMessage($"💾 التقرير محفوظ: {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            LogMessage($"  ⚠ فشل حفظ تقرير SIP: {ex.Message}");
        }
    }

    #endregion // SIP Stack Security Analyzer


        #endregion

    // ══════════════════════════════════════════════════════════════════════
    //  Preferences Tree Extractor Data Models
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>مستوى خطورة كل اكتشاف في Preferences Extractor</summary>
    internal enum PrefRisk { Low = 0, Medium = 1, High = 2, Critical = 3 }

    internal sealed class PrefsFinding
    {
        public string   PrefsType { get; set; } = "";
        public PrefRisk RiskLevel { get; set; } = PrefRisk.Low;
        public string   Source    { get; set; } = "";
        public string   Context   { get; set; } = "";
        public string   FilePaths { get; set; } = "";

        public override bool Equals(object? obj) =>
            obj is PrefsFinding f && f.PrefsType == PrefsType && f.Source == Source;

        public override int GetHashCode() =>
            HashCode.Combine(PrefsType, Source);
    }

    internal sealed class PrefsDetectionReport
    {
        public string ApkName { get; set; } = "";
        public string ApkPath { get; set; } = "";
        public HashSet<PrefsFinding> Findings { get; } = new();
        public List<string>          Errors   { get; } = new();

        // خصائص محسوبة — تُستخدم في التقرير والحفظ
        public bool UsesJavaUtilPrefs  => Findings.Any(f => f.PrefsType.Contains("java.util.prefs"));
        public bool UsesSocketHandler  => Findings.Any(f => f.PrefsType.Contains("SocketHandler"));
        public bool UsesEncryptedPrefs => Findings.Any(f => f.PrefsType.Contains("Encrypted"));
    }

    // ══════════════════════════════════════════════════════════════════════
    // نماذج بيانات DEX Entropy Fingerprinter
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>مستوى التشفير المكتشف في الـ APK</summary>
    internal enum ObfuscationLevel
    {
        None,       // بدون تشفير (Debug build)
        Light,      // تشفير خفيف (بعض الخيارات)
        ProGuard,   // ProGuard أو R8 Standard
        R8Full,     // R8 Full Mode (Google — تشفير كامل)
        DexGuard    // DexGuard (تجاري — تشفير enterprise)
    }

    /// <summary>@FlaggedApi مكتشف في android-36 sources</summary>
    internal sealed class FlaggedApiEntry
    {
        public string ApiPath    { get; set; } = "";
        public string ClassName  { get; set; } = "";
        public string Category   { get; set; } = "";
        public string FlagName   { get; set; } = "";
        public bool   IsBaklava  { get; set; }
        public bool   IsSystemApi{ get; set; }
        public bool   ApkUsesIt  { get; set; }
    }

    /// <summary>تقرير كامل من DEX Entropy Fingerprinter</summary>
    internal sealed class EntropyFingerprintReport
    {
        // APK Info
        public string ApkPath { get; set; } = "";
        public string ApkName { get; set; } = "";

        // DEX Files (path, size)
        public List<(string path, long size)> DexFiles { get; } = new();
        public long TotalDexBytes { get; set; }
        public List<int> DexdumpExitCodes { get; } = new();

        // Code Elements (من dexdump الحقيقي)
        public HashSet<string> AllClassNames  { get; } = new(StringComparer.Ordinal);
        public HashSet<string> AllMethodNames { get; } = new(StringComparer.Ordinal);
        public List<string>    StringPool     { get; } = new();
        public Dictionary<string, int> StringFrequency { get; } = new(StringComparer.Ordinal);
        public HashSet<string> AndroidApiRefs { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<int>       MethodsPerClass{ get; } = new();

        // Counts
        public int NativeMethodCount      { get; set; }
        public int ReflectionCallCount    { get; set; }
        public int EncryptedStringCallCount { get; set; }

        // Shannon Entropy
        public double StringPoolCharEntropy  { get; set; }
        public double ClassNameEntropy       { get; set; }
        public double AvgStringLength        { get; set; }
        public double UniqueStringRatio      { get; set; }
        public int    Base64StringCount      { get; set; }
        public int    HexStringCount         { get; set; }

        // Obfuscation Analysis
        public double           ShortClassNameRatio       { get; set; }
        public double           DescriptiveClassNameRatio { get; set; }
        public int              UnicodeClassNameCount     { get; set; }
        public ObfuscationLevel ObfuscationLevel          { get; set; }

        // Android 36 @FlaggedApi
        public Dictionary<string, FlaggedApiEntry> FlaggedApisInSdk    { get; } = new();
        public List<FlaggedApiEntry>               FlaggedApisUsedByApk { get; } = new();

        // DEX Fingerprint
        public string ResearchFingerprintHash{ get; set; } = "";
        public string FingerprintComponents  { get; set; } = "";

        // Errors
        public List<string> Errors { get; } = new();
    }

    // ══════════════════════════════════════════════════════════════════════
    // نماذج بيانات Android 36 API Profiler
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>معلومات API واحدة من Android 16</summary>
    internal sealed record ApiInfo(string ClassName, string Category, string Description);

    /// <summary>نتيجة مطابقة API مستخدم في APK</summary>
    internal sealed record UsedApiEntry(string ApiPath, ApiInfo Info, bool IsDirectMatch);

    /// <summary>تقرير كامل من Android 36 Profiler</summary>
    internal sealed class Android36ProfilerReport
    {
        // APK Info (من aapt2)
        public string PackageName  { get; set; } = "";
        public string AppName      { get; set; } = "";
        public string VersionName  { get; set; } = "";
        public string VersionCode  { get; set; } = "";
        public int    MinSdk       { get; set; }
        public int    TargetSdk    { get; set; }

        // DEX Info (من dexdump)
        public int    DexCount     { get; set; }
        public long   TotalDexSize { get; set; }
        public List<string> DexFiles { get; } = new();

        // Class/Method References المستخرجة بـ dexdump
        public HashSet<string> AllClassReferences  { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> AllMethodReferences { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> AllStringConstants  { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        // قاعدة بيانات APIs (مُبنية من android-36/android/ sources)
        public Dictionary<string, ApiInfo>? Android36ApiDatabase { get; set; }
        public int NewApisFoundInSources { get; set; }

        // نتائج المطابقة
        public List<UsedApiEntry>         UsedAndroid36Apis   { get; } = new();
        public double                     CompatibilityScore  { get; set; }

        // Permissions & Features (من aapt2)
        public HashSet<string>             DeclaredPermissions { get; } = new();
        public HashSet<string>             DeclaredFeatures    { get; } = new();
        public HashSet<string>             NativeAbis          { get; } = new();
        public List<(string perm, string note)> RelevantPermissions { get; } = new();

        // أخطاء
        public List<string> Errors { get; } = new();
    }


    // ══════════════════════════════════════════════════════════════════════
    // نموذج البيانات لنتائج الاستخراج — يجمع العناصر بدون تكرار
    // ══════════════════════════════════════════════════════════════════════
    internal sealed class NetworkExtractResult
    {
        public List<(string val, string ctx)> Urls              { get; } = new();
        public List<(string val, string ctx)> ApiEndpoints      { get; } = new();
        public List<(string val, string ctx)> WebSockets        { get; } = new();
        public List<(string val, string ctx)> IpAddresses       { get; } = new();
        public List<(string val, string ctx)> Firebase          { get; } = new();
        public List<(string val, string ctx)> CloudServices     { get; } = new();
        public List<(string val, string ctx)> DeepLinks         { get; } = new();
        public List<(string val, string ctx)> Secrets           { get; } = new();
        public List<(string val, string ctx)> TokensAndWebhooks { get; } = new();
        public List<(string val, string ctx)> CryptoWallets     { get; } = new();
        // ── أنماط Android 36 الجديدة ──────────────────────────────────────
        public List<(string val, string ctx)> MacAddresses      { get; } = new();
        public List<(string val, string ctx, string detail)> Android36Protocols { get; } = new();

        private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);

        private void Add(List<(string, string)> list, string val, string ctx)
        {
            if (_seen.Add(val)) list.Add((val, ctx));
        }

        public void AddUrl(string v, string c)          => Add(Urls,          v, c);
        public void AddApiEndpoint(string v, string c)  => Add(ApiEndpoints,  v, c);
        public void AddWebSocket(string v, string c)    => Add(WebSockets,    v, c);
        public void AddIpAddress(string v, string c)    => Add(IpAddresses,   v, c);
        public void AddFirebase(string v, string c)     => Add(Firebase,      v, c);
        public void AddCloudService(string v, string c) => Add(CloudServices, v, c);
        public void AddTokensAndWebhooks(string v, string c) => Add(TokensAndWebhooks, v, c);
        public void AddCryptoWallet(string v, string c) => Add(CryptoWallets, v, c);
        public void AddDeepLink(string v, string c)     => Add(DeepLinks,     v, c);
        public void AddSecret(string v, string c)       => Add(Secrets,       v, c);
        public void AddMacAddress(string v, string c)   => Add(MacAddresses,  v, c);

        public void AddAndroid36Protocol(string val, string ctx, string detail)
        {
            if (_seen.Add(val + "|" + ctx))
                Android36Protocols.Add((val, ctx, detail));
        }

        // ── sun/net Security Findings (android-36/sun/net/) ───────────────────
        public List<SunNetFinding> SunNetFindings { get; } = new();

        public void AddSunNetFinding(string value, string ctx, string detail, SunNetRisk risk = SunNetRisk.Medium)
        {
            string key = $"sunnet|{value}|{ctx}";
            if (_seen.Add(key))
                SunNetFindings.Add(new SunNetFinding
                {
                    Value   = value,
                    Context = ctx,
                    Detail  = detail,
                    Risk    = risk
                });
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // C2 Panel Detection Data Models
    // ══════════════════════════════════════════════════════════════════

    internal enum C2ChannelType
    {
        TcpSocket, WebSocket, HttpC2, Https, Mqtt, Grpc,
        DnsC2, IcmpC2, BluetoothL2cap, ThreadIot, NearbyApi,
        ReverseShell, Unknown
    }

    internal sealed class C2Finding
    {
        public C2ChannelType ChannelType   { get; set; } = C2ChannelType.Unknown;
        public string        Host          { get; set; } = "";
        public string        Port          { get; set; } = "";
        public string        ConnectionKey { get; set; } = "";
        public string        FullValue     { get; set; } = "";
        public string        Source        { get; set; } = "";
        public string        Context       { get; set; } = "";
        public int           Confidence    { get; set; }
    }

    internal sealed class C2DetectionReport
    {
        public string ApkName  { get; set; } = "";
        public string ApkPath  { get; set; } = "";
        public List<C2Finding> Findings   { get; } = new();
        public List<string>    RawIps     { get; } = new();
        public List<string>    RawPorts   { get; } = new();
        public List<string>    RawKeys    { get; } = new();
        public List<string>    RawDomains { get; } = new();
        public List<string>    Errors     { get; } = new();
        public bool IsLikelyC2  => Findings.Any(f => f.Confidence >= 60);
        public int  ThreatScore => Findings.Count == 0 ? 0 : (int)Findings.Average(f => f.Confidence);
    }

    #endregion



// ─────────────────────────────────────────────────────────────────────────────
// SIP Stack Security Analyzer — Data Models
// مبنية على NIST JAIN-SIP من android-36/gov/nist/javax/sip/
//   SIPConstants.java  → DEFAULT_PORT=5060, DEFAULT_TLS_PORT=5061, BRANCH_MAGIC_COOKIE=z9hG4bK
//   SIPTransactionStack→ serverTransactionTableHighwaterMark=5000, maxConnections, outboundProxy
//   SIPDialog.java     → Call-ID + From-tag + To-tag = session identifier triple
//   IOHandler.java     → TCP/TLS/UDP socket caching, 8KB write chunks, Semaphore
// ─────────────────────────────────────────────────────────────────────────────

internal sealed class SipSecurityReport
{
    public string ApkName  { get; set; } = "";
    public string ApkPath  { get; set; } = "";

    // SIP endpoints مكتشفة (host:port:transport)
    public List<SipEndpoint> Endpoints          { get; } = new();
    // Via headers — يكشف SIP proxy chain الحقيقي
    public List<string>      ViaHeaders          { get; } = new();
    // outbound proxy مُعيَّن صراحةً (SIPTransactionStack.outboundProxy)
    public List<string>      OutboundProxies     { get; } = new();
    // branch parameters — z9hG4bK (BRANCH_MAGIC_COOKIE من SIPConstants)
    public List<string>      BranchParams        { get; } = new();
    // Call-IDs مكتشفة — session identifiers (SIPDialog)
    public List<string>      CallIds             { get; } = new();
    // SIP methods (INVITE/REGISTER/SUBSCRIBE/REFER/…)
    public HashSet<string>   SipMethods          { get; } = new(StringComparer.OrdinalIgnoreCase);
    // TLS/SSL protocols (IOHandler: sslsock.setEnabledProtocols)
    public List<string>      TlsProtocols        { get; } = new();
    // Authorization headers (credential leak risk)
    public List<string>      AuthHeaders         { get; } = new();
    // congestion thresholds (SIPTransactionStack)
    public SipCongestionInfo CongestionInfo      { get; set; } = new();
    // socket flags (cacheServerConnections, permissions…)
    public List<string>      SocketFlags         { get; } = new();
    // ملفات Smali تحتوي SIP references
    public List<string>      SipSmaliFiles       { get; } = new();
    // dialog hijacking indicators
    public List<string>      HijackingIndicators { get; } = new();
    // errors
    public List<string>      Errors              { get; } = new();

    public int    ThreatScore { get; set; }
    public string ThreatLevel =>
        ThreatScore >= 80 ? "🔴 CRITICAL" :
        ThreatScore >= 60 ? "🟠 HIGH"     :
        ThreatScore >= 40 ? "🟡 MEDIUM"   :
        ThreatScore >= 20 ? "🟢 LOW"      :
                            "✅ MINIMAL";
}

internal sealed class SipEndpoint
{
    public string Host      { get; set; } = "";
    public string Port      { get; set; } = "";
    public string Transport { get; set; } = ""; // TCP / TLS / UDP
    public bool   IsDefault { get; set; }       // port 5060 أو 5061

    // TLS أو SIPS = مشفّر (IOHandler.java يُميّز بين TCP و TLS)
    public bool   IsEncrypted =>
        Transport.Equals("TLS",  StringComparison.OrdinalIgnoreCase) ||
        Transport.Equals("SIPS", StringComparison.OrdinalIgnoreCase);

    public string Context { get; set; } = "";
    public string Source  { get; set; } = ""; // اسم ملف .smali
}

internal sealed class SipCongestionInfo
{
    // SIPTransactionStack.serverTransactionTableHighwaterMark = 5000 (default)
    public int  ServerTxHighWater { get; set; } = -1;
    public int  ServerTxLowWater  { get; set; } = -1;
    // SIPTransactionStack.clientTransactionTableHiwaterMark = 1000 (default)
    public int  ClientTxHighWater { get; set; } = -1;
    public int  ClientTxLowWater  { get; set; } = -1;
    // SIPTransactionStack.maxConnections = -1 (unlimited) by default
    public int  MaxConnections    { get; set; } = -1;
    // SIPTransactionStack.readTimeout = -1 (infinite) by default
    public int  ReadTimeout       { get; set; } = -1;
    public bool UnlimitedServerTx { get; set; } = true;
    public bool UnlimitedClientTx { get; set; } = true;
    }

    } // end class ApkClonerWindow


// ─────────────────────────────────────────────────────────────────────────────
// sun/net Security Findings — Data Models
// مبنية على android-36/sun/net/ المُقدَّم من Google
//
//   NetworkClient.java      → TCP/SOCKS base class, timeout properties
//   DefaultProxySelector.java→ SOCKS v4/v5, HTTP, FTP, nonProxyHosts bypass
//   SocksProxy.java         → SOCKS version (4 or 5)
//   FtpClient.java          → FTP/FTPS endpoints + credentials
//   TelnetInputStream.java  → Telnet plaintext protocol (NO TLS!)
//   TelnetOutputStream.java → Telnet plaintext protocol
//   IPAddressUtil.java      → IPv4 (strict 4-octet, Android-changed) + IPv6
//   MessageHeader.java      → HTTP header parsing (Authorization, X-API-Key…)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// مستوى خطورة نتيجة sun/net.
/// يتبع CVSS severity scale مبسطاً.
/// </summary>
internal enum SunNetRisk
{
    /// <summary>معلومة إضافية — لا خطر مباشر</summary>
    Info = 0,
    /// <summary>مستوى متوسط — يستحق الدراسة</summary>
    Medium = 1,
    /// <summary>مستوى عالٍ — تأثير محتمل</summary>
    High = 2,
    /// <summary>حرج — تأثير مباشر وخطير</summary>
    Critical = 3
}

/// <summary>
/// نتيجة واحدة من محركات sun/net الأمنية.
/// </summary>
internal sealed class SunNetFinding
{
    /// <summary>وصف الاكتشاف (يظهر في اللوج)</summary>
    public string Value   { get; set; } = "";
    /// <summary>سياق الملف — اسم الكلاس في Smali</summary>
    public string Context { get; set; } = "";
    /// <summary>تفاصيل أكاديمية ومرجع الملف المصدر من android-36/sun/net/</summary>
    public string Detail  { get; set; } = "";
    /// <summary>مستوى خطورة الاكتشاف</summary>
    public SunNetRisk Risk { get; set; } = SunNetRisk.Medium;

    public string RiskIcon => Risk switch
    {
        SunNetRisk.Critical => "🔴",
        SunNetRisk.High     => "🟠",
        SunNetRisk.Medium   => "🟡",
        _                   => "⚪"
    };
}

} // end namespace Ekhtibar.Windows
