using System.Configuration;
using System.Data;

namespace TcpServerApp;

using System;
using System.Threading.Tasks;
using System.Windows;
using TcpServerApp.Models;
using TcpServerApp.Services.Elite;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    public static TcpServerApp.Services.Elite.EliteServerManager ServerManager { get; private set; } // Elite Research Core
    public static TcpServerApp.Services.Elite.Networking.EliteNetworkListener ResearchListener { get; private set; } // Advanced Android 16 Research Listener
    public static EliteDB EliteDb { get; private set; } = new();
    public static ClientRepository ClientRepository { get; private set; } = new(EliteDb); // Elite Research Core
    public static TopologySingularityEngine TopologyEngine { get; private set; } = new();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1. Splash Screen or Heavy Lifting? We do it mostly async
        // Verify/Refine Android 36 Tools
        await TcpServerApp.Services.Elite.ToolsRefinery.EnsureAndroid36JarExistsAsync();

        ServerManager = new TcpServerApp.Services.Elite.EliteServerManager();
        
        // Initialize Research Listener with shared repository access if possible, or new one
        var db = new TcpServerApp.Models.EliteDB();
        var repo = new TcpServerApp.Models.ClientRepository(db);
        ResearchListener = new TcpServerApp.Services.Elite.Networking.EliteNetworkListener(repo);
        ResearchListener.SetTopologyEngine(TopologyEngine);

        // Capture unhandled exceptions to prevent sudden app exit
        this.DispatcherUnhandledException += (s, exArgs) =>
        {
            try
            {
                System.Windows.MessageBox.Show(
                    exArgs.Exception.Message,
                    "حدث خطأ غير متوقع",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch { }
            // Prevent application from crashing where possible
            exArgs.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, exArgs) =>
        {
            var ex = exArgs.ExceptionObject as Exception;
            try
            {
                System.Windows.MessageBox.Show(
                    ex?.Message ?? "Unhandled exception",
                    "حدث خطأ غير متوقع (Domain)",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch { }
        };

        TaskScheduler.UnobservedTaskException += (s, exArgs) =>
        {
            try
            {
                System.Windows.MessageBox.Show(
                    exArgs.Exception?.Message ?? "Unobserved task exception",
                    "حدث خطأ في مهمة غير مراقبة",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch { }
            exArgs.SetObserved();
        };
    }
}

