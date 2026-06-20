using System;
using System.IO;
using System.Linq;
using TcpServerApp.Services;

namespace TcpServerApp.Services.Elite.Build
{
    public static class ToolPathManager
    {
        public static string BaseResDir => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "res", "ResearchPayloadTools");

        public static string GetBuildToolsDir()
        {
            // Specifically look for 36.1.0
            string specific = Path.Combine(BaseResDir, "build-tools", "36.1.0");
            if (Directory.Exists(specific)) return specific;

            throw new DirectoryNotFoundException($"Android 36 Build Tools not found at {specific}");
        }

        public static string BinDir => Path.Combine(GetBuildToolsDir(), "bin");

        public static string D8Path => Path.Combine(GetBuildToolsDir(), "d8.bat");
        public static string D8JarPath => Path.Combine(GetBuildToolsDir(), "lib", "d8.jar");
        
        public static string ApkSignerPath => Path.Combine(GetBuildToolsDir(), "apksigner.bat");
        public static string ApkSignerJarPath => Path.Combine(GetBuildToolsDir(), "lib", "apksigner.jar");
        
        public static string Aapt2Path => Path.Combine(GetBuildToolsDir(), "aapt2.exe");
        public static string ZipAlignPath => Path.Combine(GetBuildToolsDir(), "zipalign.exe");

        // JDK Tools — sourced from the bundled JDK in res/platformBinary64, NOT from Android build-tools
        public static string JavacPath => ToolLocator.JavacPath;
        public static string JavaPath => ToolLocator.JavaPath;
        public static string JarPath => ToolLocator.JarPath;
        public static string KeytoolPath => ToolLocator.KeytoolPath;

        public static string GetAndroidJarPath()
        {
            // We found it in android-36/platforms/android-36.1/android.jar
            string path = Path.Combine(BaseResDir, "android-36", "platforms", "android-36.1", "android.jar");
            if (File.Exists(path)) return path;

            throw new FileNotFoundException($"android.jar (API 36) not found at {path}");
        }

        public static bool ValidateTools()
        {
            return File.Exists(Aapt2Path) &&
                   File.Exists(D8Path) &&
                   File.Exists(ApkSignerPath) &&
                   File.Exists(ZipAlignPath) &&
                   File.Exists(GetAndroidJarPath()) &&
                   File.Exists(JavacPath); // Validates bundled JDK javac (res/platformBinary64/bin/javac.exe)
        }
    }
}
