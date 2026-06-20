using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace TcpServerApp.Services.Elite.Build
{
    public class JavacCompiler
    {
        private readonly Action<string> _logger;

        public JavacCompiler(Action<string> logger = null)
        {
            _logger = logger;
        }

        public async Task<string> CompileAsync(string sourceDir, string outputDir)
        {
            _logger?.Invoke($"[Javac] Scanning sources in: {sourceDir}");

            // 1. Collect all .java files
            var files = Directory.GetFiles(sourceDir, "*.java", SearchOption.AllDirectories);
            if (!files.Any()) throw new FileNotFoundException("No .java files found to compile.");

            // 2. Prepare Output Class Dir
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            // 3. Prepare Arguments
            string sourcesListPath = Path.Combine(outputDir, "sources.txt");
            await File.WriteAllLinesAsync(sourcesListPath, files);

            string androidJar = ToolPathManager.GetAndroidJarPath();
            
            // javac -cp android.jar -d outputDir @sources.txt
            var args = new[] 
            { 
                "-cp", androidJar, 
                "-d", outputDir, 
                $"@{sourcesListPath}" 
            };

            await RunJavacAsync(args);

            return outputDir;
        }

        private async Task RunJavacAsync(string[] args)
        {
             var startInfo = new ProcessStartInfo
            {
                FileName = ToolPathManager.JavacPath,
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
                throw new Exception($"Javac Failed: {error}\n{output}");
            }

            if (!string.IsNullOrWhiteSpace(output)) _logger?.Invoke($"[Javac] {output}");
            // Javac often chats on stderr
            if (!string.IsNullOrWhiteSpace(error) && !error.Contains("error")) _logger?.Invoke($"[Javac Warn] {error}");
        }
    }
}
