using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TcpServerApp.Services.Ai
{
    /// <summary>
    /// خدمة JADX لفك تشفير APK وتحويله إلى Java/Kotlin قابل للقراءة.
    /// تعتمد على jadx-cli من مجلد jadx/ (مصادر Gradle) أو jadx-gui المثبت.
    /// </summary>
    public class JadxService
    {
        private readonly string _jadxCli;   // jadx.bat أو jadx-cli
        private readonly string _javaExe;

        public JadxService()
        {
            _jadxCli = ToolLocator.JadxCli;
            ToolLocator.HasJava(out _javaExe);
        }

        /// <summary>
        /// فك تشفير APK كاملاً (Java + Resources) إلى مجلد الإخراج.
        /// يستخدم jadx.bat المبني من المصادر.
        /// </summary>
        public async Task<string> DecompileToOutputAsync(
            string apkPath,
            string outputDir,
            CancellationToken ct = default)
        {
            if (!File.Exists(apkPath))
                return "Error: APK not found.";

            Directory.CreateDirectory(outputDir);

            return await Task.Run(() =>
            {
                try
                {
                    // jadx -d [out-dir] [apk] -- تفكيك Java + Resources
                    // إزالة --no-src --no-res التي كانت تمنع الفائدة الحقيقية
                    string arguments = $"-d \"{outputDir}\" \"{apkPath}\" --threads-count 4";

                    string exe = File.Exists(_jadxCli) ? _jadxCli : "jadx";

                    var psi = new ProcessStartInfo
                    {
                        FileName               = exe,
                        Arguments              = arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError  = true,
                        UseShellExecute        = false,
                        CreateNoWindow         = true
                    };

                    using var proc = Process.Start(psi)!;

                    // timeout 3 دقائق — jadx يحتاج وقتاً للملفات الكبيرة
                    bool finished = proc.WaitForExit((int)TimeSpan.FromMinutes(3).TotalMilliseconds);
                    if (!finished) { proc.Kill(); return "Error: jadx timeout (3 min)"; }

                    string output = proc.StandardOutput.ReadToEnd();
                    string error  = proc.StandardError.ReadToEnd();

                    if (proc.ExitCode == 0)
                        return $"✅ Decompilation complete → {outputDir}";
                    
                    string msg = string.IsNullOrWhiteSpace(error) ? output : error;
                    return $"⚠️ jadx exited with code {proc.ExitCode}\n{msg[..Math.Min(500, msg.Length)]}";
                }
                catch (Exception ex)
                {
                    return $"Exception: {ex.Message}";
                }
            }, ct);
        }

        /// <summary>
        /// فك تشفير Java فقط بدون resources — أسرع للتحليل الأمني.
        /// </summary>
        public Task<string> DecompileJavaOnlyAsync(
            string apkPath,
            string outputDir,
            CancellationToken ct = default)
        {
            Directory.CreateDirectory(outputDir);

            return Task.Run(() =>
            {
                try
                {
                    string exe = File.Exists(_jadxCli) ? _jadxCli : "jadx";
                    string arguments = $"-d \"{outputDir}\" \"{apkPath}\" --no-res --threads-count 4";

                    var psi = new ProcessStartInfo
                    {
                        FileName               = exe,
                        Arguments              = arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError  = true,
                        UseShellExecute        = false,
                        CreateNoWindow         = true
                    };

                    using var proc = Process.Start(psi)!;
                    bool finished = proc.WaitForExit((int)TimeSpan.FromMinutes(3).TotalMilliseconds);
                    if (!finished) { proc.Kill(); return "Error: jadx timeout"; }

                    return proc.ExitCode == 0
                        ? $"✅ Java decompiled → {outputDir}"
                        : $"⚠️ Exit code {proc.ExitCode}";
                }
                catch (Exception ex) { return $"Exception: {ex.Message}"; }
            }, ct);
        }

        /// <summary>
        /// التحقق ما إذا كان jadx قابلاً للتشغيل على الجهاز الحالي.
        /// </summary>
        public bool IsAvailable()
        {
            return File.Exists(_jadxCli);
        }

        /// <summary>
        /// المسار الكامل لـ jadx-cli المُحدَّد.
        /// </summary>
        public string JadxCliPath => _jadxCli;
    }
}
