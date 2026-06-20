using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace TcpServerApp.Services.Elite.Build
{
    public class D8Compiler
    {
        private readonly Action<string> _logger;

        public D8Compiler(Action<string> logger = null)
        {
            _logger = logger;
        }

        public async Task<string> CompileDexAsync(string inputPath, string outputDir, bool debug = false)
        {
            string androidJar = ToolPathManager.GetAndroidJarPath();
            
            // java -jar d8.jar --output <output-dir> --lib <android.jar> [--release|--debug] <input-files>
            var args = new System.Collections.Generic.List<string>();
            args.Add("-jar");
            args.Add(ToolPathManager.D8JarPath);
            args.Add(debug ? "--debug" : "--release");
            args.Add("--output");
            args.Add(outputDir);
            args.Add("--lib");
            args.Add(androidJar);
            args.Add(inputPath);

            await RunD8Async(args.ToArray());

            string expectedDex = Path.Combine(outputDir, "classes.dex");
            if (!File.Exists(expectedDex))
            {
                throw new FileNotFoundException("D8 completed but classes.dex was not found.");
            }

            return expectedDex;
        }

        private async Task RunD8Async(string[] args)
        {
            _logger?.Invoke($"[D8 CMD] {ToolPathManager.JavaPath} {string.Join(" ", args)}");

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
                throw new Exception($"D8 Failed: {error}\n{output}");
            }

            if (!string.IsNullOrWhiteSpace(output)) _logger?.Invoke($"[D8] {output}");
            if (!string.IsNullOrWhiteSpace(error)) _logger?.Invoke($"[D8 Logs] {error}");
        }
    }
}
