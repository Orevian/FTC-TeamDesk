using System.Net.Http;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using FTC.TeamDesk.AI;
using FTC.TeamDesk.AI.Providers;
using FTC.TeamDesk.AI.Tools;
using FTC.TeamDesk.Core.Abstractions.Repositories;
using FTC.TeamDesk.Core.Abstractions.Services;
using FTC.TeamDesk.Core.Common;
using FTC.TeamDesk.Data;
using FTC.TeamDesk.Infrastructure;
using FTC.TeamDesk.Localization;
using FTC.TeamDesk.Security;
using FTC.TeamDesk.Services;
using FTC.TeamDesk.Services.Google;
using FTC.TeamDesk.Services.Logging;
using FTC.TeamDesk.UI;
using FTC.TeamDesk.UI.Abstractions;
using FTC.TeamDesk.UI.Localization;
using FTC.TeamDesk.UI.Services;
using FTC.TeamDesk.UI.ViewModels.Settings;
using FTC.TeamDesk.UI.ViewModels.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace FTC.TeamDesk;

/// <summary>Composition root: builds the DI container, initializes storage, then shows the main window.</summary>
public partial class App : Application
{
    private Mutex? _singleInstance;
    private ServiceProvider? _services;
    private IAppLogger? _logger;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new Mutex(true, @"Local\FTC.TeamDesk.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            MessageBox.Show("FTC TeamDesk is already running.\nFTC TeamDesk zaten çalışıyor.", "FTC TeamDesk", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherException;
        TaskScheduler.UnobservedTaskException += (_, args) => { _logger?.Error("Unobserved task exception.", args.Exception); args.SetObserved(); };
        AppDomain.CurrentDomain.UnhandledException += (_, args) => _logger?.Error("Fatal unhandled exception.", args.ExceptionObject as Exception);

        try
        {
            var paths = new AppPaths();
            _logger = new FileAppLogger(paths.Logs);
            Diag.Sink = _logger.Info;
            StartUiWatchdog();

            // Preferences are plain JSON (no secrets) and are read first: they decide language, theme and database location.
            var prefs = SettingsService.ReadFile(paths.Preferences, _logger);
            var language = LocalizationService.ResolveInitial(prefs.Language);
            var dbPath = string.IsNullOrWhiteSpace(prefs.DatabasePath) ? paths.DefaultDatabase : prefs.DatabasePath!;

            _services = BuildServices(paths, dbPath, language, _logger);
            // Create UI-thread-bound singletons now, while still on the UI thread (they capture its context).
            _ = _services.GetRequiredService<ToastService>();

            await _services.GetRequiredService<DatabaseInitializer>().InitializeAsync();
            await _services.GetRequiredService<ISettingsService>().LoadAsync();

            LocalizationSource.Initialize(_services.GetRequiredService<ILocalizationService>());
            var theme = _services.GetRequiredService<ThemeService>();
            theme.Apply(prefs.Theme);

            var window = ActivatorUtilities.CreateInstance<MainWindow>(_services);
            var vm = _services.GetRequiredService<MainViewModel>();
            window.DataContext = vm;
            MainWindow = window;
            window.Show();

            SystemEvents.SessionSwitch += OnSessionSwitch;
            await vm.StartAsync();
        }
        catch (Exception ex)
        {
            _logger?.Error("Start-up failed.", ex);
            MessageBox.Show($"FTC TeamDesk could not start.\nUygulama başlatılamadı.\n\n{ex.GetType().Name}", "FTC TeamDesk", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static ServiceProvider BuildServices(AppPaths paths, string dbPath, string language, IAppLogger logger)
    {
        var services = new ServiceCollection();

        // --- Infrastructure -----------------------------------------------------------------
        services.AddSingleton(paths);
        services.AddSingleton(logger);
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ILocalizationService>(_ => new LocalizationService(language));
        services.AddSingleton<ISecretStore>(_ => new DpapiSecretStore(paths.Secrets));
        services.AddSingleton<IVaultCrypto, VaultCrypto>();
        services.AddHttpClient("default", c =>
        {
            c.Timeout = TimeSpan.FromSeconds(120);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("FTC-TeamDesk/1.0");
        });
        static HttpClient Http(IServiceProvider sp) => sp.GetRequiredService<IHttpClientFactory>().CreateClient("default");

        // --- Data ---------------------------------------------------------------------------
        services.AddTeamDeskData(dbPath);

        // --- Domain services ----------------------------------------------------------------
        services.AddSingleton<ISettingsService>(sp => new SettingsService(paths.Preferences, sp.GetRequiredService<IAppSettingsRepository>(), sp.GetRequiredService<IAppLogger>()));
        services.AddOffThread<IActivityLogService, ActivityLogService>();
        services.AddOffThread<IMemberService, MemberService>();
        services.AddOffThread<IApplicationService, ApplicationService>();
        services.AddOffThread<IBudgetService, BudgetService>();
        services.AddOffThread<ITaskService, TaskService>();
        services.AddOffThread<IDashboardService, DashboardService>();
        services.AddOffThread<IAnalyticsService, AnalyticsService>();
        services.AddSingleton<IVaultService, VaultService>();
        services.AddOffThread<IBackupService, BackupService>();
        services.AddSingleton(sp => new GoogleAuthService(Http(sp), sp.GetRequiredService<ISecretStore>(), sp.GetRequiredService<ISettingsService>(), sp.GetRequiredService<IAppLogger>()));
        services.AddSingleton(sp => new GoogleSheetsClient(Http(sp), sp.GetRequiredService<GoogleAuthService>()));
        services.AddSingleton<IGoogleSheetsService, GoogleSheetsService>();

        // --- AI -----------------------------------------------------------------------------
        services.AddSingleton<IModelCatalog>(sp => new ModelCatalog(Http(sp)));
        services.AddSingleton<IAiClientFactory>(sp => new AiClientFactory(Http(sp), sp.GetRequiredService<ISecretStore>(), sp.GetRequiredService<IModelCatalog>()));
        services.AddSingleton<AiToolRegistry>();
        services.AddSingleton<IAssistantService, AssistantService>();

        // --- UI services --------------------------------------------------------------------
        services.AddSingleton<DisplayFormatter>();
        services.AddSingleton<ToastService>();
        services.AddSingleton<IToastService>(sp => sp.GetRequiredService<ToastService>());
        services.AddSingleton(sp => new ErrorPresenter(sp.GetRequiredService<ILocalizationService>(), sp.GetRequiredService<IAppLogger>(), () => sp.GetRequiredService<IToastService>()));
        services.AddSingleton<IErrorPresenter>(sp => sp.GetRequiredService<ErrorPresenter>());
        services.AddSingleton(sp => new DialogService(() => sp.GetRequiredService<UiContext>()));
        services.AddSingleton<IDialogService>(sp => sp.GetRequiredService<DialogService>());
        services.AddSingleton<UiContext>();
        services.AddSingleton<IViewModelFactory>(sp => new ViewModelFactory(sp));
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<IThemeService>(sp => sp.GetRequiredService<ThemeService>());
        services.AddSingleton<ISecureClipboard, SecureClipboard>();
        services.AddSingleton<IFileDialogService, FileDialogService>();
        services.AddSingleton<IAppLifecycle, AppLifecycle>();
        services.AddSingleton<IAvatarStore>(_ => new AvatarStore(paths.Root));

        // --- View models --------------------------------------------------------------------
        services.AddSingleton<MainViewModel>();
        // Settings tabs are resolved by the container; page view models are created on navigation via ViewModelFactory.
        services.AddTransient<GeneralSettingsViewModel>();
        services.AddTransient<AiSettingsViewModel>();
        services.AddTransient<GoogleSettingsViewModel>();
        services.AddTransient<SecuritySettingsViewModel>();
        services.AddTransient<DataSettingsViewModel>();
        services.AddTransient<AboutViewModel>();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = false, ValidateScopes = false });
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason != SessionSwitchReason.SessionLock || _services is null) return;
        var settings = _services.GetRequiredService<ISettingsService>();
        if (settings.Current.LockVaultOnSessionLock) _services.GetRequiredService<IVaultService>().Lock("session-lock");
    }

    private int _errorBurst;
    private long _burstWindowStart;
    private bool _fatalShown;

    private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Never crash on an unexpected error: log it (secrets are redacted by the logger) and tell the user in a friendly way.
        _logger?.Error("Unhandled UI exception.", e.Exception);
        e.Handled = true;

        // An exception thrown while rendering/layout repeats on every frame; without this breaker that looks like a frozen app.
        var now = Environment.TickCount64;
        if (now - _burstWindowStart > 2000) { _burstWindowStart = now; _errorBurst = 0; }
        if (++_errorBurst > 20)
        {
            if (_fatalShown) return;
            _fatalShown = true;
            _logger?.Error("Repeating UI exception loop detected; shutting down. Recent steps: " + Diag.Recent());
            MessageBox.Show("FTC TeamDesk hit a repeating error and will close. Details are in the log folder.\nFTC TeamDesk tekrarlayan bir hatayla karşılaştı ve kapanacak. Ayrıntılar log klasöründe.",
                "FTC TeamDesk", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(2);
            return;
        }
        if (_errorBurst == 1) { try { _services?.GetService<IErrorPresenter>()?.Show(e.Exception); } catch (Exception) { } }
    }

    // --- UI hang watchdog: a heartbeat on the UI thread, checked from a background thread. If the UI stops answering, the log gets the
    // last breadcrumbs (what the app was doing), which pinpoints a freeze without a debugger.
    private long _lastBeat = Environment.TickCount64;
    private DispatcherTimer? _beatTimer;

    private void StartUiWatchdog()
    {
        _beatTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _beatTimer.Tick += (_, _) => Interlocked.Exchange(ref _lastBeat, Environment.TickCount64);
        _beatTimer.Start();
        var thread = new Thread(() =>
        {
            var reported = false;
            while (true)
            {
                Thread.Sleep(1000);
                var silent = Environment.TickCount64 - Interlocked.Read(ref _lastBeat);
                if (silent > 5000 && !reported)
                {
                    reported = true;
                    _logger?.Error($"UI thread not responding for {silent / 1000}s. Recent steps: {Diag.Recent()}");
                }
                else if (silent < 1500 && reported)
                {
                    reported = false;
                    _logger?.Info("UI thread responsive again.");
                }
            }
        }) { IsBackground = true, Name = "UiWatchdog" };
        thread.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        (_services?.GetService<IVaultService>() as IDisposable)?.Dispose();
        _services?.Dispose();
        _singleInstance?.ReleaseMutex();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}

internal static class OffThreadRegistration
{
    /// <summary>Registers <typeparamref name="TImpl"/> as a singleton whose Task-returning calls run off the UI thread.</summary>
    public static IServiceCollection AddOffThread<TService, TImpl>(this IServiceCollection services)
        where TService : class where TImpl : class, TService
        => services.AddSingleton<TService>(sp => OffThreadProxy.Wrap<TService>(ActivatorUtilities.CreateInstance<TImpl>(sp)));
}
