using System;
using System.IO;

namespace TcpServerApp.Services
{
    /// <summary>
    /// إدارة المسارات المركزية - خاصة ببيئة أندرويد 16 البحثية.
    /// تم استبدال ToolLocator بهذا الكلاس لضمان الدقة وتثبيت المسارات.
    /// </summary>
    public static class PathManager
    {
        // الجذر الأساسي للتطبيق
        public static string BaseDir => AppDomain.CurrentDomain.BaseDirectory;

        // الجذر الرئيسي للأدوات البحثية
        public static string ResearchRoot => Path.Combine(BaseDir, "res", "ResearchPayloadTools");

        // جذر أندرويد 16 (تفضيل التحديث 36.1.0)
        public static string AndroidRoot
        {
            get
            {
                string v36_1 = Path.Combine(ResearchRoot, "36.1.0");
                if (Directory.Exists(v36_1)) return v36_1;
                
                return Path.Combine(ResearchRoot, "android-36");
            }
        }

        // ملف android.jar الأساسي (للبناء)
        public static string AndroidJar
        {
            get
            {
                // الخيار 1: مباشر في الجذر (كما لاحظناه في بيئة المستخدم)
                string jarRoot = Path.Combine(ResearchRoot, "android.jar");
                if (File.Exists(jarRoot)) return jarRoot;

                // الخيار 2: داخل 36.1.0/platforms
                string jar36_1 = Path.Combine(AndroidRoot, "platforms", "android-36", "android.jar");
                if (File.Exists(jar36_1)) return jar36_1;

                // الخيار 3: المسار القديم
                return Path.Combine(ResearchRoot, "android-36", "platforms", "android-36", "android.jar");
            }
        }

        // الأدوات التنفيذية
        public static string Aapt2 => Path.Combine(AndroidRoot, "aapt2.exe");
        public static string D8 => Path.Combine(AndroidRoot, "d8.bat");
        public static string ApkSigner => Path.Combine(AndroidRoot, "apksigner.bat");
        public static string ZipAlign => Path.Combine(AndroidRoot, "zipalign.exe");
        
        // أدوات Kotlin
        // ملاحظة: kotlinc عادة في مجلد فرعي kotlinc/bin
        public static string KotlincBin => Path.Combine(ResearchRoot, "kotlinc", "bin", "kotlinc.bat");
        public static string KotlinStdLib => Path.Combine(ResearchRoot, "kotlinc", "lib", "kotlin-stdlib.jar");

        // أدوات مساعدة
        public static string ApkToolJar => Path.Combine(ResearchRoot, "apktool.jar");
        public static string DebugKeystore => Path.Combine(ResearchRoot, "debug.keystore");
        
        // Native Development Kit (NDK) - Android 16
        public static string NdkClang
        {
            get
            {
                // Logic: ResearchRoot/ndk/VER/ ... or ResearchRoot/android-36/ndk ...
                // Try standard locations in ResearchRoot
                string ndkRoot = Path.Combine(AndroidRoot, "ndk");
                if (!Directory.Exists(ndkRoot)) ndkRoot = Path.Combine(ResearchRoot, "ndk");
                
                if (Directory.Exists(ndkRoot))
                {
                    // Find any version subfolder
                    var verDir = System.Linq.Enumerable.FirstOrDefault(Directory.EnumerateDirectories(ndkRoot));
                    if (verDir != null)
                    {
                        string clang = Path.Combine(verDir, "toolchains", "llvm", "prebuilt", "windows-x86_64", "bin", "clang++.exe");
                        if (File.Exists(clang)) return clang;
                    }
                }
                return null;
            }
        }
        
        // التحقق من الصحة
        public static bool IsEnvironmentReady()
        {
            // يكفي أن نتحقق من العناصر الجوهرية
            return File.Exists(AndroidJar) && 
                   File.Exists(Aapt2) && 
                   File.Exists(D8) && 
                   File.Exists(KotlincBin);
        }

        // --- Legacy/Extended Tools Support (Migrated from ToolLocator) ---
        public static string GradleDir => Path.Combine(ResearchRoot, "gradle");
        public static string ApkBuilderDir => Path.Combine(ResearchRoot, "apk_builder");
        public static string ApkBuilderScript => Path.Combine(ApkBuilderDir, "build_pro.bat");
        public static string ProfilesDir => Path.Combine(BaseDir, "Profiles");
        
        // ADB often sits in platform-tools, distinct from build-tools (AndroidRoot often points to build-tools in this custom setup, or SDK root?)
        // Based on previous logic: ResearchRoot/android-36/platform-tools/adb.exe
        public static string AdbPath 
        {
             get 
             {
                 // Try standard android-36 root (which often contains platform-tools)
                 // If AndroidRoot is "36.1.0" (build-tools), we might need to go up?
                 // Let's rely on ResearchRoot/android-36 structure for platform-tools.
                 string adb = Path.Combine(ResearchRoot, "android-36", "platform-tools", "adb.exe");
                 if (!File.Exists(adb)) adb = Path.Combine(ResearchRoot, "platform-tools", "adb.exe");
                 return adb;
             }
        }

        public static string UberApkSignerJar => Path.Combine(ResearchRoot, "uber-apk-signer.jar");
        public static string ApkToolBat => Path.Combine(ResearchRoot, "apktool.bat"); // Wrapper for jar if exists, or jar call?
        // Note: BinderService expected ApkToolBat. If only jar exists, we might need to handle that or assume bat exists.
        
        public static bool HasJava(out string javaPath)
        {
            javaPath = "java";
            try 
            {
                using (var p = new System.Diagnostics.Process())
                {
                    p.StartInfo.FileName = "java";
                    p.StartInfo.Arguments = "-version";
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.CreateNoWindow = true;
                    p.Start();
                    p.WaitForExit();
                    return p.ExitCode == 0;
                }
            } 
            catch { return false; }
        }

        public static string GnirehtetPath => Path.Combine(ResearchRoot, "gnirehtet-rust-win64", "gnirehtet.exe");
        public static string GnirehtetApkPath => Path.Combine(ResearchRoot, "gnirehtet-rust-win64", "gnirehtet.apk");

        // Alias for UI windows expecting "ApkToolDir"
        public static string ApkToolDir => ResearchRoot;

        // Legacy/General Tools
        public static string LegacyToolsRoot => Path.Combine(BaseDir, "res", "tools");
        public static string GeoIpFlagsDir => Path.Combine(LegacyToolsRoot, "GeoIP", "Flags");

        public static string FfmpegPath => Path.Combine(LegacyToolsRoot, "ffmpeg", "bin", "ffmpeg.exe");

        public static string SoxPath => Path.Combine(LegacyToolsRoot, "sox", "sox.exe");

        // ADB/Platform Tools Extensions
        public static string FastbootPath => Path.Combine(Path.GetDirectoryName(AdbPath), "fastboot.exe");
        public static string Sqlite3Path => Path.Combine(Path.GetDirectoryName(AdbPath), "sqlite3.exe");
    }
}
