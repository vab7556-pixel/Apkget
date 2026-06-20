using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace TcpServerApp.Services.Elite
{
    public class AppCrawlerService
    {
        public event Action<string> OnLog;

        // Default Install Button Coordinates (Calibration required for different densities)
        // For Pixel 6 Pro (1440x3120) - Example
        // Install Button is roughly center-right or full width top
        // Start with generic safe coordinates or allow UI calibration
        private int _installX = 720; 
        private int _installY = 900; 

        public void UpdateCalibration(int x, int y)
        {
            _installX = x;
            _installY = y;
        }

        public async Task CrawlAndInstallAsync(string packageName)
        {
            Log($"🕷️ Starting Crawler for: {packageName}");

            // 0. Pre-Flight Checks 🛫
            if (!await CheckInternetConnectivity())
            {
                Log("❌ No Internet Connection on Emulator! Aborting.");
                return;
            }

            // 1. Force Stop Play Store (Clean State)
            await RunAdbCommand($"shell am force-stop com.android.vending");
            await Task.Delay(1000);

            // 2. Open App Page via Intent
            // market://details?id=pkg
            Log($"🔗 Opening Store URI: market://details?id={packageName}");
            await RunAdbCommand($"shell am start -a android.intent.action.VIEW -d \"market://details?id={packageName}\"");
            
            // Wait for UI load (Network dependent)
            Log("⏳ Waiting for Store UI (5s)...");
            await Task.Delay(5000);

            // 3. Simulate Click on "Install"
            Log($"👆 Tapping Install Button at ({_installX}, {_installY})...");
            await RunAdbCommand($"shell input tap {_installX} {_installY}");
            
            // 4. Smart Polling for Installation (Max 60s)
            Log("⏳ Polling for Installation...");
            if (await PollForPackage(packageName, 60))
            {
                Log($"✅ SUCCESS: {packageName} successfully installed!");
                
                // Optional: Launch it
                Log("🚀 Launching App for Analysis...");
                await RunAdbCommand($"shell monkey -p {packageName} -c android.intent.category.LAUNCHER 1");
            }
            else
            {
                Log($"❌ TIMEOUT: {packageName} was not installed within 60s. Check login/coordinates.");
                // Diagnostic dump
                var bounds = await RunAdbCommand("shell uiautomator dump /sdcard/dump.xml && cat /sdcard/dump.xml");
                Log($"[Diagnostic] UI Dump saved to logs.");
            }
        }

        private async Task<bool> CheckInternetConnectivity()
        {
            string ping = await RunAdbCommand("shell ping -c 1 google.com");
            return ping.Contains("bytes from");
        }

        private async Task<bool> PollForPackage(string pkg, int timeoutSeconds)
        {
            for(int i=0; i<timeoutSeconds; i++)
            {
                 if (await CheckIfInstalled(pkg)) return true;
                 await Task.Delay(1000);
            }
            return false;
        }

        private async Task<bool> CheckIfInstalled(string pkg)
        {
            string res = await RunAdbCommand($"shell pm list packages {pkg}");
            return res.Contains($"package:{pkg}");
        }

        private async Task<string> RunAdbCommand(string args)
        {
            try
            {
                var adb = ToolLocator.AdbPath;
                var psi = new ProcessStartInfo
                {
                    FileName = adb,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc == null) return "";

                string output = await proc.StandardOutput.ReadToEndAsync();
                await proc.WaitForExitAsync();
                return output.Trim();
            }
            catch (Exception ex)
            {
                Log($"Error: {ex.Message}");
                return "";
            }
        }

        private void Log(string msg)
        {
            OnLog?.Invoke($"[{DateTime.Now:HH:mm:ss}] {msg}");
        }
    }
}
