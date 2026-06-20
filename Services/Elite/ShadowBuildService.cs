using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace TcpServerApp.Services.Elite
{
    public class ShadowBuildService
    {
        public event Action<string>? OnBuildLog;
        public event Action<bool>? OnBuildFinished;

        public async Task<bool> BuildShadowNodeAsync(string buildScriptPath, string workingDir)
        {
            return await Task.Run(() =>
            {
                bool success = false;
                try
                {
                    OnBuildLog?.Invoke("Initializing Shadow Build Engine (Next-Gen Edition)...");

                    // Determine which PowerShell version is available
                    string shell = "powershell.exe";
                    try 
                    {
                        // Check if pwsh.exe is available in the PATH
                        Process probe = new Process { StartInfo = new ProcessStartInfo("pwsh.exe", "-Command exit") { CreateNoWindow = true, UseShellExecute = false } };
                        probe.Start();
                        probe.WaitForExit();
                        shell = "pwsh.exe";
                    }
                    catch { /* Fallback to powershell.exe */ }

                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = shell,
                        Arguments = $"-ExecutionPolicy Bypass -File \"{buildScriptPath}\"",
                        WorkingDirectory = workingDir,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using (Process process = new Process { StartInfo = startInfo })
                    {
                        process.OutputDataReceived += (sender, e) =>
                        {
                            if (e.Data != null) OnBuildLog?.Invoke(e.Data);
                        };
                        process.ErrorDataReceived += (sender, e) =>
                        {
                            if (e.Data != null) OnBuildLog?.Invoke($"[ERROR] {e.Data}");
                        };

                        process.Start();
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();
                        process.WaitForExit();

                        success = (process.ExitCode == 0);
                        OnBuildLog?.Invoke(success ? "BUILD SUCCESS: Shadow Node generated." : "BUILD FAILED: See logs for details.");
                        OnBuildFinished?.Invoke(success);
                    }
                }
                catch (Exception ex)
                {
                    OnBuildLog?.Invoke($"[CRITICAL ERROR] {ex.Message}");
                    OnBuildFinished?.Invoke(false);
                }
                return success;
            });
        }
    }
}
