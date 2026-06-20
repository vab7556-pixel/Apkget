using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace TcpServerApp.Services.Elite.Build
{
    public class ApkSignerTool
    {
        private readonly Action<string> _logger;

        public ApkSignerTool(Action<string> logger = null)
        {
            _logger = logger;
        }

        public async Task SignApkAsync(string inputApk, string outputApk, string keystorePath, string storePass, string keyAlias, string keyPass = null)
        {
            if (string.IsNullOrEmpty(keyPass)) keyPass = storePass;

            // java -jar apksigner.jar sign --ks ...
            var args = new System.Collections.Generic.List<string>
            {
                "-jar", ToolPathManager.ApkSignerJarPath,
                "sign",
                "--ks", keystorePath,
                "--ks-pass", $"pass:{storePass}",
                "--ks-key-alias", keyAlias,
                "--key-pass", $"pass:{keyPass}",
                "--out", outputApk,
                inputApk
            };

            await RunApkSignerAsync(args.ToArray());

            if (!File.Exists(outputApk))
            {
                throw new FileNotFoundException("Signing completed but output APK not found.");
            }
        }

        public async Task VerifyApkAsync(string apkPath)
        {
             var args = new[] 
             { 
                 "-jar", ToolPathManager.ApkSignerJarPath,
                 "verify", 
                 apkPath 
             };
            await RunApkSignerAsync(args);
        }

        private async Task RunApkSignerAsync(string[] args)
        {
             _logger?.Invoke($"[Signer CMD] {ToolPathManager.JavaPath} {string.Join(" ", args)}");

             var startInfo = new ProcessStartInfo
            {
                FileName = ToolPathManager.JavaPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach(var arg in args) startInfo.ArgumentList.Add(arg);

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new Exception($"ApkSigner Failed: {error}\n{output}");
            }

            if (!string.IsNullOrWhiteSpace(output)) _logger?.Invoke($"[Signer] {output}");
        }
    }
}
