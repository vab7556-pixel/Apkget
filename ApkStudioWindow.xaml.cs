using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace TcpServerApp
{
    public partial class ApkStudioWindow : Window
    {
        private string? _selectedApkPath;
        private string _toolsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools");
        private string _outputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "apk_output");
        private string _javaPath = "";

        public ApkStudioWindow()
        {
            InitializeComponent();
            InitializeDirectories();
            DetectJavaPath();
            LogMessage("🎉 مرحباً بك في APK Studio - محلل التطبيقات الاحترافي");
            LogMessage($"📁 مسار الأدوات: {_toolsPath}");
            LogMessage($"📂 مسار المخرجات: {_outputPath}");
            CheckTools();
        }

        private void DetectJavaPath()
        {
            // Check if JRE is bundled
            var bundledJava = Path.Combine(_toolsPath, "jre", "bin", "java.exe");
            if (File.Exists(bundledJava))
            {
                _javaPath = bundledJava;
                return;
            }

            // Use system Java
            _javaPath = "java";
        }

        private void InitializeDirectories()
        {
            try
            {
                if (!Directory.Exists(_outputPath))
                {
                    Directory.CreateDirectory(_outputPath);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ خطأ في إنشاء المجلدات: {ex.Message}");
            }
        }

        private void CheckTools()
        {
            LogMessage("\n🔍 فحص الأدوات المتاحة...");
            
            var apktoolJar = Path.Combine(_toolsPath, "apktool.jar");
            var apktoolBat = Path.Combine(_toolsPath, "apktool.bat");
            var uastJar = Path.Combine(_toolsPath, "uast.jar");
            var jadxJar1 = Path.Combine(_toolsPath, "jadx", "build", "jadx", "lib", "jadx-dev-all.jar");
            var jadxJar2 = Path.Combine(_toolsPath, "jadx", "build", "jadx-dev", "lib", "jadx-dev-all.jar");
            var jadxJar = File.Exists(jadxJar1) ? jadxJar1 : jadxJar2;
            var bundledJava = Path.Combine(_toolsPath, "jre", "bin", "java.exe");
            
            if (File.Exists(apktoolJar))
            {
                LogMessage("✅ apktool.jar - متوفر (فك وإعادة تجميع APK)");
            }
            else
            {
                LogMessage("⚠️ apktool.jar - غير موجود");
            }

            if (File.Exists(apktoolBat))
            {
                LogMessage("✅ apktool.bat - متوفر (واجهة سطر الأوامر)");
            }

            if (File.Exists(uastJar))
            {
                LogMessage("✅ uast.jar - متوفر (قراءة معلومات APK الكاملة)");
            }
            else
            {
                LogMessage("⚠️ uast.jar - غير موجود");
            }

            if (File.Exists(jadxJar))
            {
                LogMessage($"✅ jadx-dev-all.jar - متوفر (استخراج السورس كود Java)");
                LogMessage($"   📍 المسار: {jadxJar.Replace(_toolsPath, "tools")}");
            }
            else
            {
                LogMessage("⚠️ jadx - غير موجود (تحقق من build في مجلد tools/jadx)");
            }

            // Check Java
            if (File.Exists(bundledJava))
            {
                LogMessage($"✅ Java (Bundled) - متوفر في: {bundledJava}");
            }
            else
            {
                try
                {
                    var javaCheck = new ProcessStartInfo
                    {
                        FileName = "java",
                        Arguments = "-version",
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using (var process = Process.Start(javaCheck))
                    {
                        if (process != null)
                        {
                            var output = process.StandardError.ReadToEnd();
                            process.WaitForExit();
                            if (process.ExitCode == 0)
                            {
                                var version = output.Split('\n')[0];
                                LogMessage($"✅ Java (System) - {version.Trim()}");
                            }
                        }
                    }
                }
                catch
                {
                    LogMessage("⚠️ Java - غير مثبت أو غير متاح في PATH");
                    LogMessage("   💡 قم بتثبيت Java JDK من: https://www.oracle.com/java/technologies/downloads/");
                }
            }

            LogMessage("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");
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

        private void SelectApk_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "APK Files (*.apk)|*.apk|All Files (*.*)|*.*",
                Title = "اختر ملف APK"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _selectedApkPath = openFileDialog.FileName;
                ApkPathText.Text = _selectedApkPath;
                ApkPathText.Foreground = System.Windows.Media.Brushes.White;
                
                DecompileButton.IsEnabled = true;
                ExtractSourceButton.IsEnabled = true;
                ShowInfoButton.IsEnabled = true;
                
                LogMessage($"\n📦 تم اختيار الملف: {Path.GetFileName(_selectedApkPath)}");
                LogMessage($"📍 المسار الكامل: {_selectedApkPath}");
                LogMessage($"📏 الحجم: {FormatFileSize(new FileInfo(_selectedApkPath).Length)}");
            }
        }

        private async void DecompileApk_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedApkPath))
                return;

            ShowProgress("جاري فك تجميع APK...", "استخدام apktool");
            
            await Task.Run(() =>
            {
                try
                {
                    var apkName = Path.GetFileNameWithoutExtension(_selectedApkPath);
                    var outputDir = Path.Combine(_outputPath, $"{apkName}_decompiled");

                    Dispatcher.Invoke(() => LogMessage($"\n🔓 بدء فك التجميع..."));
                    Dispatcher.Invoke(() => LogMessage($"📂 المجلد الهدف: {outputDir}"));

                    if (Directory.Exists(outputDir))
                    {
                        Directory.Delete(outputDir, true);
                    }

                    var apktoolJar = Path.Combine(_toolsPath, "apktool.jar");
                    
                    if (!File.Exists(apktoolJar))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            LogMessage($"❌ apktool.jar غير موجود في: {apktoolJar}");
                            NotificationManager.Instance.Error("apktool.jar غير موجود", "APK Studio");
                        });
                        return;
                    }

                    var startInfo = new ProcessStartInfo
                    {
                        FileName = _javaPath,
                        Arguments = $"-jar \"{apktoolJar}\" d \"{_selectedApkPath}\" -o \"{outputDir}\" -f",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };

                    Dispatcher.Invoke(() => LogMessage($"🔧 الأمر: {_javaPath} -jar apktool.jar d ..."));

                    using (var process = Process.Start(startInfo))
                    {
                        if (process != null)
                        {
                            var output = process.StandardOutput.ReadToEnd();
                            var error = process.StandardError.ReadToEnd();
                            process.WaitForExit();

                            Dispatcher.Invoke(() =>
                            {
                                if (!string.IsNullOrEmpty(output))
                                {
                                    foreach (var line in output.Split('\n'))
                                    {
                                        if (!string.IsNullOrWhiteSpace(line))
                                            LogMessage($"   {line.Trim()}");
                                    }
                                }
                                
                                if (!string.IsNullOrEmpty(error))
                                {
                                    foreach (var line in error.Split('\n'))
                                    {
                                        if (!string.IsNullOrWhiteSpace(line))
                                            LogMessage($"   {line.Trim()}");
                                    }
                                }
                            });

                            if (process.ExitCode == 0 && Directory.Exists(outputDir))
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    LogMessage($"✅ تم فك التجميع بنجاح!");
                                    LogMessage($"📁 الملفات في: {outputDir}");
                                    
                                    // Count files
                                    try
                                    {
                                        var smaliFiles = Directory.GetFiles(outputDir, "*.smali", SearchOption.AllDirectories).Length;
                                        var xmlFiles = Directory.GetFiles(outputDir, "*.xml", SearchOption.AllDirectories).Length;
                                        LogMessage($"📊 ملفات Smali: {smaliFiles}");
                                        LogMessage($"📊 ملفات XML: {xmlFiles}");
                                    }
                                    catch { }
                                    
                                    RecompileButton.IsEnabled = true;
                                    NotificationManager.Instance.Success("تم فك التجميع بنجاح", "APK Studio");
                                });
                            }
                            else
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    LogMessage($"❌ فشل فك التجميع (Exit Code: {process.ExitCode})");
                                    NotificationManager.Instance.Error("فشل فك التجميع", "APK Studio");
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        LogMessage($"❌ خطأ: {ex.Message}");
                        LogMessage($"   Stack: {ex.StackTrace}");
                        NotificationManager.Instance.Error($"خطأ: {ex.Message}", "APK Studio");
                    });
                }
            });

            HideProgress();
        }

        private async void RecompileApk_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedApkPath))
                return;

            ShowProgress("جاري إعادة تجميع APK...", "استخدام apktool");

            await Task.Run(() =>
            {
                try
                {
                    var apkName = Path.GetFileNameWithoutExtension(_selectedApkPath);
                    var inputDir = Path.Combine(_outputPath, $"{apkName}_decompiled");
                    var outputApk = Path.Combine(_outputPath, $"{apkName}_recompiled.apk");

                    if (!Directory.Exists(inputDir))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            LogMessage($"❌ المجلد غير موجود: {inputDir}");
                            LogMessage($"💡 قم بفك التجميع أولاً");
                            NotificationManager.Instance.Warning("قم بفك التجميع أولاً", "APK Studio");
                        });
                        return;
                    }

                    Dispatcher.Invoke(() => LogMessage($"\n🔒 بدء إعادة التجميع..."));
                    Dispatcher.Invoke(() => LogMessage($"📂 المجلد المصدر: {inputDir}"));

                    var apktoolJar = Path.Combine(_toolsPath, "apktool.jar");
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = _javaPath,
                        Arguments = $"-jar \"{apktoolJar}\" b \"{inputDir}\" -o \"{outputApk}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };

                    using (var process = Process.Start(startInfo))
                    {
                        if (process != null)
                        {
                            var output = process.StandardOutput.ReadToEnd();
                            var error = process.StandardError.ReadToEnd();
                            process.WaitForExit();

                            Dispatcher.Invoke(() =>
                            {
                                if (!string.IsNullOrEmpty(output))
                                {
                                    foreach (var line in output.Split('\n'))
                                    {
                                        if (!string.IsNullOrWhiteSpace(line))
                                            LogMessage($"   {line.Trim()}");
                                    }
                                }
                                
                                if (!string.IsNullOrEmpty(error))
                                {
                                    foreach (var line in error.Split('\n'))
                                    {
                                        if (!string.IsNullOrWhiteSpace(line))
                                            LogMessage($"   {line.Trim()}");
                                    }
                                }
                            });

                            if (process.ExitCode == 0 && File.Exists(outputApk))
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    LogMessage($"✅ تم إعادة التجميع بنجاح!");
                                    LogMessage($"📦 الملف الجديد: {outputApk}");
                                    LogMessage($"📏 الحجم: {FormatFileSize(new FileInfo(outputApk).Length)}");
                                    LogMessage($"⚠️ ملاحظة: يجب توقيع APK قبل التثبيت");
                                    NotificationManager.Instance.Success("تم إعادة التجميع بنجاح", "APK Studio");
                                });
                            }
                            else
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    LogMessage($"❌ فشل إعادة التجميع (Exit Code: {process.ExitCode})");
                                    NotificationManager.Instance.Error("فشل إعادة التجميع", "APK Studio");
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        LogMessage($"❌ خطأ: {ex.Message}");
                        NotificationManager.Instance.Error($"خطأ: {ex.Message}", "APK Studio");
                    });
                }
            });

            HideProgress();
        }

        private async void ExtractSource_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedApkPath))
                return;

            ShowProgress("جاري استخراج السورس كود...", "استخدام JADX + UAST تحليل متقدم");

            await Task.Run(() =>
            {
                try
                {
                    var apkName = Path.GetFileNameWithoutExtension(_selectedApkPath);
                    var outputDir = Path.Combine(_outputPath, $"{apkName}_source");

                    Dispatcher.Invoke(() => LogMessage($"\n╔═══════════════════════════════════════════════════════╗"));
                    Dispatcher.Invoke(() => LogMessage($"║      📄 استخراج وتحليل السورس كود الكامل           ║"));
                    Dispatcher.Invoke(() => LogMessage($"╚═══════════════════════════════════════════════════════╝"));
                    Dispatcher.Invoke(() => LogMessage($"📂 المجلد الهدف: {outputDir}"));

                    if (Directory.Exists(outputDir))
                    {
                        Directory.Delete(outputDir, true);
                    }
                    Directory.CreateDirectory(outputDir);

                    // مسارات الأدوات المتاحة
                    var uastPath = Path.Combine(_toolsPath, "uast.jar");
                    var jadxLibJar = Path.Combine(_toolsPath, "jadx", "build", "jadx", "lib", "jadx-dev-all.jar");
                    var jadxDevLibJar = Path.Combine(_toolsPath, "jadx", "build", "jadx-dev", "lib", "jadx-dev-all.jar");
                    var jadxCliJar = Path.Combine(_toolsPath, "jadx", "jadx-cli", "build", "libs", "jadx-cli-dev-all.jar");
                    var jadxBat = Path.Combine(_toolsPath, "jadx", "build", "jadx", "bin", "jadx.bat");
                    var jadxDevBat = Path.Combine(_toolsPath, "jadx", "build", "jadx-dev", "bin", "jadx.bat");

                    // تحديد مسار jadx المتاح
                    string? jadxJarToUse = null;
                    if (File.Exists(jadxLibJar)) jadxJarToUse = jadxLibJar;
                    else if (File.Exists(jadxDevLibJar)) jadxJarToUse = jadxDevLibJar;
                    else if (File.Exists(jadxCliJar)) jadxJarToUse = jadxCliJar;

                    string? jadxBatToUse = null;
                    if (File.Exists(jadxBat)) jadxBatToUse = jadxBat;
                    else if (File.Exists(jadxDevBat)) jadxBatToUse = jadxDevBat;

                    bool jadxSuccess = false;

                    // ═══════════════════════════════════════════════
                    // الخطوة 1: استخراج السورس كود باستخدام JADX
                    // ═══════════════════════════════════════════════
                    if (jadxJarToUse != null || jadxBatToUse != null)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            LogMessage($"\n┌─────────────────────────────────────────────────────┐");
                            LogMessage($"│  الخطوة 1: استخراج Java Source باستخدام JADX        │");
                            LogMessage($"└─────────────────────────────────────────────────────┘");
                        });

                        var jadxOutput = Path.Combine(outputDir, "java_source");
                        Directory.CreateDirectory(jadxOutput);

                        ProcessStartInfo startInfo;

                        if (jadxJarToUse != null)
                        {
                            Dispatcher.Invoke(() => LogMessage($"   📦 الأداة: {Path.GetFileName(jadxJarToUse)}"));
                            startInfo = new ProcessStartInfo
                            {
                                FileName = _javaPath,
                                Arguments = $"-jar \"{jadxJarToUse}\" -d \"{jadxOutput}\" \"{_selectedApkPath}\" --show-bad-code --no-res --threads-count 4",
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                StandardOutputEncoding = Encoding.UTF8,
                                StandardErrorEncoding = Encoding.UTF8
                            };
                        }
                        else
                        {
                            Dispatcher.Invoke(() => LogMessage($"   📦 الأداة: jadx.bat"));
                            startInfo = new ProcessStartInfo
                            {
                                FileName = jadxBatToUse!,
                                Arguments = $"-d \"{jadxOutput}\" \"{_selectedApkPath}\" --show-bad-code --no-res",
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                StandardOutputEncoding = Encoding.UTF8,
                                StandardErrorEncoding = Encoding.UTF8
                            };
                        }

                        using (var process = Process.Start(startInfo))
                        {
                            if (process != null)
                            {
                                process.OutputDataReceived += (s, args) =>
                                {
                                    if (!string.IsNullOrEmpty(args.Data))
                                        Dispatcher.Invoke(() => LogMessage($"   {args.Data}"));
                                };
                                process.ErrorDataReceived += (s, args) =>
                                {
                                    if (!string.IsNullOrEmpty(args.Data))
                                        Dispatcher.Invoke(() => LogMessage($"   {args.Data}"));
                                };
                                process.BeginOutputReadLine();
                                process.BeginErrorReadLine();
                                process.WaitForExit();

                                var javaFiles = Directory.GetFiles(jadxOutput, "*.java", SearchOption.AllDirectories);
                                jadxSuccess = javaFiles.Length > 0;

                                if (jadxSuccess)
                                {
                                    Dispatcher.Invoke(() =>
                                    {
                                        LogMessage($"   ✅ تم استخراج {javaFiles.Length} ملف Java بنجاح!");
                                        var totalSize = javaFiles.Sum(f => new FileInfo(f).Length);
                                        LogMessage($"   📏 الحجم الإجمالي: {FormatFileSize(totalSize)}");
                                        var packages = javaFiles
                                            .Select(f => Path.GetDirectoryName(f)?.Replace(jadxOutput, "").TrimStart('\\', '/').Split('\\', '/').FirstOrDefault() ?? "")
                                            .Where(p => !string.IsNullOrEmpty(p))
                                            .Distinct()
                                            .Count();
                                        LogMessage($"   📦 عدد الحزم: {packages}");
                                    });
                                }
                                else
                                {
                                    Dispatcher.Invoke(() => LogMessage($"   ⚠️ لم يتم استخراج ملفات Java (Exit Code: {process.ExitCode})"));
                                }
                            }
                        }
                    }
                    else
                    {
                        Dispatcher.Invoke(() =>
                        {
                            LogMessage($"   ⚠️ JADX غير موجود في المسارات المعروفة");
                            LogMessage($"   💡 تم البحث في:");
                            LogMessage($"      • {jadxLibJar}");
                            LogMessage($"      • {jadxDevLibJar}");
                            LogMessage($"      • {jadxCliJar}");
                        });
                    }

                    // ═══════════════════════════════════════════════
                    // الخطوة 2: استخراج APK metadata باستخدام UAST
                    // ═══════════════════════════════════════════════
                    Dispatcher.Invoke(() =>
                    {
                        LogMessage($"\n┌─────────────────────────────────────────────────────┐");
                        LogMessage($"│  الخطوة 2: استخراج APK Metadata باستخدام UAST       │");
                        LogMessage($"└─────────────────────────────────────────────────────┘");
                    });

                    if (File.Exists(uastPath))
                    {
                        RunUastAnalysis(uastPath, _selectedApkPath!, outputDir);
                    }
                    else
                    {
                        Dispatcher.Invoke(() => LogMessage($"   ⚠️ uast.jar غير موجود في: {uastPath}"));
                    }

                    // ═══════════════════════════════════════════════
                    // الخطوة 3: تحليل السورس كود المستخرج
                    // ═══════════════════════════════════════════════
                    var javaSourceDir = Path.Combine(outputDir, "java_source");
                    if (jadxSuccess && Directory.Exists(javaSourceDir))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            LogMessage($"\n┌─────────────────────────────────────────────────────┐");
                            LogMessage($"│  الخطوة 3: تحليل بيانات السورس كود المستخرج        │");
                            LogMessage($"└─────────────────────────────────────────────────────┘");
                        });
                        AnalyzeExtractedJavaSource(javaSourceDir);
                    }

                    // ملخص نهائي
                    Dispatcher.Invoke(() =>
                    {
                        LogMessage($"\n╔═══════════════════════════════════════════════════════╗");
                        LogMessage($"║       ✅ اكتمل استخراج وتحليل السورس كود ✅          ║");
                        LogMessage($"╚═══════════════════════════════════════════════════════╝");
                        LogMessage($"   📂 مجلد المخرجات: {outputDir}");
                        try
                        {
                            var totalFiles = Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories).Length;
                            LogMessage($"   📊 إجمالي الملفات: {totalFiles}");
                        }
                        catch { }
                        NotificationManager.Instance.Success("اكتمل استخراج السورس كود", "APK Studio");
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        LogMessage($"❌ خطأ: {ex.Message}");
                        LogMessage($"   Stack: {ex.StackTrace}");
                        NotificationManager.Instance.Error($"خطأ: {ex.Message}", "APK Studio");
                    });
                }
            });

            HideProgress();
        }

        // ═══════════════════════════════════════════════════════
        // UAST Analysis - استخراج APK metadata
        // ═══════════════════════════════════════════════════════
        private void RunUastAnalysis(string uastPath, string apkPath, string outputDir)
        {
            try
            {
                var uastOutputFile = Path.Combine(outputDir, "uast_metadata.txt");

                // محاولة أوضاع مختلفة لـ uast.jar
                var argsList = new[]
                {
                    $"-jar \"{uastPath}\" \"{apkPath}\"",
                    $"-jar \"{uastPath}\" info \"{apkPath}\"",
                    $"-jar \"{uastPath}\" --apk \"{apkPath}\" --info",
                    $"-jar \"{uastPath}\" parse \"{apkPath}\"",
                    $"-jar \"{uastPath}\" analyze \"{apkPath}\"",
                    $"-jar \"{uastPath}\" --help",
                };

                string? bestOutput = null;

                foreach (var args in argsList)
                {
                    try
                    {
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = _javaPath,
                            Arguments = args,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            StandardOutputEncoding = Encoding.UTF8,
                            StandardErrorEncoding = Encoding.UTF8
                        };

                        using (var process = Process.Start(startInfo))
                        {
                            if (process != null)
                            {
                                var outTask = process.StandardOutput.ReadToEndAsync();
                                var errTask = process.StandardError.ReadToEndAsync();
                                process.WaitForExit(30000);

                                var combined = outTask.Result + errTask.Result;

                                if (!string.IsNullOrWhiteSpace(combined) && combined.Length > 100)
                                {
                                    if (bestOutput == null || combined.Length > bestOutput.Length)
                                    {
                                        bestOutput = combined;
                                    }
                                }

                                if (args.Contains("--help") && !string.IsNullOrWhiteSpace(combined))
                                {
                                    Dispatcher.Invoke(() =>
                                    {
                                        LogMessage($"   📋 UAST مساعدة:");
                                        foreach (var line in combined.Split('\n').Take(15))
                                        {
                                            if (!string.IsNullOrWhiteSpace(line))
                                                LogMessage($"      {line.Trim()}");
                                        }
                                    });
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (!string.IsNullOrEmpty(bestOutput))
                {
                    File.WriteAllText(uastOutputFile, bestOutput, Encoding.UTF8);

                    Dispatcher.Invoke(() =>
                    {
                        LogMessage($"   ✅ UAST: استخرج {bestOutput.Length} حرف من metadata");
                        var lines = bestOutput.Split('\n');
                        LogMessage($"   📊 عدد الأسطر: {lines.Length}");

                        // عرض أول 25 سطر مهمة
                        var meaningfulLines = lines
                            .Where(l => !string.IsNullOrWhiteSpace(l) && l.Length > 5)
                            .Take(25);

                        foreach (var line in meaningfulLines)
                        {
                            LogMessage($"      {line.Trim()}");
                        }

                        LogMessage($"   📄 التقرير الكامل محفوظ في: uast_metadata.txt");
                    });
                }
                else
                {
                    Dispatcher.Invoke(() => LogMessage($"   ⚠️ UAST: لم ينتج مخرجات - الأداة قد تتطلب صيغة خاصة"));
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => LogMessage($"   ❌ خطأ UAST: {ex.Message}"));
            }
        }

        // ═══════════════════════════════════════════════════════
        // تحليل السورس كود Java المستخرج - كشف البيانات الحساسة
        // ═══════════════════════════════════════════════════════
        private void AnalyzeExtractedJavaSource(string sourceDir)
        {
            try
            {
                var javaFiles = Directory.GetFiles(sourceDir, "*.java", SearchOption.AllDirectories);

                if (javaFiles.Length == 0)
                {
                    Dispatcher.Invoke(() => LogMessage($"   ⚠️ لا توجد ملفات Java للتحليل"));
                    return;
                }

                Dispatcher.Invoke(() => LogMessage($"   🔍 تحليل {javaFiles.Length} ملف Java للكشف عن البيانات الحساسة..."));

                var criticalFindings = new List<(string type, string value, string file)>();
                var sensitiveFindings = new List<(string type, string value, string file)>();

                var javaPatterns = new Dictionary<string, (string description, bool isCritical)>
                {
                    // Google
                    { @"AIza[0-9A-Za-z\-_]{35}", ("Google/Firebase API Key", true) },
                    { @"ya29\.[0-9A-Za-z\-_]{50,}", ("Google OAuth Access Token", true) },
                    { @"[0-9]+-[0-9A-Za-z_]{32}\.apps\.googleusercontent\.com", ("Google OAuth Client ID", false) },
                    { @"""type""\s*:\s*""service_account""", ("Google Service Account JSON", true) },
                    { @"""private_key""\s*:\s*""-----BEGIN", ("Google Service Account Private Key", true) },

                    // AWS
                    { @"AKIA[0-9A-Z]{16}", ("AWS Access Key ID", true) },
                    { @"(?i)aws[_\-]?secret[_\-]?access[_\-]?key\s*[=:]\s*[A-Za-z0-9/+=]{40}", ("AWS Secret Access Key", true) },

                    // Stripe
                    { @"sk_live_[0-9a-zA-Z]{24,}", ("Stripe Live Secret Key", true) },
                    { @"sk_test_[0-9a-zA-Z]{24,}", ("Stripe Test Secret Key", false) },
                    { @"pk_live_[0-9a-zA-Z]{24,}", ("Stripe Live Public Key", false) },

                    // GitHub
                    { @"ghp_[0-9a-zA-Z]{36}", ("GitHub Personal Access Token", true) },
                    { @"gho_[0-9a-zA-Z]{36}", ("GitHub OAuth Token", true) },
                    { @"github_pat_[0-9a-zA-Z_]{80,}", ("GitHub Fine-Grained Token", true) },

                    // Slack
                    { @"xox[baprs]-[0-9]{12}-[0-9a-zA-Z]{12,}", ("Slack API Token", true) },
                    { @"https://hooks\.slack\.com/services/[A-Z0-9]+/[A-Z0-9]+/[a-zA-Z0-9]+", ("Slack Webhook URL", true) },

                    // SendGrid
                    { @"SG\.[a-zA-Z0-9_\-]{22}\.[a-zA-Z0-9_\-]{43}", ("SendGrid API Key", true) },

                    // Mailgun
                    { @"key-[0-9a-zA-Z]{32}", ("Mailgun API Key", true) },

                    // Twilio
                    { @"AC[a-zA-Z0-9]{32}", ("Twilio Account SID", true) },
                    { @"SK[a-zA-Z0-9]{32}", ("Twilio API Key SID", true) },

                    // Square
                    { @"sq0atp-[0-9A-Za-z\-_]{22}", ("Square Access Token", true) },
                    { @"sq0csp-[0-9A-Za-z\-_]{43}", ("Square OAuth Secret", true) },

                    // Telegram
                    { @"\d{8,10}:[a-zA-Z0-9_\-]{35}", ("Telegram Bot Token", true) },

                    // Discord
                    { @"discord(app)?\.com/api/webhooks/\d+/[a-zA-Z0-9_\-]+", ("Discord Webhook URL", true) },
                    { @"[MN][a-zA-Z0-9]{23}\.[a-zA-Z0-9\-_]{6}\.[a-zA-Z0-9\-_]{27}", ("Discord Bot Token", true) },

                    // Twitter/X
                    { @"(?i)consumer[_\-]?key\s*[=:]\s*[""'][a-zA-Z0-9]{25,}[""']", ("Twitter Consumer Key", true) },
                    { @"(?i)bearer[_\-]?token\s*[=:]\s*[""']AAAA[a-zA-Z0-9%]+[""']", ("Twitter Bearer Token", true) },

                    // JWT
                    { @"eyJ[A-Za-z0-9\-_]{20,}\.[A-Za-z0-9\-_]{20,}\.[A-Za-z0-9\-_\+/=]{20,}", ("JWT Token", true) },

                    // Database
                    { @"jdbc:(mysql|postgresql|oracle|sqlserver|sqlite|mariadb)://[^\s""'<>]+", ("Database JDBC URL", true) },
                    { @"mongodb(\+srv)?://[^\s""'<>]+", ("MongoDB Connection String", true) },
                    { @"redis://[^@\s]+@[^\s""'<>]+", ("Redis URL with credentials", true) },

                    // Azure
                    { @"DefaultEndpointsProtocol=https;AccountName=[^;]+;AccountKey=[^;]+", ("Azure Storage Connection String", true) },
                    { @"https://[a-zA-Z0-9\-]+\.vault\.azure\.net", ("Azure Key Vault URL", false) },

                    // Firebase
                    { @"https://[a-zA-Z0-9\-]+\.firebaseio\.com", ("Firebase Realtime DB URL", false) },
                    { @"""databaseURL""\s*:\s*""https://[^""]+\.firebaseio\.com""", ("Firebase Database URL in config", false) },

                    // Private Keys
                    { @"-----BEGIN (RSA |EC |OPENSSH |PGP )?PRIVATE KEY-----", ("Private Key (PEM)", true) },

                    // Sentry
                    { @"https://[a-f0-9]{32}@[a-z0-9\.]+\.ingest\.sentry\.io/\d+", ("Sentry DSN", false) },

                    // Generic secrets (Java string assignments)
                    { @"(?i)(password|passwd|pwd)\s*=\s*""[^""]{6,}""", ("Hardcoded Password", true) },
                    { @"(?i)(secret|api_?key|auth_?token|access_?token|private_?key)\s*=\s*""[^""]{8,}""", ("Hardcoded Secret", true) },
                    { @"(?i)(private final|private static final)\s+String\s+\w*(key|secret|password|token|pwd)\w*\s*=\s*""[^""]{6,}""", ("Hardcoded Credential Field", true) },

                    // URLs with credentials
                    { @"https?://[a-zA-Z0-9_\-]+:[a-zA-Z0-9_\-@!#$%&*]{4,}@[a-zA-Z0-9\-\.]+", ("URL with Embedded Credentials", true) },

                    // Crypto
                    { @"0x[a-fA-F0-9]{40}\b", ("Ethereum Wallet Address", false) },

                    // Email
                    { @"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}", ("Email Address", false) },
                };

                int filesScanned = 0;

                foreach (var file in javaFiles)
                {
                    filesScanned++;
                    try
                    {
                        var content = File.ReadAllText(file, Encoding.UTF8);
                        var shortPath = file.Replace(sourceDir, "").TrimStart('\\', '/');

                        foreach (var pattern in javaPatterns)
                        {
                            var matches = System.Text.RegularExpressions.Regex.Matches(
                                content, pattern.Key,
                                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                            if (matches.Count > 0)
                            {
                                foreach (System.Text.RegularExpressions.Match match in matches.Cast<System.Text.RegularExpressions.Match>().Take(2))
                                {
                                    var value = match.Value;
                                    var displayValue = value.Length > 40
                                        ? value.Substring(0, 20) + "..." + value.Substring(value.Length - 8)
                                        : value;

                                    if (pattern.Value.isCritical)
                                        criticalFindings.Add((pattern.Value.description, displayValue, shortPath));
                                    else
                                        sensitiveFindings.Add((pattern.Value.description, displayValue, shortPath));
                                }
                            }
                        }
                    }
                    catch { }
                }

                Dispatcher.Invoke(() =>
                {
                    LogMessage($"   📊 تم تحليل {filesScanned} ملف Java");

                    if (criticalFindings.Count > 0)
                    {
                        LogMessage($"\n   🚨 بيانات حرجة في السورس كود ({criticalFindings.Count}):");
                        foreach (var (type, value, file) in criticalFindings.Take(30))
                        {
                            LogMessage($"      ❌ [{type}]");
                            LogMessage($"         📍 الملف: {file}");
                            LogMessage($"         🔑 القيمة: {value}");
                        }
                        if (criticalFindings.Count > 30)
                            LogMessage($"      ... و {criticalFindings.Count - 30} اكتشاف آخر");
                    }

                    if (sensitiveFindings.Count > 0)
                    {
                        LogMessage($"\n   ⚠️ بيانات حساسة في السورس كود ({sensitiveFindings.Count}):");
                        foreach (var (type, value, file) in sensitiveFindings.Take(20))
                        {
                            LogMessage($"      • [{type}]");
                            LogMessage($"        📍 الملف: {file}");
                            LogMessage($"        🔍 القيمة: {value}");
                        }
                        if (sensitiveFindings.Count > 20)
                            LogMessage($"      ... و {sensitiveFindings.Count - 20} اكتشاف آخر");
                    }

                    if (criticalFindings.Count == 0 && sensitiveFindings.Count == 0)
                    {
                        LogMessage($"   ✅ لم يتم اكتشاف بيانات حساسة واضحة في السورس كود");
                    }
                    else
                    {
                        LogMessage($"\n   📊 إجمالي اكتشافات السورس كود: {criticalFindings.Count + sensitiveFindings.Count}");
                        if (criticalFindings.Count > 0)
                            LogMessage($"   🚨 تحذير: بيانات حرجة مكتشفة! يجب مراجعتها فوراً");
                    }
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => LogMessage($"   ❌ خطأ في تحليل السورس كود: {ex.Message}"));
            }
        }

        private async void ShowAppInfo_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedApkPath))
                return;

            ShowProgress("جاري التحليل الشامل للتطبيق...", "تحليل متقدم لجميع المكونات");

            await Task.Run(() =>
            {
                try
                {
                    Dispatcher.Invoke(() => LogMessage($"\n╔═══════════════════════════════════════════════════════╗"));
                    Dispatcher.Invoke(() => LogMessage($"║     🔍 التحليل الشامل والمتقدم للتطبيق 🔍          ║"));
                    Dispatcher.Invoke(() => LogMessage($"╚═══════════════════════════════════════════════════════╝\n"));

                    var apktoolJar = Path.Combine(_toolsPath, "apktool.jar");
                    var tempDir = Path.Combine(_outputPath, "temp_analysis");

                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }

                    Dispatcher.Invoke(() => LogMessage($"⚙️ فك تجميع APK للتحليل..."));

                    // Decompile APK
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = _javaPath,
                        Arguments = $"-jar \"{apktoolJar}\" d \"{_selectedApkPath}\" -o \"{tempDir}\" -f",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using (var process = Process.Start(startInfo))
                    {
                        process?.WaitForExit();
                    }

                    if (!Directory.Exists(tempDir))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            LogMessage($"❌ فشل فك التجميع");
                            NotificationManager.Instance.Error("فشل التحليل", "APK Studio");
                        });
                        return;
                    }

                    // ═══════════════════════════════════════════════════════
                    // 1. معلومات أساسية من apktool.yml
                    // ═══════════════════════════════════════════════════════
                    var apktoolYml = Path.Combine(tempDir, "apktool.yml");
                    if (File.Exists(apktoolYml))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            LogMessage($"\n┌─────────────────────────────────────────────────────┐");
                            LogMessage($"│  📋 معلومات البناء الأساسية                        │");
                            LogMessage($"└─────────────────────────────────────────────────────┘");
                        });
                        ParseApktoolYml(File.ReadAllText(apktoolYml));
                    }

                    // ═══════════════════════════════════════════════════════
                    // 2. تحليل AndroidManifest.xml
                    // ═══════════════════════════════════════════════════════
                    var manifestPath = Path.Combine(tempDir, "AndroidManifest.xml");
                    if (File.Exists(manifestPath))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            LogMessage($"\n┌─────────────────────────────────────────────────────┐");
                            LogMessage($"│  📱 تحليل AndroidManifest.xml                       │");
                            LogMessage($"└─────────────────────────────────────────────────────┘");
                        });
                        AnalyzeManifest(File.ReadAllText(manifestPath));
                    }

                    // ═══════════════════════════════════════════════════════
                    // 3. تحليل Activities والمسارات
                    // ═══════════════════════════════════════════════════════
                    Dispatcher.Invoke(() =>
                    {
                        LogMessage($"\n┌─────────────────────────────────────────────────────┐");
                        LogMessage($"│  🎬 تحليل Activities ومساراتها                     │");
                        LogMessage($"└─────────────────────────────────────────────────────┘");
                    });
                    AnalyzeActivities(tempDir, manifestPath);

                    // ═══════════════════════════════════════════════════════
                    // 4. تحليل ملفات Smali
                    // ═══════════════════════════════════════════════════════
                    Dispatcher.Invoke(() =>
                    {
                        LogMessage($"\n┌─────────────────────────────────────────────────────┐");
                        LogMessage($"│  📝 تحليل ملفات Smali (Bytecode)                   │");
                        LogMessage($"└─────────────────────────────────────────────────────┘");
                    });
                    AnalyzeSmaliFiles(tempDir);

                    // ═══════════════════════════════════════════════════════
                    // 5. تحليل Native Libraries (.so)
                    // ═══════════════════════════════════════════════════════
                    Dispatcher.Invoke(() =>
                    {
                        LogMessage($"\n┌─────────────────────────────────────────────────────┐");
                        LogMessage($"│  🔧 تحليل Native Libraries (.so)                   │");
                        LogMessage($"└─────────────────────────────────────────────────────┘");
                    });
                    AnalyzeNativeLibraries(tempDir);

                    // ═══════════════════════════════════════════════════════
                    // 6. تحليل الموارد (Resources)
                    // ═══════════════════════════════════════════════════════
                    Dispatcher.Invoke(() =>
                    {
                        LogMessage($"\n┌─────────────────────────────────────────────────────┐");
                        LogMessage($"│  🎨 تحليل الموارد (Resources)                      │");
                        LogMessage($"└─────────────────────────────────────────────────────┘");
                    });
                    AnalyzeResources(tempDir);

                    // ═══════════════════════════════════════════════════════
                    // 7. تحليل Assets
                    // ═══════════════════════════════════════════════════════
                    Dispatcher.Invoke(() =>
                    {
                        LogMessage($"\n┌─────────────────────────────────────────────────────┐");
                        LogMessage($"│  📦 تحليل Assets                                    │");
                        LogMessage($"└─────────────────────────────────────────────────────┘");
                    });
                    AnalyzeAssets(tempDir);

                    // ═══════════════════════════════════════════════════════
                    // 8. تحليل الأمان والصلاحيات
                    // ═══════════════════════════════════════════════════════
                    Dispatcher.Invoke(() =>
                    {
                        LogMessage($"\n┌─────────────────────────────────────────────────────┐");
                        LogMessage($"│  🔐 تحليل الأمان والصلاحيات                        │");
                        LogMessage($"└─────────────────────────────────────────────────────┘");
                    });
                    AnalyzeSecurity(manifestPath);

                    // ═══════════════════════════════════════════════════════
                    // 9. كشف البيانات الحساسة (NEW - ADVANCED)
                    // ═══════════════════════════════════════════════════════
                    Dispatcher.Invoke(() =>
                    {
                        LogMessage($"\n┌─────────────────────────────────────────────────────┐");
                        LogMessage($"│  � كشف البيانات الحساسة والأسرار                  │");
                        LogMessage($"└─────────────────────────────────────────────────────┘");
                    });
                    DetectSensitiveData(tempDir);

                    // ═══════════════════════════════════════════════════════
                    // 10. تحليل الشبكات والاتصالات (NEW - ADVANCED)
                    // ═══════════════════════════════════════════════════════
                    Dispatcher.Invoke(() =>
                    {
                        LogMessage($"\n┌─────────────────────────────────────────────────────┐");
                        LogMessage($"│  🌐 تحليل الشبكات والاتصالات                       │");
                        LogMessage($"└─────────────────────────────────────────────────────┘");
                    });
                    AnalyzeNetworkConnections(tempDir);

                    // ═══════════════════════════════════════════════════════
                    // 11. كشف السلوكيات المشبوهة (NEW - ADVANCED)
                    // ═══════════════════════════════════════════════════════
                    Dispatcher.Invoke(() =>
                    {
                        LogMessage($"\n┌─────────────────────────────────────────────────────┐");
                        LogMessage($"│  ⚠️ كشف السلوكيات المشبوهة والبرمجيات الخبيثة      │");
                        LogMessage($"└─────────────────────────────────────────────────────┘");
                    });
                    DetectMaliciousBehavior(tempDir);

                    // ═══════════════════════════════════════════════════════
                    // 12. تحليل التشفير والحماية (NEW - ADVANCED)
                    // ═══════════════════════════════════════════════════════
                    Dispatcher.Invoke(() =>
                    {
                        LogMessage($"\n┌─────────────────────────────────────────────────────┐");
                        LogMessage($"│  🔐 تحليل التشفير وآليات الحماية                   │");
                        LogMessage($"└─────────────────────────────────────────────────────┘");
                    });
                    AnalyzeCryptography(tempDir);

                    // ═══════════════════════════════════════════════════════
                    // 13. تحليل متعمق للمكتبات النيتيف (Native .so) - JADX Strings
                    // ═══════════════════════════════════════════════════════
                    Dispatcher.Invoke(() =>
                    {
                        LogMessage($"\n┌─────────────────────────────────────────────────────┐");
                        LogMessage($"│  🔬 تحليل متعمق للمكتبات النيتيف (.so)             │");
                        LogMessage($"└─────────────────────────────────────────────────────┘");
                    });
                    DeepAnalyzeNativeLibs(tempDir);

                    // ═══════════════════════════════════════════════════════
                    // 14. معلومات الملف
                    // ═══════════════════════════════════════════════════════
                    var fileInfo = new FileInfo(_selectedApkPath);
                    Dispatcher.Invoke(() =>
                    {
                        LogMessage($"\n┌─────────────────────────────────────────────────────┐");
                        LogMessage($"│  📦 معلومات ملف APK                                │");
                        LogMessage($"└─────────────────────────────────────────────────────┘");
                        LogMessage($"   📏 الحجم: {FormatFileSize(fileInfo.Length)}");
                        LogMessage($"   📅 تاريخ الإنشاء: {fileInfo.CreationTime:yyyy-MM-dd HH:mm:ss}");
                        LogMessage($"   📝 تاريخ التعديل: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
                        LogMessage($"   📂 المسار: {fileInfo.DirectoryName}");
                        LogMessage($"   🔖 الاسم: {fileInfo.Name}");
                    });

                    // Cleanup
                    try
                    {
                        if (Directory.Exists(tempDir))
                        {
                            Directory.Delete(tempDir, true);
                        }
                    }
                    catch { }

                    Dispatcher.Invoke(() =>
                    {
                        LogMessage($"\n╔═══════════════════════════════════════════════════════╗");
                        LogMessage($"║          ✅ اكتمل التحليل الشامل بنجاح ✅            ║");
                        LogMessage($"╚═══════════════════════════════════════════════════════╝\n");
                        NotificationManager.Instance.Success("اكتمل التحليل الشامل بنجاح", "APK Studio");
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        LogMessage($"❌ خطأ: {ex.Message}");
                        LogMessage($"   Stack: {ex.StackTrace}");
                        NotificationManager.Instance.Error($"خطأ: {ex.Message}", "APK Studio");
                    });
                }
            });

            HideProgress();
        }

        private void ParseApktoolYml(string content)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    LogMessage($"🔧 معلومات البناء:");
                    
                    foreach (var line in content.Split('\n'))
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("versionCode:"))
                        {
                            LogMessage($"   📊 رمز الإصدار: {trimmed.Replace("versionCode:", "").Trim().Trim('\'')}");
                        }
                        else if (trimmed.StartsWith("versionName:"))
                        {
                            LogMessage($"   🏷️ اسم الإصدار: {trimmed.Replace("versionName:", "").Trim().Trim('\'')}");
                        }
                        else if (trimmed.StartsWith("minSdkVersion:"))
                        {
                            LogMessage($"   📱 الحد الأدنى SDK: {trimmed.Replace("minSdkVersion:", "").Trim().Trim('\'')}");
                        }
                        else if (trimmed.StartsWith("targetSdkVersion:"))
                        {
                            LogMessage($"   🎯 الهدف SDK: {trimmed.Replace("targetSdkVersion:", "").Trim().Trim('\'')}");
                        }
                    }
                });
            }
            catch { }
        }

        private void ParseAndroidManifest(string content)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    LogMessage($"\n📋 معلومات Manifest:");
                    
                    // Extract package name
                    var packageMatch = System.Text.RegularExpressions.Regex.Match(content, @"package=""([^""]+)""");
                    if (packageMatch.Success)
                    {
                        LogMessage($"   📦 اسم الحزمة: {packageMatch.Groups[1].Value}");
                    }

                    // Count activities
                    var activityCount = System.Text.RegularExpressions.Regex.Matches(content, @"<activity").Count;
                    LogMessage($"   🎬 عدد Activities: {activityCount}");

                    // Count services
                    var serviceCount = System.Text.RegularExpressions.Regex.Matches(content, @"<service").Count;
                    LogMessage($"   ⚙️ عدد Services: {serviceCount}");

                    // Count receivers
                    var receiverCount = System.Text.RegularExpressions.Regex.Matches(content, @"<receiver").Count;
                    LogMessage($"   📡 عدد Receivers: {receiverCount}");

                    // Count providers
                    var providerCount = System.Text.RegularExpressions.Regex.Matches(content, @"<provider").Count;
                    LogMessage($"   🗄️ عدد Providers: {providerCount}");

                    // Permissions
                    var permissions = System.Text.RegularExpressions.Regex.Matches(content, @"<uses-permission[^>]+android:name=""([^""]+)""");
                    if (permissions.Count > 0)
                    {
                        LogMessage($"\n🔐 الصلاحيات المطلوبة ({permissions.Count}):");
                        foreach (System.Text.RegularExpressions.Match perm in permissions)
                        {
                            var permName = perm.Groups[1].Value.Replace("android.permission.", "");
                            LogMessage($"   • {permName}");
                        }
                    }
                });
            }
            catch { }
        }

        private void AnalyzeManifest(string manifestContent)
        {
            try
            {
                // Package info
                var packageMatch = System.Text.RegularExpressions.Regex.Match(manifestContent, @"package=""([^""]+)""");
                if (packageMatch.Success)
                {
                    Dispatcher.Invoke(() => LogMessage($"   📦 Package: {packageMatch.Groups[1].Value}"));
                }

                // Components count
                var activities = System.Text.RegularExpressions.Regex.Matches(manifestContent, @"<activity[^>]*android:name=""([^""]+)""");
                var services = System.Text.RegularExpressions.Regex.Matches(manifestContent, @"<service[^>]*android:name=""([^""]+)""");
                var receivers = System.Text.RegularExpressions.Regex.Matches(manifestContent, @"<receiver[^>]*android:name=""([^""]+)""");
                var providers = System.Text.RegularExpressions.Regex.Matches(manifestContent, @"<provider[^>]*android:name=""([^""]+)""");

                Dispatcher.Invoke(() =>
                {
                    LogMessage($"   🎬 Activities: {activities.Count}");
                    LogMessage($"   ⚙️ Services: {services.Count}");
                    LogMessage($"   📡 Broadcast Receivers: {receivers.Count}");
                    LogMessage($"   🗄️ Content Providers: {providers.Count}");
                });

                // Main Activity
                var mainActivity = System.Text.RegularExpressions.Regex.Match(manifestContent, 
                    @"<activity[^>]*android:name=""([^""]+)""[^>]*>[\s\S]*?<action\s+android:name=""android\.intent\.action\.MAIN""");
                if (mainActivity.Success)
                {
                    Dispatcher.Invoke(() => LogMessage($"   🚀 Main Activity: {mainActivity.Groups[1].Value}"));
                }

                // Exported components (security risk)
                var exportedActivities = System.Text.RegularExpressions.Regex.Matches(manifestContent, 
                    @"<activity[^>]*android:exported=""true""");
                if (exportedActivities.Count > 0)
                {
                    Dispatcher.Invoke(() => LogMessage($"   ⚠️ Exported Activities: {exportedActivities.Count} (قد تكون ثغرة أمنية)"));
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => LogMessage($"   ⚠️ خطأ في تحليل Manifest: {ex.Message}"));
            }
        }

        private void AnalyzeActivities(string tempDir, string manifestPath)
        {
            try
            {
                if (!File.Exists(manifestPath))
                    return;

                var manifestContent = File.ReadAllText(manifestPath);
                var activities = System.Text.RegularExpressions.Regex.Matches(manifestContent, 
                    @"<activity[^>]*android:name=""([^""]+)""");

                Dispatcher.Invoke(() => LogMessage($"   📊 إجمالي Activities: {activities.Count}\n"));

                int count = 0;
                foreach (System.Text.RegularExpressions.Match activity in activities)
                {
                    if (count >= 10) // Show first 10
                    {
                        Dispatcher.Invoke(() => LogMessage($"   ... و {activities.Count - 10} أخرى"));
                        break;
                    }

                    var activityName = activity.Groups[1].Value;
                    var className = activityName.Contains(".") ? activityName.Split('.').Last() : activityName;
                    
                    // Find corresponding smali file
                    var smaliPath = FindSmaliFile(tempDir, activityName);
                    
                    Dispatcher.Invoke(() =>
                    {
                        LogMessage($"   {count + 1}. {activityName}");
                        if (!string.IsNullOrEmpty(smaliPath))
                        {
                            LogMessage($"      📂 Smali: {smaliPath.Replace(tempDir, ".")}");
                            
                            // Analyze smali file
                            if (File.Exists(smaliPath))
                            {
                                var smaliContent = File.ReadAllText(smaliPath);
                                var methodCount = System.Text.RegularExpressions.Regex.Matches(smaliContent, @"\.method").Count;
                                var fieldCount = System.Text.RegularExpressions.Regex.Matches(smaliContent, @"\.field").Count;
                                LogMessage($"      🔧 Methods: {methodCount}, Fields: {fieldCount}");
                            }
                        }
                        else
                        {
                            LogMessage($"      ⚠️ ملف Smali غير موجود");
                        }
                    });
                    
                    count++;
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => LogMessage($"   ❌ خطأ: {ex.Message}"));
            }
        }

        private string FindSmaliFile(string tempDir, string className)
        {
            try
            {
                // Convert class name to file path
                var filePath = className.Replace('.', Path.DirectorySeparatorChar) + ".smali";
                
                // Search in smali directories
                var smaliDirs = Directory.GetDirectories(tempDir, "smali*", SearchOption.TopDirectoryOnly);
                
                foreach (var smaliDir in smaliDirs)
                {
                    var fullPath = Path.Combine(smaliDir, filePath);
                    if (File.Exists(fullPath))
                        return fullPath;
                }
            }
            catch { }
            
            return string.Empty;
        }

        private void AnalyzeSmaliFiles(string tempDir)
        {
            try
            {
                var smaliDirs = Directory.GetDirectories(tempDir, "smali*", SearchOption.TopDirectoryOnly);
                
                if (smaliDirs.Length == 0)
                {
                    Dispatcher.Invoke(() => LogMessage($"   ⚠️ لا توجد ملفات Smali"));
                    return;
                }

                long totalSize = 0;
                int totalFiles = 0;
                int totalMethods = 0;
                int totalFields = 0;

                foreach (var smaliDir in smaliDirs)
                {
                    var smaliFiles = Directory.GetFiles(smaliDir, "*.smali", SearchOption.AllDirectories);
                    totalFiles += smaliFiles.Length;

                    foreach (var file in smaliFiles.Take(100)) // Analyze first 100 files
                    {
                        totalSize += new FileInfo(file).Length;
                        var content = File.ReadAllText(file);
                        totalMethods += System.Text.RegularExpressions.Regex.Matches(content, @"\.method").Count;
                        totalFields += System.Text.RegularExpressions.Regex.Matches(content, @"\.field").Count;
                    }
                }

                Dispatcher.Invoke(() =>
                {
                    LogMessage($"   📊 إجمالي ملفات Smali: {totalFiles}");
                    LogMessage($"   📏 الحجم الإجمالي: {FormatFileSize(totalSize)}");
                    LogMessage($"   🔧 إجمالي Methods: {totalMethods}+");
                    LogMessage($"   📝 إجمالي Fields: {totalFields}+");
                    LogMessage($"   📂 مجلدات Smali: {smaliDirs.Length}");
                    
                    foreach (var dir in smaliDirs)
                    {
                        var dirName = Path.GetFileName(dir);
                        var fileCount = Directory.GetFiles(dir, "*.smali", SearchOption.AllDirectories).Length;
                        LogMessage($"      • {dirName}: {fileCount} ملف");
                    }
                });

                // Detect obfuscation
                DetectObfuscation(smaliDirs);
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => LogMessage($"   ❌ خطأ: {ex.Message}"));
            }
        }

        private void DetectObfuscation(string[] smaliDirs)
        {
            try
            {
                int shortNameCount = 0;
                int totalClasses = 0;

                foreach (var smaliDir in smaliDirs)
                {
                    var smaliFiles = Directory.GetFiles(smaliDir, "*.smali", SearchOption.AllDirectories).Take(200);
                    
                    foreach (var file in smaliFiles)
                    {
                        totalClasses++;
                        var className = Path.GetFileNameWithoutExtension(file);
                        
                        // Check for obfuscated names (single letter or very short)
                        if (className.Length <= 2 || System.Text.RegularExpressions.Regex.IsMatch(className, @"^[a-z]$"))
                        {
                            shortNameCount++;
                        }
                    }
                }

                if (totalClasses > 0)
                {
                    var obfuscationRate = (shortNameCount * 100.0) / totalClasses;
                    
                    Dispatcher.Invoke(() =>
                    {
                        LogMessage($"\n   🔍 كشف التشويش (Obfuscation):");
                        LogMessage($"      • نسبة الأسماء المشوشة: {obfuscationRate:F1}%");
                        
                        if (obfuscationRate > 50)
                        {
                            LogMessage($"      ⚠️ التطبيق مشوش بشكل كبير (ProGuard/R8)");
                        }
                        else if (obfuscationRate > 20)
                        {
                            LogMessage($"      ⚠️ التطبيق مشوش جزئياً");
                        }
                        else
                        {
                            LogMessage($"      ✅ التطبيق غير مشوش أو مشوش بشكل طفيف");
                        }
                    });
                }
            }
            catch { }
        }

        private void AnalyzeNativeLibraries(string tempDir)
        {
            try
            {
                var libDir = Path.Combine(tempDir, "lib");
                
                if (!Directory.Exists(libDir))
                {
                    Dispatcher.Invoke(() => LogMessage($"   ℹ️ لا توجد مكتبات Native"));
                    return;
                }

                var architectures = Directory.GetDirectories(libDir);
                
                Dispatcher.Invoke(() =>
                {
                    LogMessage($"   🏗️ المعماريات المدعومة: {architectures.Length}");
                });

                long totalLibSize = 0;
                int totalLibCount = 0;

                foreach (var archDir in architectures)
                {
                    var archName = Path.GetFileName(archDir);
                    var soFiles = Directory.GetFiles(archDir, "*.so", SearchOption.AllDirectories);
                    totalLibCount += soFiles.Length;

                    long archSize = 0;
                    foreach (var soFile in soFiles)
                    {
                        archSize += new FileInfo(soFile).Length;
                    }
                    totalLibSize += archSize;

                    Dispatcher.Invoke(() =>
                    {
                        LogMessage($"\n   📱 {archName}:");
                        LogMessage($"      • عدد المكتبات: {soFiles.Length}");
                        LogMessage($"      • الحجم: {FormatFileSize(archSize)}");
                        
                        // List libraries
                        foreach (var soFile in soFiles.Take(10))
                        {
                            var fileName = Path.GetFileName(soFile);
                            var fileSize = new FileInfo(soFile).Length;
                            LogMessage($"      • {fileName} ({FormatFileSize(fileSize)})");
                            
                            // Analyze library
                            AnalyzeNativeLibrary(soFile);
                        }
                        
                        if (soFiles.Length > 10)
                        {
                            LogMessage($"      ... و {soFiles.Length - 10} مكتبة أخرى");
                        }
                    });
                }

                Dispatcher.Invoke(() =>
                {
                    LogMessage($"\n   📊 الإجمالي:");
                    LogMessage($"      • إجمالي المكتبات: {totalLibCount}");
                    LogMessage($"      • الحجم الإجمالي: {FormatFileSize(totalLibSize)}");
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => LogMessage($"   ❌ خطأ: {ex.Message}"));
            }
        }

        private void AnalyzeNativeLibrary(string soPath)
        {
            try
            {
                // Read first bytes to detect format
                using (var fs = new FileStream(soPath, FileMode.Open, FileAccess.Read))
                {
                    var header = new byte[4];
                    fs.Read(header, 0, 4);
                    
                    // ELF magic number: 0x7F 'E' 'L' 'F'
                    if (header[0] == 0x7F && header[1] == 0x45 && header[2] == 0x4C && header[3] == 0x46)
                    {
                        fs.Seek(4, SeekOrigin.Begin);
                        var elfClass = fs.ReadByte();
                        var bitness = elfClass == 1 ? "32-bit" : elfClass == 2 ? "64-bit" : "Unknown";
                        
                        Dispatcher.Invoke(() => LogMessage($"         └─ ELF {bitness}"));
                    }
                }
            }
            catch { }
        }

        private void AnalyzeResources(string tempDir)
        {
            try
            {
                var resDir = Path.Combine(tempDir, "res");
                
                if (!Directory.Exists(resDir))
                {
                    Dispatcher.Invoke(() => LogMessage($"   ℹ️ لا توجد موارد"));
                    return;
                }

                // Layouts
                var layoutFiles = Directory.GetFiles(resDir, "*.xml", SearchOption.AllDirectories)
                    .Where(f => f.Contains("layout")).ToArray();
                
                // Drawables
                var drawableDirs = Directory.GetDirectories(resDir, "drawable*", SearchOption.TopDirectoryOnly);
                var drawableFiles = drawableDirs.SelectMany(d => Directory.GetFiles(d, "*", SearchOption.AllDirectories)).ToArray();
                
                // Values
                var valuesDirs = Directory.GetDirectories(resDir, "values*", SearchOption.TopDirectoryOnly);
                
                // Mipmaps (icons)
                var mipmapDirs = Directory.GetDirectories(resDir, "mipmap*", SearchOption.TopDirectoryOnly);
                var mipmapFiles = mipmapDirs.SelectMany(d => Directory.GetFiles(d, "*", SearchOption.AllDirectories)).ToArray();

                Dispatcher.Invoke(() =>
                {
                    LogMessage($"   🎨 Layouts: {layoutFiles.Length}");
                    LogMessage($"   🖼️ Drawables: {drawableFiles.Length} في {drawableDirs.Length} مجلد");
                    LogMessage($"   📝 Values: {valuesDirs.Length} مجلد");
                    LogMessage($"   🎯 Mipmaps (Icons): {mipmapFiles.Length} في {mipmapDirs.Length} مجلد");
                    
                    // Analyze strings
                    var stringsFile = Path.Combine(resDir, "values", "strings.xml");
                    if (File.Exists(stringsFile))
                    {
                        var content = File.ReadAllText(stringsFile);
                        var stringCount = System.Text.RegularExpressions.Regex.Matches(content, @"<string").Count;
                        LogMessage($"   💬 Strings: {stringCount}");
                    }
                    
                    // Calculate total size
                    long totalSize = 0;
                    foreach (var file in Directory.GetFiles(resDir, "*", SearchOption.AllDirectories))
                    {
                        totalSize += new FileInfo(file).Length;
                    }
                    LogMessage($"   📏 حجم الموارد: {FormatFileSize(totalSize)}");
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => LogMessage($"   ❌ خطأ: {ex.Message}"));
            }
        }

        private void AnalyzeAssets(string tempDir)
        {
            try
            {
                var assetsDir = Path.Combine(tempDir, "assets");
                
                if (!Directory.Exists(assetsDir))
                {
                    Dispatcher.Invoke(() => LogMessage($"   ℹ️ لا توجد Assets"));
                    return;
                }

                var allFiles = Directory.GetFiles(assetsDir, "*", SearchOption.AllDirectories);
                var filesByExtension = allFiles.GroupBy(f => Path.GetExtension(f).ToLower());

                long totalSize = 0;
                foreach (var file in allFiles)
                {
                    totalSize += new FileInfo(file).Length;
                }

                Dispatcher.Invoke(() =>
                {
                    LogMessage($"   📊 إجمالي الملفات: {allFiles.Length}");
                    LogMessage($"   📏 الحجم الإجمالي: {FormatFileSize(totalSize)}");
                    LogMessage($"\n   📂 حسب النوع:");
                    
                    foreach (var group in filesByExtension.OrderByDescending(g => g.Count()).Take(10))
                    {
                        var ext = string.IsNullOrEmpty(group.Key) ? "(بدون امتداد)" : group.Key;
                        var groupSize = group.Sum(f => new FileInfo(f).Length);
                        LogMessage($"      • {ext}: {group.Count()} ملف ({FormatFileSize(groupSize)})");
                    }
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => LogMessage($"   ❌ خطأ: {ex.Message}"));
            }
        }

        private void AnalyzeSecurity(string manifestPath)
        {
            try
            {
                if (!File.Exists(manifestPath))
                    return;

                var content = File.ReadAllText(manifestPath);
                var securityIssues = new List<string>();

                // Check for dangerous permissions
                var dangerousPerms = new[] { "READ_SMS", "SEND_SMS", "READ_CONTACTS", "CAMERA", 
                    "RECORD_AUDIO", "ACCESS_FINE_LOCATION", "READ_CALL_LOG", "WRITE_EXTERNAL_STORAGE" };
                
                foreach (var perm in dangerousPerms)
                {
                    if (content.Contains($"android.permission.{perm}"))
                    {
                        securityIssues.Add($"صلاحية خطرة: {perm}");
                    }
                }

                // Check for debuggable
                if (System.Text.RegularExpressions.Regex.IsMatch(content, @"android:debuggable=""true"""))
                {
                    securityIssues.Add("التطبيق قابل للتصحيح (Debuggable) - ثغرة أمنية!");
                }

                // Check for backup allowed
                if (System.Text.RegularExpressions.Regex.IsMatch(content, @"android:allowBackup=""true"""))
                {
                    securityIssues.Add("النسخ الاحتياطي مفعّل - قد يسمح باستخراج البيانات");
                }

                // Check for exported components
                var exportedCount = System.Text.RegularExpressions.Regex.Matches(content, @"android:exported=""true""").Count;
                if (exportedCount > 0)
                {
                    securityIssues.Add($"{exportedCount} مكون مُصدّر (Exported) - قد يكون عرضة للهجمات");
                }

                // Check for cleartext traffic
                if (System.Text.RegularExpressions.Regex.IsMatch(content, @"android:usesCleartextTraffic=""true"""))
                {
                    securityIssues.Add("يسمح بنقل البيانات غير المشفرة (Cleartext)");
                }

                Dispatcher.Invoke(() =>
                {
                    if (securityIssues.Count > 0)
                    {
                        LogMessage($"   ⚠️ تحذيرات أمنية ({securityIssues.Count}):");
                        foreach (var issue in securityIssues)
                        {
                            LogMessage($"      • {issue}");
                        }
                    }
                    else
                    {
                        LogMessage($"   ✅ لم يتم اكتشاف مشاكل أمنية واضحة");
                    }
                    
                    // Security score
                    var score = Math.Max(0, 100 - (securityIssues.Count * 10));
                    LogMessage($"\n   📊 التقييم الأمني: {score}/100");
                    
                    if (score >= 80)
                        LogMessage($"      ✅ جيد");
                    else if (score >= 60)
                        LogMessage($"      ⚠️ متوسط");
                    else
                        LogMessage($"      ❌ ضعيف - يحتاج تحسين");
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => LogMessage($"   ❌ خطأ: {ex.Message}"));
            }
        }

        private void OpenOutputFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!Directory.Exists(_outputPath))
                {
                    Directory.CreateDirectory(_outputPath);
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = _outputPath,
                    UseShellExecute = true
                });

                LogMessage($"\n📁 فتح مجلد المخرجات: {_outputPath}");
            }
            catch (Exception ex)
            {
                LogMessage($"❌ خطأ في فتح المجلد: {ex.Message}");
            }
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            OutputTextBox.Clear();
            LogMessage("🗑️ تم مسح السجل");
        }

        private void ShowProgress(string message, string detail)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressText.Text = message;
                ProgressDetailText.Text = detail;
                ProgressOverlay.Visibility = Visibility.Visible;
            });
        }

        private void HideProgress()
        {
            Dispatcher.Invoke(() =>
            {
                ProgressOverlay.Visibility = Visibility.Collapsed;
            });
        }

        private void LogMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                OutputTextBox.AppendText($"[{timestamp}] {message}\n");
                OutputTextBox.ScrollToEnd();
            });
        }

        // ═══════════════════════════════════════════════════════
        // كشف السلوكيات المشبوهة والبرمجيات الخبيثة (ADVANCED)
        // ═══════════════════════════════════════════════════════
        private void DetectMaliciousBehavior(string tempDir)
        {
            try
            {
                var maliciousIndicators = new List<string>();
                var highRiskBehaviors = new List<string>();
                var mediumRiskBehaviors = new List<string>();

                // Patterns للسلوكيات المشبوهة
                var maliciousPatterns = new Dictionary<string, (string description, int severity)>
                {
                    // Root Detection & Bypass
                    { @"Lcom/stericson/RootTools", ("استخدام RootTools (كشف Root)", 2) },
                    { @"su\s+binary", ("البحث عن ملف su (Root)", 3) },
                    { @"/system/xbin/su", ("الوصول إلى su binary", 3) },
                    { @"Superuser\.apk", ("فحص تطبيق Superuser", 2) },
                    
                    // Code Injection & Dynamic Loading
                    { @"DexClassLoader", ("تحميل كود ديناميكي (DexClassLoader)", 3) },
                    { @"PathClassLoader", ("تحميل كود من مسار خارجي", 2) },
                    { @"Runtime\.exec", ("تنفيذ أوامر النظام", 3) },
                    { @"ProcessBuilder", ("بناء عمليات نظام", 2) },
                    { @"\.invoke\(", ("استدعاء Reflection (قد يكون تشويش)", 2) },
                    
                    // SMS & Call Interception
                    { @"SmsManager", ("إرسال رسائل SMS", 2) },
                    { @"sendTextMessage", ("إرسال رسائل نصية", 3) },
                    { @"abortBroadcast", ("إيقاف بث الرسائل (اعتراض SMS)", 3) },
                    { @"android\.provider\.Telephony\.SMS_RECEIVED", ("استقبال SMS", 2) },
                    { @"NEW_OUTGOING_CALL", ("مراقبة المكالمات الصادرة", 2) },
                    
                    // Location Tracking
                    { @"LocationManager", ("تتبع الموقع", 1) },
                    { @"getLastKnownLocation", ("الحصول على آخر موقع", 2) },
                    { @"requestLocationUpdates", ("طلب تحديثات الموقع المستمرة", 2) },
                    
                    // Data Exfiltration
                    { @"HttpURLConnection", ("اتصال HTTP (قد ينقل بيانات)", 1) },
                    { @"HttpClient", ("عميل HTTP", 1) },
                    { @"Socket", ("اتصال Socket مباشر", 2) },
                    { @"DataOutputStream", ("إرسال بيانات", 1) },
                    
                    // File System Access
                    { @"/data/data/", ("الوصول إلى بيانات التطبيقات الأخرى", 3) },
                    { @"getExternalStorageDirectory", ("الوصول إلى التخزين الخارجي", 1) },
                    { @"openFileOutput", ("كتابة ملفات", 1) },
                    
                    // Camera & Microphone
                    { @"Camera\.open", ("فتح الكاميرا", 2) },
                    { @"MediaRecorder", ("تسجيل صوت/فيديو", 2) },
                    { @"AudioRecord", ("تسجيل صوتي", 2) },
                    
                    // Contacts & Accounts
                    { @"ContactsContract", ("الوصول إلى جهات الاتصال", 2) },
                    { @"AccountManager", ("الوصول إلى الحسابات", 2) },
                    { @"getAccounts", ("قراءة الحسابات", 2) },
                    
                    // Device Admin
                    { @"DeviceAdminReceiver", ("مستقبل مدير الجهاز", 3) },
                    { @"DevicePolicyManager", ("إدارة سياسات الجهاز", 3) },
                    { @"lockNow", ("قفل الجهاز", 3) },
                    { @"wipeData", ("مسح بيانات الجهاز", 3) },
                    
                    // Obfuscation & Anti-Analysis
                    { @"Cipher\.getInstance", ("استخدام التشفير (قد يخفي بيانات)", 1) },
                    { @"Base64\.decode", ("فك تشفير Base64", 1) },
                    { @"\.dex", ("ملفات DEX إضافية", 2) },
                    
                    // Accessibility Service Abuse
                    { @"AccessibilityService", ("خدمة إمكانية الوصول (قد تُساء استخدامها)", 2) },
                    { @"performGlobalAction", ("تنفيذ إجراءات عامة", 2) },
                    
                    // Package Management
                    { @"PackageManager", ("إدارة الحزم", 1) },
                    { @"getInstalledPackages", ("قراءة التطبيقات المثبتة", 2) },
                    { @"installPackage", ("تثبيت حزم", 3) },
                    { @"deletePackage", ("حذف حزم", 3) },
                    
                    // Notification Interception
                    { @"NotificationListenerService", ("الاستماع للإشعارات", 2) },
                    { @"cancelNotification", ("إلغاء الإشعارات", 2) },
                    
                    // Keylogging Indicators
                    { @"KeyEvent", ("مراقبة أحداث لوحة المفاتيح", 2) },
                    { @"onKey", ("معالج أحداث المفاتيح", 1) },
                    
                    // Screen Capture
                    { @"MediaProjection", ("التقاط الشاشة", 2) },
                    { @"VirtualDisplay", ("عرض افتراضي (تسجيل شاشة)", 2) },
                    
                    // Native Code Execution
                    { @"System\.loadLibrary", ("تحميل مكتبة Native", 2) },
                    { @"System\.load\(", ("تحميل مكتبة من مسار", 2) }
                };

                // البحث في ملفات Smali
                var smaliDirs = Directory.GetDirectories(tempDir, "smali*", SearchOption.TopDirectoryOnly);
                int filesScanned = 0;
                var detectedPatterns = new Dictionary<string, int>();
                
                foreach (var smaliDir in smaliDirs)
                {
                    var smaliFiles = Directory.GetFiles(smaliDir, "*.smali", SearchOption.AllDirectories);
                    
                    foreach (var file in smaliFiles)
                    {
                        filesScanned++;
                        var content = File.ReadAllText(file);
                        var fileName = Path.GetFileName(file);
                        
                        foreach (var pattern in maliciousPatterns)
                        {
                            if (System.Text.RegularExpressions.Regex.IsMatch(content, pattern.Key))
                            {
                                var key = $"{pattern.Value.description} في {fileName}";
                                
                                if (!detectedPatterns.ContainsKey(key))
                                {
                                    detectedPatterns[key] = pattern.Value.severity;
                                    
                                    if (pattern.Value.severity >= 3)
                                    {
                                        highRiskBehaviors.Add(key);
                                    }
                                    else if (pattern.Value.severity == 2)
                                    {
                                        mediumRiskBehaviors.Add(key);
                                    }
                                    else
                                    {
                                        maliciousIndicators.Add(key);
                                    }
                                }
                            }
                        }
                    }
                }

                // فحص الصلاحيات الخطرة من Manifest
                var manifestPath = Path.Combine(tempDir, "AndroidManifest.xml");
                var dangerousPermissions = new List<string>();
                
                if (File.Exists(manifestPath))
                {
                    var manifestContent = File.ReadAllText(manifestPath);
                    
                    var criticalPerms = new[] {
                        "SEND_SMS", "READ_SMS", "RECEIVE_SMS",
                        "CALL_PHONE", "READ_CALL_LOG", "PROCESS_OUTGOING_CALLS",
                        "CAMERA", "RECORD_AUDIO",
                        "READ_CONTACTS", "WRITE_CONTACTS",
                        "ACCESS_FINE_LOCATION", "ACCESS_COARSE_LOCATION",
                        "READ_EXTERNAL_STORAGE", "WRITE_EXTERNAL_STORAGE",
                        "SYSTEM_ALERT_WINDOW", "BIND_DEVICE_ADMIN",
                        "REQUEST_INSTALL_PACKAGES", "REQUEST_DELETE_PACKAGES"
                    };
                    
                    foreach (var perm in criticalPerms)
                    {
                        if (manifestContent.Contains($"android.permission.{perm}"))
                        {
                            dangerousPermissions.Add(perm);
                        }
                    }
                }

                // حساب درجة الخطورة
                var riskScore = 0;
                riskScore += highRiskBehaviors.Count * 15;
                riskScore += mediumRiskBehaviors.Count * 8;
                riskScore += maliciousIndicators.Count * 3;
                riskScore += dangerousPermissions.Count * 5;

                // عرض النتائج
                Dispatcher.Invoke(() =>
                {
                    LogMessage($"   📊 تم فحص {filesScanned} ملف Smali");
                    LogMessage($"   🔍 إجمالي السلوكيات المكتشفة: {detectedPatterns.Count}");
                    
                    if (highRiskBehaviors.Count > 0)
                    {
                        LogMessage($"\n   🚨 سلوكيات عالية الخطورة ({highRiskBehaviors.Count}):");
                        foreach (var behavior in highRiskBehaviors.Take(15))
                        {
                            LogMessage($"      ❌ {behavior}");
                        }
                        if (highRiskBehaviors.Count > 15)
                        {
                            LogMessage($"      ... و {highRiskBehaviors.Count - 15} سلوك آخر");
                        }
                    }
                    
                    if (mediumRiskBehaviors.Count > 0)
                    {
                        LogMessage($"\n   ⚠️ سلوكيات متوسطة الخطورة ({mediumRiskBehaviors.Count}):");
                        foreach (var behavior in mediumRiskBehaviors.Take(10))
                        {
                            LogMessage($"      • {behavior}");
                        }
                        if (mediumRiskBehaviors.Count > 10)
                        {
                            LogMessage($"      ... و {mediumRiskBehaviors.Count - 10} سلوك آخر");
                        }
                    }
                    
                    if (dangerousPermissions.Count > 0)
                    {
                        LogMessage($"\n   🔐 صلاحيات خطرة ({dangerousPermissions.Count}):");
                        foreach (var perm in dangerousPermissions)
                        {
                            LogMessage($"      • {perm}");
                        }
                    }
                    
                    // التقييم النهائي
                    LogMessage($"\n   📊 درجة الخطورة: {riskScore}");
                    
                    string riskLevel;
                    string recommendation;
                    
                    if (riskScore >= 100)
                    {
                        riskLevel = "🔴 خطر عالي جداً";
                        recommendation = "التطبيق يحتوي على سلوكيات خبيثة واضحة! لا يُنصح بتثبيته";
                    }
                    else if (riskScore >= 60)
                    {
                        riskLevel = "🟠 خطر عالي";
                        recommendation = "التطبيق يحتوي على سلوكيات مشبوهة متعددة";
                    }
                    else if (riskScore >= 30)
                    {
                        riskLevel = "🟡 خطر متوسط";
                        recommendation = "التطبيق يحتوي على بعض السلوكيات التي تحتاج مراجعة";
                    }
                    else if (riskScore >= 10)
                    {
                        riskLevel = "🟢 خطر منخفض";
                        recommendation = "التطبيق يبدو آمناً نسبياً";
                    }
                    else
                    {
                        riskLevel = "✅ آمن";
                        recommendation = "لم يتم اكتشاف سلوكيات مشبوهة واضحة";
                    }
                    
                    LogMessage($"   {riskLevel}");
                    LogMessage($"   💡 التوصية: {recommendation}");
                    
                    if (highRiskBehaviors.Count > 0 || riskScore >= 60)
                    {
                        LogMessage($"\n   ⚠️ تحذير: يُنصح بعدم تثبيت هذا التطبيق على جهاز حقيقي!");
                    }
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => LogMessage($"   ❌ خطأ في كشف السلوكيات المشبوهة: {ex.Message}"));
            }
        }

        // ═══════════════════════════════════════════════════════
        // تحليل الشبكات والاتصالات (ADVANCED)
        // ═══════════════════════════════════════════════════════
        private void AnalyzeNetworkConnections(string tempDir)
        {
            try
            {
                var networkFindings = new List<string>();
                var suspiciousUrls = new List<string>();
                var domains = new HashSet<string>();

                // Patterns للبحث عن اتصالات الشبكة
                var urlPattern = @"https?://[a-zA-Z0-9\-\.]+\.[a-zA-Z]{2,}[^\s\""'<>]*";
                var ipPattern = @"\b(?:[0-9]{1,3}\.){3}[0-9]{1,3}\b";

                // قائمة بالنطاقات المشبوهة
                var suspiciousDomainKeywords = new[] { 
                    "bit.ly", "tinyurl", "goo.gl", "ow.ly", "t.co",
                    ".tk", ".ml", ".ga", ".cf", ".gq",
                    "ngrok", "serveo", "localtunnel",
                    "pastebin", "hastebin", "ghostbin",
                    "duckdns", "no-ip", "ddns"
                };

                // البحث في ملفات Smali
                var smaliDirs = Directory.GetDirectories(tempDir, "smali*", SearchOption.TopDirectoryOnly);
                int filesScanned = 0;
                
                foreach (var smaliDir in smaliDirs)
                {
                    var smaliFiles = Directory.GetFiles(smaliDir, "*.smali", SearchOption.AllDirectories);
                    
                    foreach (var file in smaliFiles.Take(500))
                    {
                        filesScanned++;
                        var content = File.ReadAllText(file);
                        
                        var urlMatches = System.Text.RegularExpressions.Regex.Matches(content, urlPattern);
                        foreach (System.Text.RegularExpressions.Match match in urlMatches)
                        {
                            var url = match.Value;
                            networkFindings.Add(url);
                            
                            try
                            {
                                var uri = new Uri(url);
                                domains.Add(uri.Host);
                                
                                foreach (var keyword in suspiciousDomainKeywords)
                                {
                                    if (uri.Host.Contains(keyword))
                                    {
                                        suspiciousUrls.Add($"{url} (يحتوي على: {keyword})");
                                        break;
                                    }
                                }
                            }
                            catch { }
                        }
                        
                        var ipMatches = System.Text.RegularExpressions.Regex.Matches(content, ipPattern);
                        foreach (System.Text.RegularExpressions.Match match in ipMatches)
                        {
                            var ip = match.Value;
                            if (!ip.StartsWith("127.") && !ip.StartsWith("192.168.") && 
                                !ip.StartsWith("10.") && !ip.StartsWith("0."))
                            {
                                networkFindings.Add($"IP: {ip}");
                            }
                        }
                    }
                }

                // البحث في ملفات XML
                var xmlFiles = Directory.GetFiles(tempDir, "*.xml", SearchOption.AllDirectories);
                foreach (var file in xmlFiles)
                {
                    filesScanned++;
                    var content = File.ReadAllText(file);
                    
                    var urlMatches = System.Text.RegularExpressions.Regex.Matches(content, urlPattern);
                    foreach (System.Text.RegularExpressions.Match match in urlMatches)
                    {
                        var url = match.Value;
                        networkFindings.Add(url);
                        
                        try
                        {
                            var uri = new Uri(url);
                            domains.Add(uri.Host);
                            
                            foreach (var keyword in suspiciousDomainKeywords)
                            {
                                if (uri.Host.Contains(keyword))
                                {
                                    suspiciousUrls.Add($"{url} (من XML)");
                                    break;
                                }
                            }
                        }
                        catch { }
                    }
                }

                // تحليل network_security_config.xml
                var networkSecurityConfig = Directory.GetFiles(tempDir, "network_security_config.xml", 
                    SearchOption.AllDirectories).FirstOrDefault();
                
                bool allowsCleartextTraffic = false;
                bool hasCertificatePinning = false;
                
                if (networkSecurityConfig != null && File.Exists(networkSecurityConfig))
                {
                    var configContent = File.ReadAllText(networkSecurityConfig);
                    allowsCleartextTraffic = configContent.Contains("cleartextTrafficPermitted=\"true\"");
                    hasCertificatePinning = configContent.Contains("<pin-set>");
                }

                // عرض النتائج
                Dispatcher.Invoke(() =>
                {
                    LogMessage($"   📊 تم فحص {filesScanned} ملف");
                    LogMessage($"   🌐 إجمالي الاتصالات المكتشفة: {networkFindings.Count}");
                    LogMessage($"   🏢 عدد النطاقات الفريدة: {domains.Count}");
                    
                    if (domains.Count > 0)
                    {
                        LogMessage($"\n   🌍 النطاقات المكتشفة:");
                        foreach (var domain in domains.Take(20))
                        {
                            LogMessage($"      • {domain}");
                        }
                        if (domains.Count > 20)
                        {
                            LogMessage($"      ... و {domains.Count - 20} نطاق آخر");
                        }
                    }
                    
                    if (suspiciousUrls.Count > 0)
                    {
                        LogMessage($"\n   ⚠️ روابط مشبوهة ({suspiciousUrls.Count}):");
                        foreach (var url in suspiciousUrls.Take(10))
                        {
                            LogMessage($"      🚨 {url}");
                        }
                        if (suspiciousUrls.Count > 10)
                        {
                            LogMessage($"      ... و {suspiciousUrls.Count - 10} رابط آخر");
                        }
                    }
                    
                    LogMessage($"\n   🔐 تحليل أمان الشبكة:");
                    
                    if (allowsCleartextTraffic)
                    {
                        LogMessage($"      ⚠️ يسمح بنقل البيانات غير المشفرة (Cleartext)");
                    }
                    else
                    {
                        LogMessage($"      ✅ لا يسمح بنقل البيانات غير المشفرة");
                    }
                    
                    if (hasCertificatePinning)
                    {
                        LogMessage($"      ✅ يستخدم Certificate Pinning (حماية إضافية)");
                    }
                    else
                    {
                        LogMessage($"      ⚠️ لا يستخدم Certificate Pinning");
                    }
                    
                    var httpsCount = networkFindings.Count(u => u.StartsWith("https://"));
                    var httpCount = networkFindings.Count(u => u.StartsWith("http://") && !u.StartsWith("https://"));
                    
                    if (httpCount > 0)
                    {
                        LogMessage($"      ⚠️ يستخدم HTTP غير الآمن: {httpCount} اتصال");
                    }
                    
                    if (httpsCount > 0)
                    {
                        LogMessage($"      ✅ يستخدم HTTPS الآمن: {httpsCount} اتصال");
                    }
                    
                    var securityScore = 100;
                    if (allowsCleartextTraffic) securityScore -= 20;
                    if (!hasCertificatePinning) securityScore -= 10;
                    if (httpCount > 0) securityScore -= 15;
                    if (suspiciousUrls.Count > 0) securityScore -= (suspiciousUrls.Count * 5);
                    securityScore = Math.Max(0, securityScore);
                    
                    LogMessage($"\n   📊 تقييم أمان الشبكة: {securityScore}/100");
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => LogMessage($"   ❌ خطأ في تحليل الشبكات: {ex.Message}"));
            }
        }

        // ═══════════════════════════════════════════════════════
        // كشف البيانات الحساسة والأسرار (ADVANCED)
        // ═══════════════════════════════════════════════════════
        private void DetectSensitiveData(string tempDir)
        {
            try
            {
                var sensitiveFindings = new List<string>();
                var criticalFindings = new List<string>();

                // Patterns للبحث عن البيانات الحساسة (محدّث ومتقدم)
                var patterns = new Dictionary<string, (string label, bool critical)>
                {
                    // Google & Firebase
                    { @"AIza[0-9A-Za-z\-_]{35}", ("🔑 Google/Firebase API Key", true) },
                    { @"ya29\.[0-9A-Za-z\-_]{50,}", ("🔐 Google OAuth Access Token", true) },
                    { @"[0-9]+-[0-9A-Za-z_]{32}\.apps\.googleusercontent\.com", ("🔑 Google Client ID", false) },
                    { @"""type""\s*:\s*""service_account""", ("☁️ Google Service Account JSON", true) },
                    { @"""private_key""\s*:\s*""-----BEGIN", ("☁️ Google SA Private Key", true) },
                    { @"https://[a-z0-9\-]+\.firebaseio\.com", ("🔥 Firebase Database URL", false) },
                    { @"[a-zA-Z0-9\-]+\.firebaseapp\.com", ("🔥 Firebase App URL", false) },

                    // AWS
                    { @"AKIA[0-9A-Z]{16}", ("☁️ AWS Access Key ID", true) },
                    { @"(?i)aws[_\-]?secret[_\-]?access[_\-]?key\s*[=:]\s*[A-Za-z0-9/+=]{40}", ("☁️ AWS Secret Access Key", true) },
                    { @"(?i)aws_access_key_id\s*[=:]\s*[A-Z0-9]{20}", ("☁️ AWS Access Key", true) },

                    // Stripe
                    { @"sk_live_[0-9a-zA-Z]{24,}", ("💳 Stripe Live Secret Key", true) },
                    { @"sk_test_[0-9a-zA-Z]{24,}", ("💳 Stripe Test Secret Key", false) },
                    { @"pk_live_[0-9a-zA-Z]{24,}", ("💳 Stripe Live Public Key", false) },

                    // GitHub
                    { @"ghp_[0-9a-zA-Z]{36}", ("🐙 GitHub Personal Access Token", true) },
                    { @"gho_[0-9a-zA-Z]{36}", ("🐙 GitHub OAuth Token", true) },
                    { @"github_pat_[0-9a-zA-Z_]{80,}", ("🐙 GitHub Fine-Grained PAT", true) },

                    // Slack
                    { @"xox[baprs]-[0-9]{10,13}-[0-9a-zA-Z]{10,}", ("💬 Slack Token", true) },
                    { @"https://hooks\.slack\.com/services/[A-Z0-9]+/[A-Z0-9]+/[a-zA-Z0-9]+", ("💬 Slack Webhook", true) },

                    // SendGrid / Mailgun / Twilio / Square
                    { @"SG\.[a-zA-Z0-9_\-]{22}\.[a-zA-Z0-9_\-]{43}", ("📧 SendGrid API Key", true) },
                    { @"key-[0-9a-zA-Z]{32}", ("📧 Mailgun API Key", true) },
                    { @"AC[a-zA-Z0-9]{32}", ("📞 Twilio Account SID", true) },
                    { @"SK[a-zA-Z0-9]{32}", ("📞 Twilio API SID", true) },
                    { @"sq0atp-[0-9A-Za-z\-_]{22}", ("💳 Square Access Token", true) },
                    { @"sq0csp-[0-9A-Za-z\-_]{43}", ("💳 Square OAuth Secret", true) },

                    // Telegram / Discord
                    { @"\d{8,10}:[a-zA-Z0-9_\-]{35}", ("🤖 Telegram Bot Token", true) },
                    { @"discord(app)?\.com/api/webhooks/\d+/[a-zA-Z0-9_\-]+", ("💬 Discord Webhook URL", true) },
                    { @"[MN][a-zA-Z0-9]{23}\.[a-zA-Z0-9\-_]{6}\.[a-zA-Z0-9\-_]{27}", ("💬 Discord Bot Token", true) },

                    // JWT
                    { @"eyJ[A-Za-z0-9\-_]{20,}\.[A-Za-z0-9\-_]{20,}\.[A-Za-z0-9\-_\+/=]{20,}", ("🎫 JWT Token", true) },

                    // Azure
                    { @"DefaultEndpointsProtocol=https;AccountName=[^;]+;AccountKey=[^;]+", ("☁️ Azure Storage Connection", true) },
                    { @"https://[a-zA-Z0-9\-]+\.vault\.azure\.net", ("☁️ Azure Key Vault URL", false) },

                    // Database Credentials
                    { @"jdbc:(mysql|postgresql|oracle|sqlserver|sqlite|mariadb)://[^\s""'<>]+", ("🗄️ Database JDBC URL", true) },
                    { @"mongodb(\+srv)?://[^\s""'<>]+", ("🗄️ MongoDB Connection String", true) },
                    { @"redis://[^\s""'<>]+", ("🗄️ Redis Connection String", true) },
                    { @"Server\s*=\s*[^;]+;\s*Database\s*=\s*[^;]+;\s*(User Id|Uid)\s*=\s*[^;]+;\s*Password\s*=\s*[^;]+", ("🗄️ SQL Server Connection String", true) },

                    // Private Keys
                    { @"-----BEGIN (RSA |EC |OPENSSH |PGP )?PRIVATE KEY-----", ("🔐 Private Key (PEM)", true) },
                    { @"-----BEGIN CERTIFICATE-----", ("🔐 SSL Certificate", false) },

                    // Passwords & Secrets (Smali/XML)
                    { @"(?i)password\s*[=:]\s*[""'][^""']{4,}[""']", ("🔒 Hardcoded Password", true) },
                    { @"(?i)passwd\s*[=:]\s*[""'][^""']{4,}[""']", ("🔒 Hardcoded Password", true) },
                    { @"(?i)(api_?key|apikey)\s*[=:]\s*[""'][^""']{8,}[""']", ("🔑 Hardcoded API Key", true) },
                    { @"(?i)(secret|secret_?key)\s*[=:]\s*[""'][^""']{8,}[""']", ("🔐 Hardcoded Secret", true) },
                    { @"(?i)(access_?token|auth_?token)\s*[=:]\s*[""'][^""']{8,}[""']", ("🎫 Hardcoded Token", true) },
                    { @"(?i)const-string\s+\w+,\s*""[A-Za-z0-9+/]{32,}==""", ("🔑 Base64 Encoded Key (Smali)", true) },

                    // URLs with credentials
                    { @"https?://[a-zA-Z0-9_\-]+:[a-zA-Z0-9_\-@!#$%&*]{4,}@[a-zA-Z0-9\-\.]+", ("🌐 URL with Credentials", true) },

                    // API Endpoints
                    { @"https?://[a-zA-Z0-9\-\.]+\.[a-zA-Z]{2,}[^\s]*api[^\s""'<>]*", ("🌐 API Endpoint", false) },
                    { @"https?://[^\s]*admin[^\s""'<>]*", ("⚠️ Admin Panel URL", false) },

                    // Email & Phone
                    { @"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}", ("📧 Email Address", false) },
                    { @"\+?[0-9]{10,15}", ("📱 Phone Number", false) },

                    // Credit Card
                    { @"\b(?:4[0-9]{12}(?:[0-9]{3})?|5[1-5][0-9]{14}|3[47][0-9]{13})\b", ("💳 Credit Card Number", true) },

                    // Sentry
                    { @"https://[a-f0-9]{32}@[a-z0-9\.]+\.ingest\.sentry\.io/\d+", ("🐛 Sentry DSN", false) },

                    // Crypto Wallets
                    { @"0x[a-fA-F0-9]{40}\b", ("₿ Ethereum Address", false) },
                    { @"\b[13][a-km-zA-HJ-NP-Z1-9]{25,34}\b", ("₿ Bitcoin Address", false) },
                };

                // البحث في ملفات Smali
                var smaliDirs = Directory.GetDirectories(tempDir, "smali*", SearchOption.TopDirectoryOnly);
                int filesScanned = 0;
                
                void ScanFileForSecrets(string filePath, string displayPath)
                {
                    try
                    {
                        var content = File.ReadAllText(filePath);
                        foreach (var pattern in patterns)
                        {
                            var matches = System.Text.RegularExpressions.Regex.Matches(content, pattern.Key,
                                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                            if (matches.Count > 0)
                            {
                                foreach (System.Text.RegularExpressions.Match match in matches.Cast<System.Text.RegularExpressions.Match>().Take(2))
                                {
                                    var val = match.Value;
                                    var displayVal = val.Length > 50
                                        ? val.Substring(0, 20) + "..." + val.Substring(val.Length - 8)
                                        : val;

                                    var finding = $"{pattern.Value.label} | 📍{displayPath} | 🔑{displayVal}";

                                    if (pattern.Value.critical)
                                        criticalFindings.Add(finding);
                                    else
                                        sensitiveFindings.Add(finding);
                                }
                            }
                        }
                    }
                    catch { }
                }

                foreach (var smaliDir in smaliDirs)
                {
                    var smaliFiles = Directory.GetFiles(smaliDir, "*.smali", SearchOption.AllDirectories);
                    foreach (var file in smaliFiles)
                    {
                        filesScanned++;
                        ScanFileForSecrets(file, Path.GetFileName(file));
                    }
                }

                // البحث في ملفات XML (strings, configs)
                var xmlFiles = Directory.GetFiles(tempDir, "*.xml", SearchOption.AllDirectories);
                foreach (var file in xmlFiles)
                {
                    filesScanned++;
                    ScanFileForSecrets(file, $"xml/{Path.GetFileName(file)}");
                }

                // البحث في Assets (json, txt, properties, config)
                var assetsDir = Path.Combine(tempDir, "assets");
                if (Directory.Exists(assetsDir))
                {
                    var assetFiles = Directory.GetFiles(assetsDir, "*", SearchOption.AllDirectories)
                        .Where(f => {
                            var ext = Path.GetExtension(f).ToLower();
                            return ext == ".txt" || ext == ".json" || ext == ".xml" ||
                                   ext == ".properties" || ext == ".config" || ext == ".yaml" || ext == ".env";
                        });

                    foreach (var file in assetFiles)
                    {
                        filesScanned++;
                        ScanFileForSecrets(file, $"assets/{Path.GetFileName(file)}");
                    }
                }

                // عرض النتائج
                Dispatcher.Invoke(() =>
                {
                    LogMessage($"   📊 تم فحص {filesScanned} ملف");
                    
                    if (criticalFindings.Count > 0)
                    {
                        LogMessage($"\n   🚨 بيانات حساسة حرجة ({criticalFindings.Count}):");
                        foreach (var finding in criticalFindings.Take(25))
                        {
                            var parts = finding.Split('|');
                            LogMessage($"      ❌ {parts[0].Trim()}");
                            if (parts.Length > 1) LogMessage($"         {parts[1].Trim()}");
                            if (parts.Length > 2) LogMessage($"         {parts[2].Trim()}");
                        }
                        if (criticalFindings.Count > 25)
                        {
                            LogMessage($"      ... و {criticalFindings.Count - 25} اكتشاف آخر");
                        }
                    }
                    
                    if (sensitiveFindings.Count > 0)
                    {
                        LogMessage($"\n   ⚠️ بيانات حساسة ({sensitiveFindings.Count}):");
                        foreach (var finding in sensitiveFindings.Take(15))
                        {
                            var parts = finding.Split('|');
                            LogMessage($"      • {parts[0].Trim()}");
                            if (parts.Length > 1) LogMessage($"        {parts[1].Trim()}");
                            if (parts.Length > 2) LogMessage($"        {parts[2].Trim()}");
                        }
                        if (sensitiveFindings.Count > 15)
                        {
                            LogMessage($"      ... و {sensitiveFindings.Count - 15} اكتشاف آخر");
                        }
                    }
                    
                    if (criticalFindings.Count == 0 && sensitiveFindings.Count == 0)
                    {
                        LogMessage($"   ✅ لم يتم اكتشاف بيانات حساسة واضحة");
                    }
                    else
                    {
                        LogMessage($"\n   📊 الإجمالي: {criticalFindings.Count + sensitiveFindings.Count} اكتشاف");
                        
                        if (criticalFindings.Count > 0)
                        {
                            LogMessage($"   🚨 تحذير: تم اكتشاف بيانات حرجة! يجب مراجعتها فوراً");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => LogMessage($"   ❌ خطأ في كشف البيانات الحساسة: {ex.Message}"));
            }
        }

        // ═══════════════════════════════════════════════════════
        // تحليل التشفير وآليات الحماية (ADVANCED)
        // ═══════════════════════════════════════════════════════
        private void AnalyzeCryptography(string tempDir)
        {
            try
            {
                var cryptoFindings = new List<string>();
                var weakCryptoFindings = new List<string>();
                var strongCryptoFindings = new List<string>();

                // Patterns للبحث عن استخدامات التشفير
                var cryptoPatterns = new Dictionary<string, (string description, bool isWeak)>
                {
                    // Strong Cryptography
                    { @"AES/GCM", ("AES-GCM (تشفير قوي)", false) },
                    { @"AES/CBC/PKCS5Padding", ("AES-CBC (تشفير جيد)", false) },
                    { @"RSA/ECB/OAEPWithSHA-256", ("RSA-OAEP SHA-256 (تشفير قوي)", false) },
                    { @"SHA-256", ("SHA-256 (هاش قوي)", false) },
                    { @"SHA-512", ("SHA-512 (هاش قوي جداً)", false) },
                    { @"PBKDF2WithHmacSHA256", ("PBKDF2 SHA-256 (اشتقاق مفاتيح قوي)", false) },
                    { @"Argon2", ("Argon2 (اشتقاق مفاتيح حديث)", false) },
                    { @"ChaCha20", ("ChaCha20 (تشفير حديث)", false) },
                    { @"X25519", ("X25519 (تبادل مفاتيح حديث)", false) },
                    { @"Ed25519", ("Ed25519 (توقيع رقمي حديث)", false) },
                    
                    // Weak/Deprecated Cryptography
                    { @"DES/", ("DES (تشفير ضعيف - مهمل)", true) },
                    { @"3DES", ("3DES (تشفير قديم)", true) },
                    { @"RC4", ("RC4 (تشفير ضعيف جداً)", true) },
                    { @"MD5", ("MD5 (هاش ضعيف - مكسور)", true) },
                    { @"SHA-1", ("SHA-1 (هاش ضعيف)", true) },
                    { @"ECB", ("ECB Mode (وضع غير آمن)", true) },
                    { @"RSA/ECB/PKCS1Padding", ("RSA PKCS1 (قديم - يفضل OAEP)", true) },
                    { @"AES/ECB", ("AES-ECB (وضع غير آمن)", true) },
                    
                    // SSL/TLS
                    { @"TLSv1\.3", ("TLS 1.3 (بروتوكول حديث)", false) },
                    { @"TLSv1\.2", ("TLS 1.2 (بروتوكول جيد)", false) },
                    { @"TLSv1\.1", ("TLS 1.1 (قديم)", true) },
                    { @"TLSv1\.0", ("TLS 1.0 (قديم جداً)", true) },
                    { @"SSLv3", ("SSL 3.0 (غير آمن - مكسور)", true) },
                    
                    // Key Generation
                    { @"KeyGenerator", ("مولد مفاتيح", false) },
                    { @"KeyPairGenerator", ("مولد أزواج مفاتيح", false) },
                    { @"SecureRandom", ("مولد أرقام عشوائية آمن", false) },
                    { @"Random\(\)", ("مولد أرقام عشوائية عادي (غير آمن للتشفير)", true) },
                    
                    // Certificate Pinning
                    { @"CertificatePinner", ("Certificate Pinning (حماية قوية)", false) },
                    { @"TrustManager", ("إدارة الثقة", false) },
                    { @"X509TrustManager", ("X509 Trust Manager", false) },
                    
                    // Obfuscation & Protection
                    { @"ProGuard", ("ProGuard (تشويش الكود)", false) },
                    { @"R8", ("R8 (تشويش حديث)", false) },
                    { @"DexGuard", ("DexGuard (حماية متقدمة)", false) },
                    
                    // Root Detection
                    { @"RootBeer", ("RootBeer (كشف Root)", false) },
                    { @"SafetyNet", ("SafetyNet (فحص أمان Google)", false) },
                    
                    // Keystore
                    { @"AndroidKeyStore", ("Android KeyStore (تخزين آمن)", false) },
                    { @"KeyStore\.getInstance", ("استخدام KeyStore", false) }
                };

                // البحث في ملفات Smali
                var smaliDirs = Directory.GetDirectories(tempDir, "smali*", SearchOption.TopDirectoryOnly);
                int filesScanned = 0;
                var detectedCrypto = new Dictionary<string, int>();
                
                foreach (var smaliDir in smaliDirs)
                {
                    var smaliFiles = Directory.GetFiles(smaliDir, "*.smali", SearchOption.AllDirectories);
                    
                    foreach (var file in smaliFiles)
                    {
                        filesScanned++;
                        var content = File.ReadAllText(file);
                        
                        foreach (var pattern in cryptoPatterns)
                        {
                            if (System.Text.RegularExpressions.Regex.IsMatch(content, pattern.Key))
                            {
                                if (!detectedCrypto.ContainsKey(pattern.Value.description))
                                {
                                    detectedCrypto[pattern.Value.description] = 0;
                                }
                                detectedCrypto[pattern.Value.description]++;
                                
                                if (pattern.Value.isWeak)
                                {
                                    if (!weakCryptoFindings.Contains(pattern.Value.description))
                                        weakCryptoFindings.Add(pattern.Value.description);
                                }
                                else
                                {
                                    if (!strongCryptoFindings.Contains(pattern.Value.description))
                                        strongCryptoFindings.Add(pattern.Value.description);
                                }
                            }
                        }
                        
                        // كشف المفاتيح المشفرة في الكود (Hardcoded Keys)
                        var hardcodedKeyPatterns = new[]
                        {
                            @"const-string\s+v\d+,\s+""[A-Za-z0-9+/]{32,}=""",
                            @"const-string\s+v\d+,\s+""[0-9a-fA-F]{32,}""",
                        };
                        
                        foreach (var keyPattern in hardcodedKeyPatterns)
                        {
                            if (System.Text.RegularExpressions.Regex.IsMatch(content, keyPattern))
                            {
                                cryptoFindings.Add($"مفتاح مشفر محتمل في {Path.GetFileName(file)}");
                            }
                        }
                    }
                }

                // تحليل Native Libraries للتشفير
                var libDir = Path.Combine(tempDir, "lib");
                var nativeCryptoLibs = new List<string>();
                
                if (Directory.Exists(libDir))
                {
                    var soFiles = Directory.GetFiles(libDir, "*.so", SearchOption.AllDirectories);
                    var cryptoLibNames = new[] { "crypto", "ssl", "sodium", "openssl", "boringssl", "mbedtls" };
                    
                    foreach (var soFile in soFiles)
                    {
                        var fileName = Path.GetFileName(soFile).ToLower();
                        foreach (var cryptoLib in cryptoLibNames)
                        {
                            if (fileName.Contains(cryptoLib))
                            {
                                nativeCryptoLibs.Add($"{Path.GetFileName(soFile)} ({cryptoLib})");
                                break;
                            }
                        }
                    }
                }

                // حساب درجة الأمان
                var securityScore = 100;
                securityScore -= weakCryptoFindings.Count * 15;
                securityScore += Math.Min(strongCryptoFindings.Count * 5, 30);
                securityScore = Math.Max(0, Math.Min(100, securityScore));

                // عرض النتائج
                Dispatcher.Invoke(() =>
                {
                    LogMessage($"   📊 تم فحص {filesScanned} ملف Smali");
                    LogMessage($"   🔍 إجمالي استخدامات التشفير: {detectedCrypto.Count}");
                    
                    if (strongCryptoFindings.Count > 0)
                    {
                        LogMessage($"\n   ✅ تشفير قوي وآمن ({strongCryptoFindings.Count}):");
                        foreach (var finding in strongCryptoFindings.Take(15))
                        {
                            var count = detectedCrypto.ContainsKey(finding) ? detectedCrypto[finding] : 0;
                            LogMessage($"      • {finding} ({count} استخدام)");
                        }
                        if (strongCryptoFindings.Count > 15)
                        {
                            LogMessage($"      ... و {strongCryptoFindings.Count - 15} آخر");
                        }
                    }
                    
                    if (weakCryptoFindings.Count > 0)
                    {
                        LogMessage($"\n   ⚠️ تشفير ضعيف أو قديم ({weakCryptoFindings.Count}):");
                        foreach (var finding in weakCryptoFindings)
                        {
                            var count = detectedCrypto.ContainsKey(finding) ? detectedCrypto[finding] : 0;
                            LogMessage($"      ❌ {finding} ({count} استخدام)");
                        }
                    }
                    
                    if (cryptoFindings.Count > 0)
                    {
                        LogMessage($"\n   🔑 مفاتيح مشفرة محتملة ({cryptoFindings.Count}):");
                        foreach (var finding in cryptoFindings.Take(10))
                        {
                            LogMessage($"      ⚠️ {finding}");
                        }
                        if (cryptoFindings.Count > 10)
                        {
                            LogMessage($"      ... و {cryptoFindings.Count - 10} آخر");
                        }
                    }
                    
                    if (nativeCryptoLibs.Count > 0)
                    {
                        LogMessage($"\n   🔧 مكتبات تشفير Native ({nativeCryptoLibs.Count}):");
                        foreach (var lib in nativeCryptoLibs)
                        {
                            LogMessage($"      • {lib}");
                        }
                    }
                    
                    LogMessage($"\n   📊 درجة أمان التشفير: {securityScore}/100");
                    
                    if (securityScore >= 80)
                    {
                        LogMessage($"      ✅ ممتاز - يستخدم تشفير قوي وحديث");
                    }
                    else if (securityScore >= 60)
                    {
                        LogMessage($"      ⚠️ جيد - لكن يحتوي على بعض نقاط الضعف");
                    }
                    else if (securityScore >= 40)
                    {
                        LogMessage($"      ⚠️ متوسط - يحتاج تحسين");
                    }
                    else
                    {
                        LogMessage($"      ❌ ضعيف - يستخدم تشفير قديم أو ضعيف");
                    }
                    
                    if (weakCryptoFindings.Count > 0)
                    {
                        LogMessage($"\n   💡 توصية: استبدال خوارزميات التشفير الضعيفة بأخرى حديثة وآمنة");
                    }
                    
                    if (cryptoFindings.Count > 0)
                    {
                        LogMessage($"   💡 تحذير: وجود مفاتيح مشفرة في الكود يشكل خطراً أمنياً");
                    }
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => LogMessage($"   ❌ خطأ في تحليل التشفير: {ex.Message}"));
            }
        }

        // ═══════════════════════════════════════════════════════
        // تحليل متعمق للمكتبات النيتيف - استخراج Strings + JADX JNI
        // ═══════════════════════════════════════════════════════
        private void DeepAnalyzeNativeLibs(string tempDir)
        {
            try
            {
                var libDir = Path.Combine(tempDir, "lib");
                if (!Directory.Exists(libDir))
                {
                    Dispatcher.Invoke(() => LogMessage($"   ℹ️ لا توجد مكتبات Native في هذا التطبيق"));
                    return;
                }

                var soFiles = Directory.GetFiles(libDir, "*.so", SearchOption.AllDirectories);
                if (soFiles.Length == 0)
                {
                    Dispatcher.Invoke(() => LogMessage($"   ℹ️ لا توجد ملفات .so"));
                    return;
                }

                Dispatcher.Invoke(() => LogMessage($"   📊 إجمالي ملفات .so للتحليل: {soFiles.Length}"));

                // أنماط البيانات الحساسة والمهمة للبحث في strings
                var interestingPatterns = new Dictionary<string, string>
                {
                    { @"https?://[a-zA-Z0-9\-\.]+\.[a-zA-Z]{2,}[^\s]{0,100}", "🌐 URL" },
                    { @"AIza[0-9A-Za-z\-_]{35}", "🔑 Google API Key" },
                    { @"AKIA[0-9A-Z]{16}", "☁️ AWS Key" },
                    { @"sk_live_[0-9a-zA-Z]{24,}", "💳 Stripe Key" },
                    { @"eyJ[A-Za-z0-9\-_]{20,}\.[A-Za-z0-9\-_]{20,}\.[A-Za-z0-9\-_]{20,}", "🎫 JWT Token" },
                    { @"-----BEGIN [A-Z ]+KEY-----", "🔐 Private Key" },
                    { @"(?i)(password|passwd|secret|apikey|api_key)\s*[=:]\s*\S{4,}", "🔒 Credential" },
                    { @"\d{8,10}:[a-zA-Z0-9_\-]{35}", "🤖 Telegram Token" },
                    { @"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}", "📧 Email" },
                    { @"(?i)(exec|system|popen|fork|chmod|chown)\s*\(", "⚠️ System Call" },
                    { @"(?i)dlopen|dlsym|dlclose", "🔧 Dynamic Linking" },
                    { @"/proc/[a-z/]+", "📁 /proc Access" },
                    { @"/system/bin/[a-z]+", "📁 /system/bin Access" },
                    { @"(?i)(root|superuser|su\b)", "👑 Root Reference" },
                    { @"(?i)(frida|xposed|substrate|cydia)", "🛡️ Anti-Tamper Detection" },
                    { @"(?i)(encrypt|decrypt|aes|rsa|hmac|sha)", "🔐 Crypto Reference" },
                    { @"(?i)(inject|hook|patch|bypass)", "⚠️ Code Manipulation" },
                    { @"(?i)(sqlite|database|\.db\b)", "🗄️ Database Reference" },
                    { @"(?i)(camera|microphone|gps|location)", "📷 Sensor Reference" },
                };

                var allFindings = new List<(string lib, string type, string value)>();
                var jniFunctions = new List<(string lib, string func)>();
                var libStats = new List<(string name, int stringCount, long size, string arch)>();

                foreach (var soFile in soFiles)
                {
                    var fileName = Path.GetFileName(soFile);
                    var arch = Path.GetFileName(Path.GetDirectoryName(soFile) ?? "");
                    var fileSize = new FileInfo(soFile).Length;

                    // 1. استخراج Strings من الملف الثنائي
                    var extractedStrings = ExtractBinaryStrings(soFile, minLength: 6);
                    libStats.Add((fileName, extractedStrings.Count, fileSize, arch));

                    // 2. البحث عن JNI functions (Java_com_package_Class_method)
                    foreach (var s in extractedStrings)
                    {
                        if (System.Text.RegularExpressions.Regex.IsMatch(s, @"^Java_[a-zA-Z0-9_]{5,}$"))
                        {
                            jniFunctions.Add((fileName, s));
                        }
                    }

                    // 3. البحث عن أنماط مهمة
                    var content = string.Join("\n", extractedStrings);
                    foreach (var pattern in interestingPatterns)
                    {
                        var matches = System.Text.RegularExpressions.Regex.Matches(
                            content, pattern.Key,
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                        foreach (System.Text.RegularExpressions.Match match in matches.Cast<System.Text.RegularExpressions.Match>().Take(3))
                        {
                            var val = match.Value.Trim();
                            if (val.Length > 80) val = val.Substring(0, 40) + "..." + val.Substring(val.Length - 10);
                            allFindings.Add((fileName, pattern.Value, val));
                        }
                    }

                    // 4. تحليل ELF header متقدم + التحليل العميق الكامل
                    var elfInfo = ParseElfHeader(soFile);
                    Dispatcher.Invoke(() =>
                    {
                        LogMessage($"\n   ╔══ 🗂️ {fileName} [{arch}] ══╗");
                        LogMessage($"   ║  📏 الحجم: {FormatFileSize(fileSize)}");
                        if (elfInfo != null) LogMessage($"   ║  🏗️ ELF: {elfInfo}");
                        LogMessage($"   ║  📝 ASCII Strings: {extractedStrings.Count}");
                        LogMessage($"   ╚{'═' + new string('═', Math.Max(0, fileName.Length + arch.Length + 10))}╝");
                    });

                    // التحليل العميق الكامل لكل مكتبة
                    PerformDeepElfAnalysis(soFile, fileName);
                }

                // عرض JNI Functions
                if (jniFunctions.Count > 0)
                {
                    Dispatcher.Invoke(() =>
                    {
                        LogMessage($"\n   🔗 JNI Functions المكتشفة ({jniFunctions.Count}):");
                        foreach (var (lib, func) in jniFunctions.Take(30))
                        {
                            var javaClass = func.Replace("Java_", "").Replace("_", ".");
                            LogMessage($"      • {func}");
                            LogMessage($"        └─ Java: {javaClass}");
                        }
                        if (jniFunctions.Count > 30)
                            LogMessage($"      ... و {jniFunctions.Count - 30} دالة أخرى");
                    });
                }

                // عرض الاكتشافات المهمة
                if (allFindings.Count > 0)
                {
                    var criticals = allFindings.Where(f =>
                        f.type.Contains("Key") || f.type.Contains("Token") ||
                        f.type.Contains("Credential") || f.type.Contains("JWT") ||
                        f.type.Contains("Private") || f.type.Contains("Stripe")).ToList();

                    var warnings = allFindings.Where(f =>
                        f.type.Contains("System Call") || f.type.Contains("Root") ||
                        f.type.Contains("Tamper") || f.type.Contains("inject") ||
                        f.type.Contains("Manipulation")).ToList();

                    var info = allFindings.Except(criticals).Except(warnings).ToList();

                    Dispatcher.Invoke(() =>
                    {
                        if (criticals.Count > 0)
                        {
                            LogMessage($"\n   🚨 بيانات حساسة في Native Libs ({criticals.Count}):");
                            foreach (var (lib, type, val) in criticals.Take(20))
                            {
                                LogMessage($"      ❌ [{type}] في {lib}");
                                LogMessage($"         🔑 {val}");
                            }
                        }

                        if (warnings.Count > 0)
                        {
                            LogMessage($"\n   ⚠️ سلوكيات مشبوهة في Native Libs ({warnings.Count}):");
                            foreach (var (lib, type, val) in warnings.Take(15))
                            {
                                LogMessage($"      ⚠️ [{type}] في {lib}");
                                LogMessage($"         → {val}");
                            }
                        }

                        if (info.Count > 0)
                        {
                            LogMessage($"\n   📋 معلومات عامة من Native Libs ({info.Count}):");
                            foreach (var (lib, type, val) in info.Take(20))
                            {
                                LogMessage($"      • [{type}] في {lib}: {val}");
                            }
                            if (info.Count > 20)
                                LogMessage($"      ... و {info.Count - 20} اكتشاف آخر");
                        }

                        LogMessage($"\n   📊 ملخص تحليل Native Libs:");
                        LogMessage($"      • إجمالي الاكتشافات: {allFindings.Count}");
                        LogMessage($"      • JNI Functions: {jniFunctions.Count}");
                        if (criticals.Count > 0)
                            LogMessage($"      🚨 بيانات حرجة: {criticals.Count} - تحتاج مراجعة فورية!");
                    });
                }
                else
                {
                    Dispatcher.Invoke(() => LogMessage($"   ✅ لم يتم اكتشاف بيانات حساسة في المكتبات النيتيف"));
                }

                // تحليل JADX للتعرف على JNI declarations من الكود Java
                AnalyzeJniDeclarationsViaJadx(tempDir, jniFunctions);
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => LogMessage($"   ❌ خطأ في تحليل Native Libs: {ex.Message}"));
            }
        }

        // استخراج Strings من الملف الثنائي (مثل أمر strings في Linux)
        private List<string> ExtractBinaryStrings(string filePath, int minLength = 6)
        {
            var results = new List<string>();
            try
            {
                var bytes = File.ReadAllBytes(filePath);
                var current = new StringBuilder();

                for (int i = 0; i < bytes.Length; i++)
                {
                    byte b = bytes[i];

                    // ASCII printable characters
                    if (b >= 0x20 && b <= 0x7E)
                    {
                        current.Append((char)b);
                    }
                    else
                    {
                        if (current.Length >= minLength)
                        {
                            var s = current.ToString().Trim();
                            if (s.Length >= minLength)
                                results.Add(s);
                        }
                        current.Clear();
                    }

                    // حد أقصى للطول لتجنب strings وهمية طويلة جداً
                    if (current.Length > 512)
                    {
                        results.Add(current.ToString().Trim());
                        current.Clear();
                    }
                }

                if (current.Length >= minLength)
                    results.Add(current.ToString().Trim());

                // إزالة التكرار مع الحفاظ على الترتيب
                return results.Distinct().ToList();
            }
            catch { }
            return results;
        }

        // تحليل ELF Header للمكتبة
        private string? ParseElfHeader(string soPath)
        {
            try
            {
                using var fs = new FileStream(soPath, FileMode.Open, FileAccess.Read);
                if (fs.Length < 16) return null;

                var header = new byte[64];
                int read = fs.Read(header, 0, Math.Min(64, (int)fs.Length));
                if (read < 16) return null;

                // تحقق من ELF magic: 0x7F 'E' 'L' 'F'
                if (!(header[0] == 0x7F && header[1] == 0x45 && header[2] == 0x4C && header[3] == 0x46))
                    return null;

                // EI_CLASS: 1 = 32-bit, 2 = 64-bit
                var bitness = header[4] == 1 ? "32-bit" : header[4] == 2 ? "64-bit" : "Unknown";

                // EI_DATA: 1 = Little Endian, 2 = Big Endian
                var endian = header[5] == 1 ? "LE" : header[5] == 2 ? "BE" : "?";

                // e_type (offset 16, 2 bytes)
                var eType = (header[17] << 8) | header[16];
                var typeStr = eType switch
                {
                    1 => "ET_REL",
                    2 => "ET_EXEC",
                    3 => "ET_DYN (Shared)",
                    4 => "ET_CORE",
                    _ => $"0x{eType:X}"
                };

                // e_machine (offset 18, 2 bytes)
                var machine = (header[19] << 8) | header[18];
                var archStr = machine switch
                {
                    0x28 => "ARM (armeabi)",
                    0xB7 => "AArch64 (arm64-v8a)",
                    0x03 => "x86",
                    0x3E => "x86_64",
                    0x08 => "MIPS",
                    _ => $"0x{machine:X}"
                };

                return $"{bitness} | {archStr} | {typeStr} | {endian}";
            }
            catch { }
            return null;
        }

        // تحليل JNI declarations من كود Java باستخدام JADX
        private void AnalyzeJniDeclarationsViaJadx(string tempDir, List<(string lib, string func)> nativeJniFunctions)
        {
            try
            {
                // البحث عن native method declarations في ملفات Smali
                var smaliDirs = Directory.GetDirectories(tempDir, "smali*", SearchOption.TopDirectoryOnly);
                var nativeDeclarations = new List<(string className, string method, string signature)>();

                foreach (var smaliDir in smaliDirs)
                {
                    var smaliFiles = Directory.GetFiles(smaliDir, "*.smali", SearchOption.AllDirectories);

                    foreach (var file in smaliFiles)
                    {
                        try
                        {
                            var content = File.ReadAllText(file);

                            // البحث عن native methods في Smali
                            var nativeMethodMatches = System.Text.RegularExpressions.Regex.Matches(
                                content,
                                @"\.method\s+(?:public|private|protected|static|\s)*native\s+(\w+)\(([^\)]*)\)([^\n]+)",
                                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                            if (nativeMethodMatches.Count > 0)
                            {
                                var className = System.Text.RegularExpressions.Regex.Match(
                                    content, @"\.class\s+\S+\s+L([^;]+);").Groups[1].Value.Replace("/", ".");

                                foreach (System.Text.RegularExpressions.Match m in nativeMethodMatches)
                                {
                                    nativeDeclarations.Add((
                                        className,
                                        m.Groups[1].Value,
                                        $"({m.Groups[2].Value}){m.Groups[3].Value.Trim()}"
                                    ));
                                }
                            }
                        }
                        catch { }
                    }
                }

                if (nativeDeclarations.Count > 0)
                {
                    Dispatcher.Invoke(() =>
                    {
                        LogMessage($"\n   🔗 Native Method Declarations في Java/Smali ({nativeDeclarations.Count}):");
                        LogMessage($"   (هذه الدوال تُنفَّذ في ملفات .so)");

                        foreach (var (cls, method, sig) in nativeDeclarations.Take(25))
                        {
                            LogMessage($"      📌 {cls}.{method}");
                            LogMessage($"         توقيع: {sig}");

                            // ربط بـ JNI function المقابل إن وُجد
                            var jniName = $"Java_{cls.Replace(".", "_")}_{method}";
                            var matchedJni = nativeJniFunctions.FirstOrDefault(j =>
                                j.func.Contains(method) || jniName.Contains(j.func));

                            if (!string.IsNullOrEmpty(matchedJni.func))
                            {
                                LogMessage($"         🔗 JNI match: {matchedJni.func} في {matchedJni.lib}");
                            }
                        }

                        if (nativeDeclarations.Count > 25)
                            LogMessage($"      ... و {nativeDeclarations.Count - 25} دالة أخرى");
                    });
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => LogMessage($"   ⚠️ خطأ في تحليل JNI declarations: {ex.Message}"));
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // محرك التحليل العميق الاحترافي لملفات ELF (.so)
        // ═══════════════════════════════════════════════════════════════

        private record ElfSectionInfo(string Name, uint SHType, ulong Flags, long Offset, long Size, long EntSize);
        private record ElfSecFlags(bool IsPIE, bool HasNX, bool HasGnuStack, bool HasRelro, bool HasFullRelro,
            bool HasStackCanary, bool HasFortify, bool HasCFI, bool HasSafeStack);

        private void PerformDeepElfAnalysis(string soPath, string fileName)
        {
            try
            {
                var data = File.ReadAllBytes(soPath);
                if (data.Length < 64) return;
                if (!(data[0] == 0x7F && data[1] == 0x45 && data[2] == 0x4C && data[3] == 0x46)) return;

                bool is64 = data[4] == 2;
                bool le = data[5] == 1;

                ushort RU16(int o) => le ? (ushort)(data[o] | (data[o + 1] << 8))
                                        : (ushort)((data[o] << 8) | data[o + 1]);
                uint RU32(int o) => le ? (uint)(data[o] | (data[o+1]<<8) | (data[o+2]<<16) | (data[o+3]<<24))
                                       : (uint)((data[o]<<24) | (data[o+1]<<16) | (data[o+2]<<8) | data[o+3]);
                ulong RU64(int o) => le
                    ? (ulong)data[o] | ((ulong)data[o+1]<<8) | ((ulong)data[o+2]<<16) | ((ulong)data[o+3]<<24)
                      | ((ulong)data[o+4]<<32) | ((ulong)data[o+5]<<40) | ((ulong)data[o+6]<<48) | ((ulong)data[o+7]<<56)
                    : ((ulong)data[o]<<56) | ((ulong)data[o+1]<<48) | ((ulong)data[o+2]<<40) | ((ulong)data[o+3]<<32)
                      | ((ulong)data[o+4]<<24) | ((ulong)data[o+5]<<16) | ((ulong)data[o+6]<<8) | (ulong)data[o+7];

                ushort eType = RU16(16);

                long phoff, shoff;
                int phentsize, phnum, shentsize, shnum, shstrndx;
                if (is64)
                {
                    phoff = (long)RU64(32); shoff = (long)RU64(40);
                    phentsize = RU16(54); phnum = RU16(56);
                    shentsize = RU16(58); shnum = RU16(60); shstrndx = RU16(62);
                }
                else
                {
                    phoff = RU32(28); shoff = RU32(32);
                    phentsize = RU16(42); phnum = RU16(44);
                    shentsize = RU16(46); shnum = RU16(48); shstrndx = RU16(50);
                }

                // ─── Parse Section Headers ───────────────────────────────────
                var sections = new List<ElfSectionInfo>();
                long shstrtabOff = 0, shstrtabSize = 0;

                if (shoff > 0 && shnum > 0 && shstrndx < shnum
                    && shoff + shnum * shentsize <= data.Length)
                {
                    long ssOff = shoff + shstrndx * shentsize;
                    shstrtabOff = is64 ? (long)RU64((int)ssOff + 24) : RU32((int)ssOff + 16);
                    shstrtabSize = is64 ? (long)RU64((int)ssOff + 32) : RU32((int)ssOff + 20);

                    for (int i = 0; i < shnum && i < 512; i++)
                    {
                        long sh = shoff + i * shentsize;
                        if (sh + shentsize > data.Length) break;

                        uint nameIdx = RU32((int)sh);
                        uint shType = RU32((int)sh + 4);
                        ulong shFlags, shOffset, shSize, shEntSize;

                        if (is64)
                        {
                            shFlags = RU64((int)sh + 8);
                            shOffset = RU64((int)sh + 24);
                            shSize = RU64((int)sh + 32);
                            shEntSize = RU64((int)sh + 56);
                        }
                        else
                        {
                            shFlags = RU32((int)sh + 8);
                            shOffset = RU32((int)sh + 16);
                            shSize = RU32((int)sh + 20);
                            shEntSize = RU32((int)sh + 36);
                        }

                        string secName = "";
                        if (shstrtabOff > 0 && nameIdx < shstrtabSize)
                        {
                            long no = shstrtabOff + nameIdx;
                            int end = (int)no;
                            while (end < data.Length && data[end] != 0) end++;
                            secName = Encoding.ASCII.GetString(data, (int)no, end - (int)no);
                        }

                        if ((long)shOffset >= 0 && (long)shOffset < data.Length)
                            sections.Add(new ElfSectionInfo(secName, shType, shFlags, (long)shOffset, (long)shSize, (long)shEntSize));
                    }
                }

                // ─── Parse Program Headers (Security Flags) ──────────────────
                bool hasNX = false, hasGnuStack = false, hasRelro = false, hasBindNow = false;

                if (phoff > 0 && phnum > 0 && phoff + phnum * phentsize <= data.Length)
                {
                    for (int i = 0; i < phnum && i < 128; i++)
                    {
                        long ph = phoff + i * phentsize;
                        uint pType = RU32((int)ph);
                        uint pFlags = is64 ? RU32((int)ph + 4) : RU32((int)ph + 24);

                        if (pType == 0x6474e551) { hasGnuStack = true; hasNX = (pFlags & 1) == 0; }
                        else if (pType == 0x6474e552) hasRelro = true;
                    }
                }

                // ─── Parse Dynamic Segment for BIND_NOW ──────────────────────
                var dynSec = sections.FirstOrDefault(s => s.Name == ".dynamic");
                if (dynSec != null)
                {
                    int de = is64 ? 16 : 8;
                    for (long p = dynSec.Offset; p + de <= Math.Min(dynSec.Offset + dynSec.Size, data.Length); p += de)
                    {
                        long tag = is64 ? (long)RU64((int)p) : RU32((int)p);
                        long val = is64 ? (long)RU64((int)p + 8) : RU32((int)p + 4);
                        if (tag == 0x18) { hasBindNow = true; }
                        if (tag == 0x1e && (val & 0x8) != 0) hasBindNow = true;
                        if (tag == 0x6ffffffb && (val & 0x1) != 0) hasBindNow = true;
                        if (tag == 0) break;
                    }
                }

                // ─── Parse Dynamic Symbols (.dynsym + .dynstr) ───────────────
                var imports = new List<string>();
                var exports = new List<string>();

                var dynsymSec = sections.FirstOrDefault(s => s.Name == ".dynsym");
                var dynstrSec = sections.FirstOrDefault(s => s.Name == ".dynstr");

                if (dynsymSec != null && dynstrSec != null && dynsymSec.EntSize > 0)
                {
                    int symSize = is64 ? 24 : 16;
                    int symCount = (int)(dynsymSec.Size / symSize);

                    for (int i = 1; i < symCount && i < 2000; i++)
                    {
                        long so = dynsymSec.Offset + i * symSize;
                        if (so + symSize > data.Length) break;

                        uint stName = RU32((int)so);
                        ushort stShndx;
                        ulong stValue;
                        if (is64)
                        {
                            stShndx = RU16((int)so + 6);
                            stValue = RU64((int)so + 8);
                        }
                        else
                        {
                            stShndx = RU16((int)so + 14);
                            stValue = RU32((int)so + 4);
                        }

                        if (stName > 0 && dynstrSec.Offset + stName < data.Length)
                        {
                            long no = dynstrSec.Offset + stName;
                            int end = (int)no;
                            while (end < data.Length && data[end] != 0 && end - no < 256) end++;
                            var sym = Encoding.ASCII.GetString(data, (int)no, end - (int)no).Trim();

                            if (!string.IsNullOrWhiteSpace(sym))
                            {
                                if (stShndx == 0) imports.Add(sym);
                                else if (stValue > 0) exports.Add(sym);
                            }
                        }
                    }
                }

                // ─── Security Feature Detection via Symbols ───────────────────
                bool hasStackCanary = imports.Any(i => i.Contains("__stack_chk_fail") || i.Contains("__stack_chk_guard"));
                bool hasFortify = imports.Any(i => i.EndsWith("_chk") || i.Contains("__fortify_fail"));
                bool hasCFI = imports.Any(i => i.Contains("__cfi_check") || i.Contains("__ubsan_handle"));
                bool hasSafeStack = imports.Any(i => i.Contains("__safestack_unsafe_stack_ptr"));
                bool isPIE = eType == 3;
                bool hasFullRelro = hasRelro && hasBindNow;

                var secFlags = new ElfSecFlags(isPIE, hasNX, hasGnuStack, hasRelro, hasFullRelro,
                    hasStackCanary, hasFortify, hasCFI, hasSafeStack);

                // ─── Entropy per Section ──────────────────────────────────────
                var sectionEntropies = sections
                    .Where(s => s.Size > 64 && s.Size < 15_000_000 && s.Offset + s.Size <= data.Length)
                    .Select(s => (s.Name, Entropy: CalcElSentropy(data, s.Offset, s.Size), s.Size))
                    .OrderByDescending(e => e.Entropy)
                    .ToList();

                // ─── Wide Strings (UTF-16LE) ──────────────────────────────────
                var wideStrings = ExtractWideStrings(data);

                // ─── XOR Brute Force on .rodata ───────────────────────────────
                var xorFindings = new List<(byte key, string decoded)>();
                var rodataSec = sections.FirstOrDefault(s => s.Name == ".rodata");
                if (rodataSec != null && rodataSec.Size is > 16 and < 3_000_000)
                {
                    var chunk = new byte[Math.Min(rodataSec.Size, 512_000)];
                    Array.Copy(data, rodataSec.Offset, chunk, 0, chunk.Length);
                    xorFindings = TryXorBruteforce(chunk);
                }

                // ─── Suspicious Import Classification ─────────────────────────
                var importCategories = new[]
                {
                    (pattern: "ptrace",            label: "🔍 Anti-Debug"),
                    (pattern: "fork",               label: "🔱 Process Fork"),
                    (pattern: "execve",             label: "⚡ Process Exec"),
                    (pattern: "execl",              label: "⚡ Process Exec"),
                    (pattern: "system",             label: "💀 Shell Execute"),
                    (pattern: "popen",              label: "💀 Shell Execute"),
                    (pattern: "mprotect",           label: "🔓 Memory Perm Mod"),
                    (pattern: "mmap",               label: "🗺️ Memory Map"),
                    (pattern: "dlopen",             label: "🔌 Dynamic Load"),
                    (pattern: "dlsym",              label: "🔌 Dynamic Sym"),
                    (pattern: "inotify_init",       label: "👁️ FS Monitor"),
                    (pattern: "kill",               label: "☠️ Kill Signal"),
                    (pattern: "setuid",             label: "👑 Priv Escalation"),
                    (pattern: "setgid",             label: "👑 Priv Escalation"),
                    (pattern: "getuid",             label: "🔍 UID Check"),
                    (pattern: "connect",            label: "🌐 Network Connect"),
                    (pattern: "socket",             label: "🌐 Socket Create"),
                    (pattern: "getaddrinfo",        label: "🌐 DNS Lookup"),
                    (pattern: "SSL_",               label: "🔐 SSL/TLS"),
                    (pattern: "EVP_",               label: "🔐 OpenSSL Crypto"),
                    (pattern: "AES_",               label: "🔑 AES Crypto"),
                    (pattern: "RSA_",               label: "🔑 RSA Crypto"),
                    (pattern: "HMAC",               label: "🔑 HMAC"),
                    (pattern: "SHA",                label: "🔑 Hash SHA"),
                    (pattern: "MD5",                label: "🔑 Hash MD5"),
                    (pattern: "JNI_OnLoad",         label: "🎯 JNI Entry"),
                    (pattern: "JNI_OnUnload",       label: "🎯 JNI Cleanup"),
                    (pattern: "pthread_create",     label: "🧵 Thread Create"),
                    (pattern: "pthread_mutex",      label: "🧵 Mutex"),
                    (pattern: "__android_log",      label: "📋 Android Logging"),
                };

                var flaggedImports = imports
                    .SelectMany(imp => importCategories
                        .Where(cat => imp.Contains(cat.pattern, StringComparison.OrdinalIgnoreCase))
                        .Take(1)
                        .Select(cat => (imp, cat.label)))
                    .ToList();

                // ─── Anti-Analysis Strings ────────────────────────────────────
                var allAsciiStrings = ExtractBinaryStrings(soPath, minLength: 4);
                var antiPatterns = new (string pattern, string label)[]
                {
                    ("frida",              "🛡️ Frida Detection"),
                    ("xposed",             "🛡️ Xposed Detection"),
                    ("substrate",          "🛡️ Cydia Substrate"),
                    ("magisk",             "🛡️ Magisk Detection"),
                    ("TracerPid",          "🐛 Debugger Check (/proc)"),
                    ("ro.debuggable",      "🐛 Debug Flag Check"),
                    ("ro.secure",          "🔒 Secure Flag Check"),
                    ("ro.build.tags",      "🔒 Build Tags Check"),
                    ("genymotion",         "📱 Emulator Detection"),
                    ("vbox86",             "📱 VirtualBox Detection"),
                    ("goldfish",           "📱 Android Emulator"),
                    ("ranchu",             "📱 Android Emulator"),
                    ("qemu",               "📱 QEMU Emulator"),
                    ("/system/bin/su",     "👑 Root Check"),
                    ("/sbin/su",           "👑 Root Check"),
                    ("Superuser.apk",      "👑 SuperUser Detection"),
                    ("com.topjohnwu",      "👑 Magisk Manager"),
                    ("SafetyNet",          "🏆 SafetyNet Check"),
                    ("attestation",        "🏆 Device Attestation"),
                    ("JADX",               "🔬 JADX Marker"),
                    ("UPX!",               "📦 UPX Packed"),
                };

                var antiAnalysis = antiPatterns
                    .SelectMany(ap => allAsciiStrings
                        .Where(s => s.IndexOf(ap.pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                        .Take(1)
                        .Select(s => (ap.label, s)))
                    .ToList();

                // ─── Display Full Report ──────────────────────────────────────
                Dispatcher.Invoke(() =>
                {
                    // 1. Security Hardening
                    LogMessage($"      ┌─── 🛡️ Security Hardening ───────────────────────────┐");
                    LogMessage($"      │  PIE:          {(secFlags.IsPIE     ? "✅ مُفعّل (ASLR)" : "❌ معطّل - عنوان ثابت!")}");
                    LogMessage($"      │  NX/DEP:       {(secFlags.HasNX     ? "✅ مُفعّل" : secFlags.HasGnuStack ? "❌ معطّل - stack قابل للتنفيذ!" : "⚠️ غير محدد")}");
                    LogMessage($"      │  Stack Canary: {(secFlags.HasStackCanary ? "✅ موجود" : "❌ غير موجود - Stack Overflow ممكن!")}");
                    LogMessage($"      │  RELRO:        {(secFlags.HasFullRelro ? "✅ Full RELRO" : secFlags.HasRelro ? "⚠️ Partial RELRO" : "❌ No RELRO")}");
                    LogMessage($"      │  FORTIFY:      {(secFlags.HasFortify  ? "✅ موجود" : "⚠️ غير موجود")}");
                    LogMessage($"      │  CFI:          {(secFlags.HasCFI      ? "✅ موجود" : "⚠️ غير موجود")}");
                    LogMessage($"      │  SafeStack:    {(secFlags.HasSafeStack ? "✅ موجود" : "⚠️ غير موجود")}");
                    LogMessage($"      └──────────────────────────────────────────────────────┘");

                    // 2. Sections + Entropy
                    if (sectionEntropies.Count > 0)
                    {
                        LogMessage($"      📊 Entropy لاكتشاف التشفير/التغليف:");
                        LogMessage($"         {"القسم",-22} {"الحجم",-10} {"Entropy",-7}  التقييم");
                        LogMessage($"         {new string('─', 58)}");
                        foreach (var (sname, ent, ssize) in sectionEntropies.Take(12))
                        {
                            var bar = new string('▓', Math.Min((int)(ent * 2.5), 16));
                            var rating = ent > 7.2 ? "🔴 مُشفَّر/مُحزَّم!" :
                                         ent > 6.5 ? "🟡 Entropy عالية" :
                                         ent > 3.5 ? "🟢 طبيعي" : "⚪ ثوابت/صفر";
                            LogMessage($"         {sname,-22} {FormatFileSize(ssize),-10} {ent:F3}   {bar} {rating}");
                        }
                    }

                    // 3. ELF Sections summary
                    if (sections.Count > 0)
                    {
                        var importantSecs = sections.Where(s => !string.IsNullOrEmpty(s.Name)).Take(15);
                        LogMessage($"      📂 الأقسام ({sections.Count} إجمالاً):");
                        foreach (var sec in importantSecs)
                        {
                            var typeStr = sec.SHType switch
                            {
                                1 => "PROGBITS", 2 => "SYMTAB", 3 => "STRTAB", 4 => "RELA",
                                5 => "HASH", 6 => "DYNAMIC", 7 => "NOTE", 8 => "NOBITS",
                                9 => "REL", 11 => "DYNSYM", _ => $"0x{sec.SHType:X}"
                            };
                            var flagsStr = "";
                            if ((sec.Flags & 2) != 0) flagsStr += "A";  // SHF_ALLOC
                            if ((sec.Flags & 4) != 0) flagsStr += "X";  // SHF_EXECINSTR (code!)
                            if ((sec.Flags & 1) != 0) flagsStr += "W";  // SHF_WRITE
                            var execMark = (sec.Flags & 4) != 0 ? " 🔴 EXEC" : "";
                            LogMessage($"         {sec.Name,-22} {typeStr,-10} {FormatFileSize(sec.Size),-10} [{flagsStr}]{execMark}");
                        }
                    }

                    // 4. Imports analysis
                    LogMessage($"      📥 Imports: {imports.Count} | 📤 Exports: {exports.Count}");
                    if (flaggedImports.Count > 0)
                    {
                        LogMessage($"      ⚠️ Imports مُصنَّفة ({flaggedImports.Count}):");
                        foreach (var grp in flaggedImports.GroupBy(f => f.label).OrderBy(g => g.Key))
                        {
                            LogMessage($"         {grp.Key}:");
                            foreach (var (imp, _) in grp.Take(4))
                                LogMessage($"            └─ {imp}");
                        }
                    }

                    // 5. JNI Exports
                    var jniExports = exports.Where(e => e.StartsWith("Java_") || e == "JNI_OnLoad" || e == "JNI_OnUnload").ToList();
                    if (jniExports.Count > 0)
                    {
                        LogMessage($"      🎯 JNI Exports ({jniExports.Count}):");
                        foreach (var exp in jniExports.Take(15))
                        {
                            LogMessage($"         • {exp}");
                            if (exp.StartsWith("Java_"))
                            {
                                var parts = exp.Substring(5);
                                var lastUnderscore = parts.LastIndexOf('_');
                                if (lastUnderscore > 0)
                                {
                                    var cls = parts.Substring(0, lastUnderscore).Replace("_", ".");
                                    var method = parts.Substring(lastUnderscore + 1);
                                    LogMessage($"           ↔️  {cls}.{method}()");
                                }
                            }
                        }
                        if (jniExports.Count > 15) LogMessage($"         ... و {jniExports.Count - 15} دالة أخرى");
                    }

                    // 6. Anti-Analysis
                    if (antiAnalysis.Count > 0)
                    {
                        LogMessage($"      🚨 آليات Anti-Analysis ({antiAnalysis.Count}):");
                        foreach (var (label, ctx) in antiAnalysis)
                        {
                            LogMessage($"         {label}");
                            var preview = ctx.Length > 60 ? ctx.Substring(0, 60) + "..." : ctx;
                            LogMessage($"            \"{preview}\"");
                        }
                    }

                    // 7. Wide Strings
                    if (wideStrings.Count > 0)
                    {
                        LogMessage($"      🔤 UTF-16LE Wide Strings ({wideStrings.Count}):");
                        foreach (var ws in wideStrings.Take(8))
                            LogMessage($"         • \"{ws}\"");
                    }

                    // 8. XOR Decoded Strings
                    if (xorFindings.Count > 0)
                    {
                        LogMessage($"      🔓 XOR Brute Force - Strings مفككة ({xorFindings.Count}):");
                        foreach (var (k, decoded) in xorFindings.Take(10))
                            LogMessage($"         Key=0x{k:X2}: \"{decoded}\"");
                    }
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => LogMessage($"      ❌ خطأ في التحليل العميق: {ex.Message}"));
            }
        }

        // Shannon Entropy لاكتشاف التشفير والتغليف
        private double CalcElSentropy(byte[] data, long offset, long size)
        {
            if (size == 0) return 0;
            var freq = new int[256];
            long end = Math.Min(offset + size, data.Length);
            long count = end - offset;
            if (count <= 0) return 0;
            for (long i = offset; i < end; i++) freq[data[i]]++;
            double entropy = 0;
            for (int i = 0; i < 256; i++)
            {
                if (freq[i] > 0)
                {
                    double p = (double)freq[i] / count;
                    entropy -= p * Math.Log(p, 2);
                }
            }
            return entropy;
        }

        // استخراج UTF-16LE Wide Strings
        private List<string> ExtractWideStrings(byte[] data, int minLength = 5)
        {
            var results = new List<string>();
            var sb = new StringBuilder();
            for (int i = 0; i < data.Length - 1; i += 2)
            {
                ushort c = (ushort)(data[i] | (data[i + 1] << 8));
                if (c >= 0x20 && c <= 0x7E)
                {
                    sb.Append((char)c);
                }
                else
                {
                    if (sb.Length >= minLength)
                    {
                        var s = sb.ToString();
                        if (s.All(ch => ch >= 0x20 && ch <= 0x7E))
                            results.Add(s);
                    }
                    sb.Clear();
                }
            }
            if (sb.Length >= minLength) results.Add(sb.ToString());
            return results.Distinct().ToList();
        }

        // XOR Brute Force - كشف الـ strings المخفية بتشفير XOR
        private List<(byte key, string decoded)> TryXorBruteforce(byte[] data)
        {
            var results = new List<(byte, string)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int key = 1; key <= 255; key++)
            {
                var sb = new StringBuilder();

                for (int i = 0; i < data.Length; i++)
                {
                    byte decoded = (byte)(data[i] ^ key);
                    if (decoded >= 0x20 && decoded <= 0x7E)
                    {
                        sb.Append((char)decoded);
                    }
                    else
                    {
                        if (sb.Length >= 8)
                        {
                            var s = sb.ToString();
                            if (IsXorInterestingString(s) && seen.Add(s))
                            {
                                results.Add(((byte)key, s));
                                if (results.Count >= 30) return results;
                            }
                        }
                        sb.Clear();
                    }
                }

                if (sb.Length >= 8)
                {
                    var s = sb.ToString();
                    if (IsXorInterestingString(s) && seen.Add(s))
                    {
                        results.Add(((byte)key, s));
                        if (results.Count >= 30) return results;
                    }
                }
            }
            return results;
        }

        private bool IsXorInterestingString(string s)
        {
            if (s.Length < 8) return false;
            // فقط الـ strings ذات المعنى
            return s.Contains("http", StringComparison.OrdinalIgnoreCase)
                || s.Contains("api", StringComparison.OrdinalIgnoreCase)
                || s.Contains("key", StringComparison.OrdinalIgnoreCase)
                || s.Contains("pass", StringComparison.OrdinalIgnoreCase)
                || s.Contains("secret", StringComparison.OrdinalIgnoreCase)
                || s.Contains("token", StringComparison.OrdinalIgnoreCase)
                || s.Contains("login", StringComparison.OrdinalIgnoreCase)
                || s.Contains("auth", StringComparison.OrdinalIgnoreCase)
                || s.Contains("admin", StringComparison.OrdinalIgnoreCase)
                || s.Contains(".com", StringComparison.OrdinalIgnoreCase)
                || s.Contains("://")
                || s.Contains("AIza")
                || s.Contains("AKIA")
                || s.Contains("bearer", StringComparison.OrdinalIgnoreCase)
                || s.Contains("jwt", StringComparison.OrdinalIgnoreCase)
                || s.Contains("database", StringComparison.OrdinalIgnoreCase)
                || s.Contains("sql", StringComparison.OrdinalIgnoreCase)
                || s.Contains("mongo", StringComparison.OrdinalIgnoreCase);
        }

        private string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F2} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F2} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }
    }
}
