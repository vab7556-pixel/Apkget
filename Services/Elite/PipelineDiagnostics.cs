using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace TcpServerApp.Services.Elite
{
    public class PipelineDiagnostics
    {
        private readonly Action<string> _logger;

        public PipelineDiagnostics(Action<string> logger)
        {
            _logger = logger;
        }

        public async Task<bool> RunDiagnosticsAsync()
        {
            _logger("🔍 Starting Pipeline Diagnostics...");
            bool allPassed = true;

            // 1. JAVA CHECK
            if (ToolLocator.HasJava(out string javaPath))
            {
                _logger($"✅ Java Found: {javaPath}");
                await CheckJavaVersion(javaPath);
            }
            else
            {
                _logger("❌ Java Environment NOT FOUND. Kotlin compilation will fail.");
                allPassed = false;
            }

            // 2. KOTLIN CHECK
            string kotlincPath = Path.Combine(ToolLocator.ResearchToolsRoot, "kotlinc", "bin", "kotlinc.bat");
            if (File.Exists(kotlincPath))
            {
                _logger($"✅ Kotlinc Found: {kotlincPath}");
                // Verify it runs
                bool kRun = await TryRunKotlinVersion(kotlincPath, javaPath);
                if (!kRun) allPassed = false;
            }
            else
            {
                _logger($"❌ Kotlinc NOT FOUND at {kotlincPath}");
                _logger("   Ensure 'kotlinc' folder is inside 'ResearchPayloadTools'");
                allPassed = false;
            }

            // 3. ANDROID JAR CHECK
            string androidJar = ToolLocator.Android36JarPath;
            if (File.Exists(androidJar))
            {
                 _logger($"✅ Android.jar (API 36) Found: {androidJar}");
            }
            else
            {
                _logger($"❌ Android.jar NOT FOUND. Searched at: {androidJar}");
                allPassed = false;
            }

            // 4. TEST COMPILATION
            if (allPassed)
            {
                _logger("🧪 Running Dummy Compilation Test...");
                bool compileSuccess = await TestCompilation(kotlincPath, androidJar, javaPath);
                if (compileSuccess) _logger("✅ Test Compilation PASSED.");
                else 
                {
                    _logger("❌ Test Compilation FAILED.");
                    allPassed = false;
                }
            }

            return allPassed;
        }

        private async Task CheckJavaVersion(string javaExe)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = javaExe,
                    Arguments = "-version",
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var p = Process.Start(psi);
                string outV = await p.StandardError.ReadToEndAsync();
                await p.WaitForExitAsync();
                _logger($"   Version Info: {outV.Split('\n')[0].Trim()}");
            }
            catch { _logger("   ⚠️ Could not read Java version."); }
        }

        private async Task<bool> TryRunKotlinVersion(string kotlinc, string javaPath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = kotlinc,
                    Arguments = "-version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                // CRITICAL: Inject JAVA_HOME if we resolved a specific java path
                if (!string.IsNullOrEmpty(javaPath))
                {
                    string javaHome = Path.GetDirectoryName(Path.GetDirectoryName(javaPath));
                    psi.EnvironmentVariables["JAVA_HOME"] = javaHome;
                }

                var p = Process.Start(psi);
                string outV = await p.StandardOutput.ReadToEndAsync();
                string errV = await p.StandardError.ReadToEndAsync();
                await p.WaitForExitAsync();

                if (p.ExitCode == 0)
                {
                    _logger($"   Kotlin Version: {outV.Trim()} {errV.Trim()}");
                    return true;
                }
                else
                {
                    _logger($"   ⚠️ Kotlinc run failed: {errV}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger($"   ⚠️ Exception running kotlinc: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TestCompilation(string kotlinc, string androidJar, string javaPath)
        {
            string tempParams = Path.Combine(Path.GetTempPath(), $"KtTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempParams);
            string srcFile = Path.Combine(tempParams, "Test.kt");
            string outFile = Path.Combine(tempParams, "test.jar");

            try
            {
                await File.WriteAllTextAsync(srcFile, "package com.test \n import android.app.Service \n class Test : Service() { override fun onBind(i: android.content.Intent?) = null }");

                var psi = new ProcessStartInfo
                {
                    FileName = kotlinc,
                    Arguments = $"\"{srcFile}\" -cp \"{androidJar}\" -d \"{outFile}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                if (!string.IsNullOrEmpty(javaPath))
                {
                    string javaHome = Path.GetDirectoryName(Path.GetDirectoryName(javaPath));
                    psi.EnvironmentVariables["JAVA_HOME"] = javaHome;
                }

                var p = Process.Start(psi);
                string err = await p.StandardError.ReadToEndAsync();
                await p.WaitForExitAsync();

                if (p.ExitCode == 0 && File.Exists(outFile))
                    return true;
                
                _logger($"   Compile Error: {err}");
                return false;
            }
            finally
            {
                try { Directory.Delete(tempParams, true); } catch { }
            }
        }
    }
}
