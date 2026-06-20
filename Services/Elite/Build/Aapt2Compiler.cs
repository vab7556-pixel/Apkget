using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace TcpServerApp.Services.Elite.Build
{
    public class Aapt2Compiler
    {
        private readonly Action<string> _logger;

        public Aapt2Compiler(Action<string> logger = null)
        {
            _logger = logger;
        }

        public async Task<string> CompileResourcesAsync(string resDir, string outputDir)
        {
            string outputZip = Path.Combine(outputDir, "compiled_res.zip");
            var args = new[] { "compile", "--dir", resDir, "-o", outputZip };
            await RunAapt2Async(args);
            return outputZip;
        }

        public async Task<string> LinkResourcesAsync(string compiledZip, string manifestPath, string outputDir, string packageName)
        {
            string outputApk = Path.Combine(outputDir, "resources.apk");
            string rJavaDir = Path.Combine(outputDir, "gen"); // Generate R.java
            Directory.CreateDirectory(rJavaDir);

            string androidJar = ToolPathManager.GetAndroidJarPath();
            if (!File.Exists(androidJar)) throw new FileNotFoundException($"Android Jar not found at: {androidJar}");

            var args = new[] 
            { 
                "link", 
                "-o", outputApk, 
                "-I", androidJar, 
                "--manifest", manifestPath, 
                "--java", rJavaDir, 
                "--auto-add-overlay", compiledZip 
            };

            await RunAapt2Async(args);
            return rJavaDir; 
        }

        private async Task RunAapt2Async(string[] args)
        {
            _logger?.Invoke($"[AAPT2 CMD] {ToolPathManager.Aapt2Path} {string.Join(" ", args)}");
            
            var startInfo = new ProcessStartInfo
            {
                FileName = ToolPathManager.Aapt2Path,
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
                throw new Exception($"AAPT2 Failed: {error}\n{output}");
            }
            
            if (!string.IsNullOrWhiteSpace(output)) _logger?.Invoke($"[AAPT2] {output}");
        }
    }
}
