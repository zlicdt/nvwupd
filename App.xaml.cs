using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NvwUpd.Services;
using NvwUpd.Core;
using NvwUpd.ViewModels;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Microsoft.UI.Windowing;

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
        // Apply --lang override before anything else
        ApplyLanguageOverride();

        // Setup console logging to file
        var logPath = Path.Combine(AppContext.BaseDirectory, "debug.log");
        _logWriter = new StreamWriter(logPath, append: false) { AutoFlush = true };
        Console.SetOut(_logWriter);
        Console.WriteLine($"[App] Log started at {DateTime.Now}");
        Console.WriteLine($"[App] CurrentUICulture: {CultureInfo.CurrentUICulture.Name}");
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
                services.AddSingleton<ISettingsService, SettingsService>();
                services.AddSingleton<IStartupService, StartupService>();

                // ViewModels
                services.AddTransient<MainViewModel>();
                services.AddTransient<UpdateDialogViewModel>();
            })
            .Build();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var isBackground = IsBackgroundLaunch(args.Arguments);

        _window = new MainWindow();

        if (isBackground)
        {
            _window.AppWindow.Hide();
            return;
        }

        _window.Activate();
    }

    public static Window? MainWindow => ((App)Current)._window;

    public static void ShowMainWindow()
    {
        var app = (App)Current;

        if (app._window == null)
        {
            app._window = new MainWindow();
        }
        var window = app._window;
        var dispatcher = window.DispatcherQueue;
        if (dispatcher != null && dispatcher.TryEnqueue(() =>
            {
                window.AppWindow.Show();
                window.Activate();
            }))
        {
            return;
        }

        window.AppWindow.Show();
        window.Activate();
    }

    private static bool IsBackgroundLaunch(string? arguments)
    {
        if (!string.IsNullOrWhiteSpace(arguments))
        {
            return arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Any(arg => string.Equals(arg, "--background", StringComparison.OrdinalIgnoreCase));
        }

        var cliArgs = Environment.GetCommandLineArgs();
        return cliArgs.Any(arg => string.Equals(arg, "--background", StringComparison.OrdinalIgnoreCase));
    }

    private static void ApplyLanguageOverride()
    {
        var cliArgs = Environment.GetCommandLineArgs();
        for (int i = 0; i < cliArgs.Length - 1; i++)
        {
            if (string.Equals(cliArgs[i], "--lang", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var culture = new CultureInfo(cliArgs[i + 1]);
                    CultureInfo.CurrentUICulture = culture;
                    CultureInfo.CurrentCulture = culture;
                    CultureInfo.DefaultThreadCurrentUICulture = culture;
                    CultureInfo.DefaultThreadCurrentCulture = culture;
                }
                catch { }
                break;
            }
        }
    }
}
