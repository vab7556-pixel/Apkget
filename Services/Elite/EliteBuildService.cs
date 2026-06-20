using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TcpServerApp.Services
{
    public class BuildResult
    {
        public bool   Success  { get; set; }
        public int    ExitCode { get; set; } = -1;
        public string Logs     { get; set; } = string.Empty;
        public string Error    { get; set; } = string.Empty;

        /// <summary>نص موحَّد للعرض في الواجهة.</summary>
        public string Summary => Success
            ? $"✅ Success | {Logs.Split('\n')[0].Trim()}"
            : $"❌ Failed (exit {ExitCode}) | {Error.Split('\n')[0].Trim()}";
    }

    /// <summary>
    /// Elite Build Service for end-to-end APK manipulation.
    /// Integrates University of Aleppo's APKEditor and Official Google apksigner.
    /// </summary>
    public class EliteBuildService
    {
        private readonly string _javaPath;
        private readonly string _apkEditorRoot;
        private readonly string _apkSignerJar;
        private readonly bool _useExplodedMode;

        public event Action<string>? OnLog;

        public EliteBuildService()
        {
            if (ToolLocator.HasJava(out string javaPath))
            {
                _javaPath = javaPath;
            }
            else
            {
                _javaPath = "java";
            }

            _apkEditorRoot = Path.GetDirectoryName(ToolLocator.ResearchApkEditor) ?? "";
            string explodedDir = Path.Combine(_apkEditorRoot, "APKEditor");
            
            // Checking if the academic research team has unpacked/customized the library
            _useExplodedMode = Directory.Exists(explodedDir);
            
            _apkSignerJar = ToolLocator.OfficialApkSigner;
        }

        private string GetApkEditorExecutionArgs(string subCommand)
        {
            if (_useExplodedMode)
            {
                // Professional Link: Execute directly from the academic source tree
                string classpath = Path.Combine(_apkEditorRoot, "APKEditor");
                return $"-cp \"{classpath}\" com.reandroid.apkeditor.Main {subCommand}";
            }
            else
            {
                // Fallback to legacy JAR execution
                return $"-jar \"{ToolLocator.ResearchApkEditor}\" {subCommand}";
            }
        }

        public async Task<BuildResult> ReArchiveLibraryAsync(string targetJarPath)
        {
            var result = new BuildResult();
            if (!_useExplodedMode) { result.Success = false; result.Error = "Not in exploded mode."; return result; }
            Log("[ELITE_SYNC] Re-assembling customized APKEditor.jar from research tree...");
            string explodedDir = Path.Combine(_apkEditorRoot, "APKEditor");
            
            // Using JDK 21 jar tool to bundle the customizations
            string args = $"cvfM \"{targetJarPath}\" -C \"{explodedDir}\" .";
            return await ExecuteCommandWithToolAsync("jar", args);
        }

        public async Task<BuildResult> DecodeApkAsync(string apkPath, string outputDir, CancellationToken ct = default)
        {
            if (!File.Exists(apkPath))
                return Fail($"APK غير موجود: {apkPath}");

            Log($"[ELITE_BUILD] Decoding APK (Mode: {(_useExplodedMode ? "EXPLODED" : "JAR")}): {Path.GetFileName(apkPath)}");
            string frameworkArg = $"-framework \"{ToolLocator.Android36JarPath}\"";
            string args = GetApkEditorExecutionArgs($"d {frameworkArg} -i \"{apkPath}\" -o \"{outputDir}\"");
            return await ExecuteJavaCommandAsync(args, ct);
        }

        public async Task<BuildResult> BuildApkAsync(string inputDir, string outputApk, bool optimize = true, CancellationToken ct = default)
        {
            if (!Directory.Exists(inputDir))
                return Fail($"مجلد المصدر غير موجود: {inputDir}");

            Log($"[ELITE_BUILD] Re-assembling Package: {Path.GetFileName(outputApk)}");
            string optimizeFlag = optimize ? "" : "-uncompressed";
            string frameworkArg = $"-framework \"{ToolLocator.Android36JarPath}\"";
            string args = GetApkEditorExecutionArgs($"b {optimizeFlag} {frameworkArg} -i \"{inputDir}\" -o \"{outputApk}\"");

            var result = await ExecuteJavaCommandAsync(args, ct);

            if (result.Success)
            {
                Log("[ELITE_LINK] Build Successful. Transitioning to Automated Signing Pipeline...");
                var signResult = await SignApkAsync(outputApk, ct);
                result.Success  = signResult.Success;
                result.ExitCode = signResult.ExitCode;
                result.Logs    += "\n" + signResult.Logs;
                result.Error   += signResult.Success ? "" : "\n" + signResult.Error;
            }

            return result;
        }

        public async Task<BuildResult> MergeApksAsync(string mainApk, string[] sidecars, string outputApk, CancellationToken ct = default)
        {
            if (!File.Exists(mainApk))
                return Fail($"APK الرئيسي غير موجود: {mainApk}");
            var missing = sidecars.Where(s => !File.Exists(s)).ToList();
            if (missing.Any())
                return Fail($"ملفات sidecar مفقودة: {string.Join(", ", missing.Select(Path.GetFileName))}");

            Log($"[ELITE_BUILD] Merging {sidecars.Length} sidecars into {Path.GetFileName(mainApk)}");
            string sidecarArgs = string.Join(" ", sidecars.Select(s => $"-i \"{s}\""));
            string args = GetApkEditorExecutionArgs($"m -i \"{mainApk}\" {sidecarArgs} -o \"{outputApk}\"");
            return await ExecuteJavaCommandAsync(args, ct);
        }

        public async Task<BuildResult> ProtectApkAsync(string apkPath, string outputApk, int level, CancellationToken ct = default)
        {
            if (!File.Exists(apkPath))
                return Fail($"APK غير موجود: {apkPath}");
            if (level < 1 || level > 5)
                return Fail($"مستوى الحماية غير صالح: {level}. القيم المقبولة 1-5.");

            Log($"[ELITE_BUILD] Applying Protection (Level {level}) to {Path.GetFileName(apkPath)}");
            string protectArgs = level >= 4 ? "-res -manifest" : "-res";
            string args = GetApkEditorExecutionArgs($"p {protectArgs} -i \"{apkPath}\" -o \"{outputApk}\"");
            return await ExecuteJavaCommandAsync(args, ct);
        }

        public async Task<BuildResult> RefactorApkAsync(string apkPath, string outputApk, string newPackageName, CancellationToken ct = default)
        {
            if (!File.Exists(apkPath))
                return Fail($"APK غير موجود: {apkPath}");
            if (string.IsNullOrWhiteSpace(newPackageName) || !newPackageName.Contains('.'))
                return Fail($"اسم الحزمة غير صالح: '{newPackageName}'. يجب أن يحتوي على نقطة (مثال: com.example.app).");

            Log($"[ELITE_BUILD] Refactoring Package to: {newPackageName}");
            string args = GetApkEditorExecutionArgs($"x -p \"{newPackageName}\" -i \"{apkPath}\" -o \"{outputApk}\"");
            return await ExecuteJavaCommandAsync(args, ct);
        }

        public async Task<BuildResult> SignApkAsync(string apkPath, CancellationToken ct = default)
        {
            if (!File.Exists(apkPath))
                return Fail($"APK غير موجود للتوقيع: {apkPath}");

            // سلسلة بحث عن debug.keystore
            string keystore = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "res", "debug.keystore"),
                Path.Combine(ToolLocator.ResearchToolsRoot, "debug.keystore"),
                Path.Combine(ToolLocator.RichApktoolRoot,  "debug.keystore"),
                Path.Combine(ToolLocator.Android36Root,     "debug.keystore"),
            }.FirstOrDefault(File.Exists) ?? "";

            if (string.IsNullOrEmpty(keystore))
                return Fail("debug.keystore غير موجود في أي مسار معروف.");

            Log($"[ELITE_BUILD] Signing APK: {Path.GetFileName(apkPath)} | Keystore: {Path.GetFileName(keystore)}");
            string args = $"sign --ks \"{keystore}\" --ks-pass pass:android \"{apkPath}\"";
            return await ExecuteCommandWithToolAsync(_apkSignerJar, args, ct);
        }

        /// <summary>نتيجة فاشلة فورية دون تشغيل عملية.</summary>
        private static BuildResult Fail(string reason) =>
            new BuildResult { Success = false, ExitCode = -1, Error = reason };

        private async Task<BuildResult> ExecuteJavaCommandAsync(string arguments, CancellationToken ct = default)
            => await ExecuteCommandInternalAsync(_javaPath, arguments, ct);

        private async Task<BuildResult> ExecuteCommandWithToolAsync(string toolName, string arguments, CancellationToken ct = default)
            => await ExecuteCommandInternalAsync(toolName, arguments, ct);

        /// <summary>
        /// تنفيذ عملية خارجية بشكل async حقيقي مع دعم الإلغاء والـ timeout.
        /// </summary>
        private async Task<BuildResult> ExecuteCommandInternalAsync(
            string fileName, string arguments, CancellationToken ct = default)
        {
            var result        = new BuildResult();
            var outputBuilder = new StringBuilder();
            var errorBuilder  = new StringBuilder();

            try
            {
                Log($"⚡ EXEC: {Path.GetFileName(fileName)} {arguments[..Math.Min(120, arguments.Length)]}...");

                var psi = new ProcessStartInfo
                {
                    FileName               = fileName,
                    Arguments              = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };

                // حقن JAVA_HOME إذا كنا نشغّل java
                if (fileName == _javaPath && ToolLocator.HasJava(out string javaExe))
                    psi.EnvironmentVariables["JAVA_HOME"] =
                        Path.GetDirectoryName(Path.GetDirectoryName(javaExe)) ?? "";

                using var process = new Process { StartInfo = psi };

                process.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    { outputBuilder.AppendLine(e.Data); Log($"[OUT] {e.Data}"); }
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    { errorBuilder.AppendLine(e.Data); Log($"[ERR] {e.Data}"); }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // انتظار async مع timeout 90 ثانية + CancellationToken
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
                using var linkedCts  = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

                try
                {
                    await process.WaitForExitAsync(linkedCts.Token);
                }
                catch (OperationCanceledException)
                {
                    try { process.Kill(true); } catch { }
                    if (ct.IsCancellationRequested)
                        throw;
                    result.Error   = $"العملية تجاوزت 90 ثانية: {Path.GetFileName(fileName)}";
                    result.Success = false;
                    return result;
                }

                result.ExitCode = process.ExitCode;
                result.Success  = process.ExitCode == 0;
                result.Logs     = outputBuilder.ToString();
                result.Error    = errorBuilder.ToString();

                if (!result.Success)
                    Log($"⚠️ ExitCode={result.ExitCode} | {result.Error.Split('\n')[0].Trim()}");

                return result;
            }
            catch (Exception ex)
            {
                Log($"❌ EXCEPTION: {ex.Message}");
                result.Success = false;
                result.Error   = ex.Message;
                return result;
            }
        }

        private void Log(string message)
        {
            OnLog?.Invoke(message);
            ToolLocator.LogGlobal(message);
        }
    }
}
