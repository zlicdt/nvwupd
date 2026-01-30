using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NvwUpd.Services;
using NvwUpd.Core;
using NvwUpd.ViewModels;
using System.Diagnostics;

namespace NvwUpd;

/// <summary>
/// Main application class for NVIDIA Driver Updater.
/// </summary>
public partial class App : Application
{
    private Window? _window;
    private static StreamWriter? _logWriter;

    static App()
    {
        // Setup console logging to file
        var logPath = Path.Combine(AppContext.BaseDirectory, "debug.log");
        _logWriter = new StreamWriter(logPath, append: false) { AutoFlush = true };
        Console.SetOut(_logWriter);
        Console.WriteLine($"[App] Log started at {DateTime.Now}");
    }
    private readonly IHost _host;

    public static IServiceProvider Services => ((App)Current)._host.Services;

    public App()
    {
        InitializeComponent();

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Core services
                services.AddSingleton<IGpuDetector, GpuDetector>();
                services.AddSingleton<IDriverFetcher, DriverFetcher>();
                services.AddSingleton<IDriverDownloader, DriverDownloader>();
                services.AddSingleton<IDriverInstaller, DriverInstaller>();

                // Application services
                services.AddSingleton<INotificationService, NotificationService>();
                services.AddSingleton<IUpdateChecker, UpdateChecker>();

                // ViewModels
                services.AddTransient<MainViewModel>();
                services.AddTransient<UpdateDialogViewModel>();
            })
            .Build();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }

    public static Window? MainWindow => ((App)Current)._window;
}
