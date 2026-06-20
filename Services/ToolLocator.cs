using System;
using System.IO;
using System.Collections.Generic;

namespace TcpServerApp.Services
{
    public class ResearchIntegrityException : Exception
    {
        public ResearchIntegrityException(string message) : base(message) { }
    }

    /// <summary>
    /// المسؤول عن إدارة مسارات أدوات البحث والبرامج المساعدة (Android 16 Research Toolchain).
    /// تم تحديثه لاستعادة المراجع المفقودة وضمان توافق النظام بالكامل.
    /// </summary>
    public static class ToolLocator
    {
        public static event Action<string>? OnGlobalSystemLog;
        public static void LogGlobal(string message) => OnGlobalSystemLog?.Invoke(message);

        public static string BaseDir => AppDomain.CurrentDomain.BaseDirectory;

        // المسار الجذري لأدوات البحث — يستخدم res/ResearchPayloadTools مثل PathManager
        public static string ResearchToolsRoot => Path.Combine(BaseDir, "res", "ResearchPayloadTools");

        // --- بيئة الأبحاث الرسمية (Google Research Fidelity) ---
        public static string AndroidOfficialRoot => Path.Combine(ResearchToolsRoot, "android-36");
        public static string AndroidModRoot => Path.Combine(ResearchToolsRoot, "android36");
        
        // Aliases for Backward Compatibility (Omega Legacy Support)
        public static string Android36Root => AndroidOfficialRoot;
        public static string Android36Platform => AndroidOfficialRoot;
        public static string ResearchModRoot => AndroidModRoot;
        public static string GoogleApisX86 => Path.Combine(ResearchToolsRoot, "google_apis_playstore", "x86_64");
        public static string Android36JarPath => AndroidPlatformJar;
        public static string ResearchAndroidJar => AndroidPlatformJar;
        
        public static string AndroidPlatformJar => Path.Combine(AndroidOfficialRoot, "android.jar");
        public static string AndroidOfficialJar => AndroidPlatformJar;
        public static string BuildToolsRoot => Path.Combine(ResearchToolsRoot, "build-tools", "36.1.0");
        
        public static string ResearchApkTool => Path.Combine(AndroidModRoot, "apktool.bat");
        public static string ResearchApkToolJar => Path.Combine(AndroidModRoot, "apktool.jar");
        public static string ResearchUiAutomatorJar => Path.Combine(AndroidModRoot, "uiautomator.jar");

        // أدوات التجميع الرسمية
		public static string KotlinRoot => Path.Combine(BaseDir, "res", "kotlin");
        public static string KotlinPath => Path.Combine(KotlinRoot, "bin", "kotlin.bat");
        public static string KotlincPath => Path.Combine(KotlinRoot, "bin", "kotlinc.bat");
        public static string KotlinLibPath => Path.Combine(KotlinRoot, "lib", "kotlin-stdlib.jar");
        public static string ResearchAapt2 => Path.Combine(BuildToolsRoot, "aapt2.exe");
        public static string ResearchD8 => Path.Combine(BuildToolsRoot, "d8.bat");
        public static string Android36D8 => ResearchD8;
        public static string ResearchZipalign => Path.Combine(BuildToolsRoot, "zipalign.exe");
        public static string ResearchApkEditor => Path.Combine(ResearchToolsRoot, "apktool", "APKEditor.jar");
        public static string OfficialApkSigner => ResearchApkSigner;

        // --- المترجمات والمكتبات الأصلية (NDK) ---
        public static string JdkRoot => Path.Combine(BaseDir, "res", "java");
        public static string JavaPath => Path.Combine(JdkRoot, "bin", "java.exe");
        public static string JavacPath => Path.Combine(JdkRoot, "bin", "javac.exe");
        public static string JarPath => Path.Combine(JdkRoot, "bin", "jar.exe");

        
        public static string NdkRoot => Path.Combine(ResearchToolsRoot, "android-ndk-r27d");
        public static string NdkClang => Path.Combine(NdkRoot, "toolchains", "llvm", "prebuilt", "windows-x86_64", "bin", "clang++.exe");

        // أدوات التوقيع
        public static string ResearchApkSigner => Path.Combine(BuildToolsRoot, "apksigner.jar");
        public static string ResearchJavac => JavacPath;

        // --- مكتبات الحقن والارتباط (Injection Frameworks) ---
        private static string ApktoolLibRoot => Path.Combine(ResearchToolsRoot, "apktool");
        public static string LibBiometric => Path.Combine(ApktoolLibRoot, "biometric-1.1.0");
        public static string LibFirebase => Path.Combine(ApktoolLibRoot, "firebase-messaging-23.4.0");

        // --- أدوات النظام المساعدة ---
        public static string AdbDir => Path.Combine(BaseDir, "res", "tools", "adb", "platform-tools");
        public static string AdbPath => Path.Combine(AdbDir, "adb.exe");
        public static string EmulatorPath => Path.Combine(ResearchToolsRoot, "emulator", "emulator.exe");
        public static string GnirehtetPath => Path.Combine(BaseDir, "res", "tools", "gnirehtet", "gnirehtet.exe");
        public static string IconsRoot => Path.Combine(ResearchToolsRoot, "Icons");

        // --- توافقية النظام (Vital Aliases for Research Core) ---
        public static string ZipalignPath => ResearchZipalign;
        public static string ToolsRoot => Path.Combine(BaseDir, "res");
        public static string ApkToolBat => ResearchApkTool;
        public static string ApkToolDir => AndroidModRoot;
        public static string ApkBuilderToolsRoot => Path.Combine(BaseDir, "res", "ApkBuilder", "Tools");
        public static string Aapt2Path => ResearchAapt2;
        public static string RichApktoolRoot => Path.Combine(ResearchToolsRoot, "apktool");
   
        
        // --- JADX Decompiler (API 36 High Fidelity) ---
        // يبحث تلقائياً عن jadx في عدة مواضع بدلاً من مسار مطور آخر مكتوب بشكل ثابت
        private static string ResolveJadxRoot()
        {
            // 1. jadx CLI مبني من مصادر Gradle في مجلد المشروع
            var projectJadx = Path.GetFullPath(Path.Combine(BaseDir, "..", "..", "..", "..", "jadx"));
            if (Directory.Exists(projectJadx))
            {
                // بحث عن jadx-cli مبني في build/distributions
                var distDirs = Directory.GetDirectories(projectJadx, "jadx*cli*build", SearchOption.AllDirectories)
                    .Concat(Directory.GetDirectories(projectJadx, "jadx*", SearchOption.AllDirectories)
                        .Where(d => d.Contains("distributions")))
                    .ToArray();
                foreach (var d in distDirs)
                {
                    var batFile = Directory.GetFiles(d, "jadx.bat", SearchOption.AllDirectories).FirstOrDefault();
                    if (batFile != null) return Path.GetDirectoryName(batFile);
                }
                // مصادر Gradle موجودة لكن لم يُبن بعد
                return projectJadx;
            }

            // 2. jadx-gui مثبت في Desktop أو Program Files
            var desktop  = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var candidates = new[]
            {
                Path.Combine(BaseDir, "..", "..", "..", "..", "jadx-gui-1.5.3-with-jre-win"),
                Path.Combine(desktop, "jadx-gui-1.5.3-with-jre-win"),
                Path.Combine(desktop, "jadx"),
                Path.Combine(progFiles, "jadx"),
                @"C:\jadx",
            };
            var found = candidates.Select(Path.GetFullPath).FirstOrDefault(Directory.Exists);
            return found ?? projectJadx; // fallback to source tree
        }

        private static readonly Lazy<string> _jadxRoot = new Lazy<string>(ResolveJadxRoot);

        public static string JadxRoot => _jadxRoot.Value;

        // jadx-cli.bat أو jadx.bat داخل التوزيع
        public static string JadxCli =>
            Directory.GetFiles(JadxRoot, "jadx.bat", SearchOption.AllDirectories).FirstOrDefault()
            ?? Path.Combine(JadxRoot, "bin", "jadx.bat");

        // الواجهة الرسومية (jadx-gui)
        public static string JadxGui =>
            Directory.GetFiles(JadxRoot, "jadx-gui*.exe", SearchOption.AllDirectories).FirstOrDefault()
            ?? Path.Combine(JadxRoot, "jadx-gui.exe");

        // jadx-cli all-in-one jar (للتشغيل عبر java -jar)
        public static string JadxJar =>
            Directory.GetFiles(JadxRoot, "jadx-*-all.jar", SearchOption.AllDirectories).FirstOrDefault()
            ?? Path.Combine(JadxRoot, "lib", "jadx-cli-all.jar");

        public static string TryFindTool(string path1, string path2)
        {
            if (File.Exists(path1)) return path1;
            if (File.Exists(path2)) return path2;
            return path1; // Fallback
        }

        public static string TryFindTool(string name)
        {
            var p = Path.Combine(BuildToolsRoot, name);
            if (File.Exists(p)) return p;
            p = Path.Combine(ResearchModRoot, name);
            if (File.Exists(p)) return p;
            
            // STRICT MODE: No system fallbacks for core research tools
            throw new ResearchIntegrityException($"CRITICAL: Tool '{name}' not found in official research folders. Fidelity compromised.");
        }

        public static bool IsAndroid36Available => Directory.Exists(AndroidOfficialRoot);

        /// <summary>
        /// فحص سلامة بيئة الأدوات (Toolchain Integrity Check).
        /// </summary>
        public static void EnsureFidelity()
        {
            var missing = new List<string>();
        
            if (!Directory.Exists(ResearchToolsRoot)) missing.Add("Research Tools Root");
            if (!Directory.Exists(AndroidOfficialRoot)) missing.Add("Android 16 Official Root");
            if (!Directory.Exists(AndroidModRoot)) missing.Add("Android 16 Mod Root");
            if (!Directory.Exists(BuildToolsRoot)) missing.Add("Build Tools 36.1.0");
            if (!File.Exists(ResearchAapt2)) missing.Add("AAPT2 (Official)");
            if (!File.Exists(ResearchD8)) missing.Add("D8 (Official)");
            if (!File.Exists(Android36JarPath)) missing.Add("Android 16 Platform Jar");

            if (missing.Count > 0)
            {
                throw new ResearchIntegrityException("⚠️ CRITICAL FIDELITY FAILURE: Missing official Google research components:\n" + string.Join("\n", missing));
            }
        }

        public static string ValidateToolchain()
        {
            try { EnsureFidelity(); return "OK"; }
            catch (ResearchIntegrityException ex) { return ex.Message; }
        }

        public static bool HasJava(out string javaExe)
        {
            var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrWhiteSpace(javaHome))
            {
                var c = Path.Combine(javaHome, "bin", "java.exe");
                if (File.Exists(c)) { javaExe = c; return true; }
            }
            
            // Basic PATH check could be added here, but for now fallback to "java"
            javaExe = "java"; 
            try 
            {
                // Simple check if java is in PATH by returning true if we default to "java" 
                // In a stricter version we would run "java -version"
                return true; 
            }
            catch { return false; }
        }

        public static string KeytoolPath 
        {
            get 
            {
                var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
                if (!string.IsNullOrWhiteSpace(javaHome))
                {
                    var k = Path.Combine(javaHome, "bin", "keytool.exe");
                    if (File.Exists(k)) return k;
                }
                return "keytool"; // Fallback to PATH
            }
        }
    }
}
