using System.IO;
using System.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using Microsoft.Extensions.DependencyInjection;
using StickyNotes.Services;
using StickyNotes.Services.Interfaces;
using StickyNotes.Utilities;
using StickyNotes.ViewModels;
using StickyNotes.Views;

namespace StickyNotes;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private SingleInstanceService? _singleInstance;
    private IDesktopPinService? _pinService;
    private ISystemTrayService? _trayService;
    private INoteWindowManager? _windowManager;
    private INoteService? _noteService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1. Ensure data directories exist
        PathHelper.EnsureDirectories();

        // 2. Register crash handlers (three layers)
        RegisterCrashHandlers();

        // 3. Enforce single instance
        _singleInstance = new SingleInstanceService();
        if (!_singleInstance.TryAcquire())
        {
            MessageBox.Show("便签程序已经在运行中。", "StickyNotes",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // 4. Configure DI container
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // 5. Resolve core services
        _pinService = _serviceProvider.GetRequiredService<IDesktopPinService>();
        _trayService = _serviceProvider.GetRequiredService<ISystemTrayService>();
        _windowManager = _serviceProvider.GetRequiredService<INoteWindowManager>();
        _noteService = _serviceProvider.GetRequiredService<INoteService>();

        // 6. Wire desktop-pin service: start listening for system events,
        //    and remount all windows when WorkerW is invalidated.
        _pinService.RemountRequired += (_, _) =>
        {
            SafeExec.Try(() => _windowManager.RemountAll());
        };
        _pinService.StartSystemEventListening();

        // 7. Initialize system tray
        SafeExec.Try(() => _trayService.Initialize());

        // 8. Load persisted notes and open their windows
        SafeExec.Try(() => _windowManager.OpenAll());

        Logger.Info("Application started.");
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Services — singletons for app-wide state
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<INoteService, NoteService>();
        services.AddSingleton<IDesktopPinService, DesktopPinService>();
        services.AddSingleton<INoteWindowManager, NoteWindowManager>();
        services.AddSingleton<ISystemTrayService, SystemTrayService>();

        // ViewModels — transient (created per window)
        services.AddTransient<NoteViewModel>();
        services.AddTransient<SettingsViewModel>();

        // Windows — transient (settings window created on demand)
        services.AddTransient<SettingsWindow>();
    }

    private void RegisterCrashHandlers()
    {
        // WPF UI thread exceptions
        DispatcherUnhandledException += (_, e) =>
        {
            LogCrash("DispatcherUnhandledException", e.Exception);
            e.Handled = true;
        };

        // CLR unhandled exceptions (all threads)
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LogCrash("AppDomain.UnhandledException", e.ExceptionObject as Exception);

        // Unobserved async Task exceptions
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogCrash("TaskScheduler.UnobservedTaskException", e.Exception);
            e.SetObserved();
        };
    }

    private static void LogCrash(string source, Exception? ex)
    {
        try
        {
            Directory.CreateDirectory(PathHelper.LocalAppDataDir);
            var msg = $"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {source}\n{ex}\n";
            File.AppendAllText(PathHelper.CrashLogFilePath, msg);
        }
        catch
        {
            // Crash logging must never throw
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Logger.Info("Application exiting.");

        // Flush any pending note saves to disk
        SafeExec.Try(() => _noteService?.Save());

        // Dispose services in reverse order of initialization
        SafeExec.Try(() => _trayService?.Dispose());
        SafeExec.Try(() => _pinService?.Dispose());
        SafeExec.Try(() => _singleInstance?.Dispose());
        SafeExec.Try(() => (_serviceProvider as IDisposable)?.Dispose());

        base.OnExit(e);
    }
}
