using System.Runtime.InteropServices;
using System.Windows;
using DryIoc;
using DryIoc.Microsoft.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using ModernThinkPadLEDsController.Hardware;
using ModernThinkPadLEDsController.Lighting;
using ModernThinkPadLEDsController.Logging;
using ModernThinkPadLEDsController.Monitoring;
using ModernThinkPadLEDsController.Presentation.Services;
using ModernThinkPadLEDsController.Presentation.ViewModels;
using ModernThinkPadLEDsController.Presentation.Views;
using ModernThinkPadLEDsController.Runtime;
using ModernThinkPadLEDsController.Settings;
using ModernThinkPadLEDsController.Shell;
using Serilog;
using Wpf.Ui.Appearance;

namespace ModernThinkPadLEDsController;

/// <summary>Root app manager.</summary>
public partial class App : Application
{
    private static readonly string[] _safeModeArguments = ["--safe-mode", "--no-hardware"];

    [LibraryImport("user32.dll", EntryPoint = "SetForegroundWindow", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr hWnd);

    [LibraryImport("user32.dll", EntryPoint = "ShowWindow", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [LibraryImport("user32.dll", EntryPoint = "IsIconic", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsIconic(IntPtr hWnd);

    [LibraryImport("user32.dll", EntryPoint = "FindWindowW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr FindWindow(string? lpClassName, string? lpWindowName);

#pragma warning disable IDE1006 // Naming Styles
    private const int _SW_RESTORE = 9;
#pragma warning restore IDE1006 // Naming Styles

    private IServiceProvider? _serviceProvider;
    private ILogger<App>? _logger;
    private ApplicationCoordinator? _app;
    private SettingsPersistenceService? _settingsPersistence;
    private ApplicationExceptionCoordinator? _exceptionCoordinator;
    private ResourceDictionary? _themeResourceDictionary;

    private Mutex? _singleInstanceMutex;
    private bool _mutexOwned;

    protected override void OnStartup(StartupEventArgs e)
    {
        StartupEmergencyLogger emergencyLogger = new StartupEmergencyLogger("App.OnStartup");

        try
        {
            emergencyLogger.Log("=== OnStartup() called ===");
            emergencyLogger.Log($"Args: {string.Join(" ", e.Args)}");

            // ══════════════════════════════════════════════════════════════
            // CRITICAL: Load settings FIRST to get log level preference
            // Then initialize logging with the saved level
            // ══════════════════════════════════════════════════════════════
            emergencyLogger.Log("Loading app settings...");
            AppSettings appSettings = AppSettings.Load();
            emergencyLogger.Log($"Settings loaded. Log level: {appSettings.LogLevel}");

            emergencyLogger.Log("Calling ConfigureSerilog()...");
            LoggingConfiguration.ConfigureSerilog(appSettings.LogLevel);
            emergencyLogger.Log("ConfigureSerilog() completed");

            // Set up theme change handlers
            ApplicationThemeManager.Changed += OnApplicationThemeChanged;
            SystemEvents.UserPreferenceChanged += OnSystemUserPreferenceChanged;

            base.OnStartup(e);
            emergencyLogger.Log("base.OnStartup() completed");

            // Apply system theme and load theme-specific resource overrides
            ApplySystemTheme();
            emergencyLogger.Log("Applied system theme");

            // Build dependency injection container
            _serviceProvider = CreateServiceProvider(e.Args);
            _logger = _serviceProvider.GetRequiredService<ILogger<App>>();
            _exceptionCoordinator = new ApplicationExceptionCoordinator(
                Dispatcher,
                () => _logger,
                DisposeApplicationResources,
                Shutdown);
            HardwareAccessController hardwareAccess = _serviceProvider.GetRequiredService<HardwareAccessController>();

            _logger.LogDebug("Application startup initiated");
            _logger.LogDebug("Command line arguments: {Args}", string.Join(" ", e.Args));

#if DEBUG
            _logger.LogWarning("Running in DEBUG configuration - memory usage will be 30-50% higher than Release");
#else
            _logger.LogInformation("Running in RELEASE configuration");
#endif

            _logger.LogInformation("Hardware access status: {Status}", hardwareAccess.GetStatusDescription());
            LoggingConfiguration.LogEnvironmentDetails();

            // Global exception handlers to prevent silent crashes
            DispatcherUnhandledException += _exceptionCoordinator.OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += _exceptionCoordinator.OnUnhandledException;
            TaskScheduler.UnobservedTaskException += _exceptionCoordinator.OnUnobservedTaskException;

            _logger.LogDebug("Exception handlers registered");

            if (!TryInitializeSingleInstance())
            {
                return; // Another instance is already running, we can kill the current one
            }

            try
            {
                ResolveServices();
                _app!.ExitRequested += RequestExit;
            }
            catch (LhmDriverInitializationException ex)
            {
                _logger.LogError(ex, "Failed to initialize driver - showing PawnIO setup window");
                PawnIOSetupWindow setup = new PawnIOSetupWindow();
                setup.ShowDialog();
                Shutdown();
                return;
            }

            _app.Initialize();
            _app.EnsureMainWindowHandle();

            bool startMinimized = e.Args.Contains("--minimized");
            _app.Start(startMinimized);

            _logger.LogInformation("Application startup completed successfully");
            emergencyLogger.Log("=== OnStartup() completed successfully ===");
        }
        catch (Exception ex)
        {
            emergencyLogger.LogException("FATAL EXCEPTION in OnStartup()", ex);

            Log.Fatal(ex, "Fatal error during application startup");

            string message = $"Fatal startup error:\n\n{ex.GetType().Name}: {ex.Message}\n\n" +
                         $"Stack Trace:\n{ex.StackTrace}\n\n" +
                         $"See logs at: {StartupEmergencyLogger.LogDirectory}\emergency.log";

            MessageBox.Show(message, "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);

            LoggingConfiguration.CloseAndFlush();
            Shutdown();
        }
    }

    /// <summary>
    /// Creates the dependency injection container for the application.
    /// This configures all services and logging infrastructure.
    /// </summary>
    private static IServiceProvider CreateServiceProvider(string[] args)
    {
        Container container = new Container(rules => rules
            .WithoutThrowOnRegisteringDisposableTransient()
            .WithTrackingDisposableTransients());

        ServiceCollection services = new();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(dispose: false); // Serilog already initialized in OnStartup
        });

        AppSettings settings = AppSettings.Load();

        bool startedInSafeMode = args.Any(IsSafeModeArgument);
        bool startWithHardwareAccess = settings.EnableHardwareAccess && !startedInSafeMode;
        string? startupReason = null;

        if (!startWithHardwareAccess)
        {
            startupReason = startedInSafeMode
                ? "Started with --safe-mode."
                : "Disabled in app settings.";
        }

        services.AddSingleton(settings);
        services.AddSingleton(new HardwareAccessController(
            isEnabled: startWithHardwareAccess,
            driverLoaded: startWithHardwareAccess,
            startupReason: startupReason));

        // Hardware layer - driver must be validated before use
        if (startWithHardwareAccess)
        {
            services.AddSingleton<LhmDriver>(__ =>
            {
                if (!LhmDriver.TryOpen(out LhmDriver? driver) || driver is null)
                {
                    throw new LhmDriverInitializationException("Failed to initialize LHM driver");
                }

                return driver;
            });

            // Register LhmDriver as IPortIO so EcController can resolve it
            services.AddSingleton<IPortIO>(sp => sp.GetRequiredService<LhmDriver>());
        }
        else
        {
            services.AddSingleton<IPortIO, NoOpPortIO>();
        }

        services.AddSingleton<EcController>();
        services.AddSingleton<LedController>();
        services.AddSingleton<HotkeyService>();
        services.AddSingleton<LedBehaviorService>();
        services.AddSingleton<ISettingsRuntimeService, SettingsRuntimeService>();

        // Monitoring services
        services.AddSingleton<DiskActivityMonitor>(sp =>
        {
            AppSettings settings = sp.GetRequiredService<AppSettings>();
            DiskActivityMonitor monitor = new DiskActivityMonitor(settings.DiskPollIntervalMs);
            monitor.TryInitialize(); // Initialize on creation
            return monitor;
        });
        services.AddSingleton<KeyboardBacklightMonitor>();
        services.AddSingleton<MicrophoneMuteMonitor>();
        services.AddSingleton<SpeakerMuteMonitor>();
        services.AddSingleton<PowerEventMonitor>();
        services.AddSingleton<FullscreenMonitor>();

        // UI services
        services.AddSingleton<TrayIconService>(__ =>
        {
            TrayIconService tray = new TrayIconService();
            tray.Initialize();
            return tray;
        });

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<SettingsViewModel>();

        // Main window
        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainWindowHost>();
        services.AddSingleton<IUiDispatcher>(sp => sp.GetRequiredService<MainWindowHost>());
        services.AddSingleton<MainPresentationService>();
        services.AddSingleton<SettingsPersistenceService>();
        services.AddSingleton<HotkeyConfigurationService>();
        services.AddSingleton<ShellCoordinator>();
        services.AddSingleton<HardwareRuntimeCoordinator>();
        services.AddSingleton<ApplicationCoordinator>();

        // Build and return service provider
        return container.WithDependencyInjectionAdapter(services).BuildServiceProvider();
    }

    private static bool IsSafeModeArgument(string arg)
    {
        return _safeModeArguments.Any(safeModeArg => string.Equals(arg, safeModeArg, StringComparison.OrdinalIgnoreCase));
    }

    private void OnApplicationThemeChanged(ApplicationTheme theme, System.Windows.Media.Color accent)
    {
        _ = accent;
        Dispatcher.BeginInvoke(() => LoadThemeSpecificResources(theme));
    }

    private void OnSystemUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        // Detect Windows theme/color changes and re-apply system theme
        if (e.Category == UserPreferenceCategory.General ||
            e.Category == UserPreferenceCategory.Color)
        {
            Dispatcher.BeginInvoke(() =>
            {
                _logger?.LogDebug("Windows theme preference changed, re-applying system theme");
                ApplySystemTheme();
            });
        }
    }

    private void ApplySystemTheme()
    {
        // Apply system theme (dark/light based on Windows settings)
        ApplicationThemeManager.ApplySystemTheme();

        // Apply system accent color
        ApplicationTheme currentTheme = ApplicationThemeManager.GetAppTheme();
        ApplicationAccentColorManager.Apply(
            ApplicationAccentColorManager.GetColorizationColor(),
            currentTheme,
            true,
            true);

        // Load theme-specific resource overrides
        LoadThemeSpecificResources(currentTheme);
    }

    private void LoadThemeSpecificResources(ApplicationTheme theme)
    {
        // Remove previous theme dictionary if it exists
        if (_themeResourceDictionary != null)
        {
            Resources.MergedDictionaries.Remove(_themeResourceDictionary);
        }

        // Load appropriate theme resource dictionary
        string themeFile = theme == ApplicationTheme.Light ? "LightTheme.xaml" : "DarkTheme.xaml";
        _themeResourceDictionary = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Presentation/Resources/{themeFile}", UriKind.Absolute)
        };

        Resources.MergedDictionaries.Add(_themeResourceDictionary);

        // Populate theme-specific brushes from WPF-UI resources
        // XAML doesn't support resource aliasing, so we copy the brush references here
        string inputBackgroundKey = theme == ApplicationTheme.Light
            ? "ControlFillColorSecondaryBrush"
            : "ControlFillColorDefaultBrush";
        CopyBrushResource(inputBackgroundKey, "AppInputBackgroundBrush");

        // Sync CheckBox styling with RadioButton for visual consistency
        CopyBrushResource("RadioButtonOuterEllipseCheckedStroke", "CheckBoxCheckBackgroundFillChecked");
        CopyBrushResource("RadioButtonOuterEllipseCheckedStrokePointerOver", "CheckBoxCheckBackgroundFillCheckedPointerOver");
        CopyBrushResource("RadioButtonOuterEllipseCheckedStrokePointerOver", "CheckBoxCheckBackgroundFillCheckedPressed");
        CopyBrushResource("RadioButtonOuterEllipseCheckedStroke", "CheckBoxCheckBorderBrush");
        CopyBrushResource("RadioButtonCheckGlyphFill", "CheckBoxCheckGlyphForeground");

        _logger?.LogDebug("Loaded theme-specific resources: {ThemeFile}", themeFile);
    }

    private void CopyBrushResource(string sourceKey, string targetKey)
    {
        if (_themeResourceDictionary != null && TryFindResource(sourceKey) is System.Windows.Media.Brush sourceBrush)
        {
            _themeResourceDictionary[targetKey] = sourceBrush;
        }
    }

    private bool TryInitializeSingleInstance()
    {
        _logger?.LogDebug("Checking for existing application instance");

        _singleInstanceMutex = new Mutex(
            initiallyOwned: true,
            name: "ModernThinkPadLEDsController_SingleInstance",
            out bool isFirstInstance);

        _mutexOwned = isFirstInstance;

        if (!isFirstInstance)
        {
            _logger?.LogWarning("Another instance is already running - attempting to activate it");

            // Try to find and activate the existing window
            if (TryActivateExistingInstance())
            {
                _logger?.LogInformation("Successfully activated existing instance window");
            }
            else
            {
                _logger?.LogWarning("Could not find/activate existing window - showing message");
                MessageBox.Show(
                    "Modern ThinkPad LEDs Controller is already running.\n\nLook for its icon in the system tray.",
                    "Already Running",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            Shutdown();
            return false;
        }

        _logger?.LogInformation("Single instance check passed - no other instance detected");
        return true;
    }

    /// <summary>
    /// Attempts to find and activate the main window of an existing application instance.
    /// </summary>
    /// <returns>True if the window was found and activated; otherwise false.</returns>
    private bool TryActivateExistingInstance()
    {
        try
        {
            // Try to find the window by its title
            // MainWindow's title is "Modern ThinkPad LEDs Controller"
            IntPtr hWnd = FindWindow(null, "Modern ThinkPad LEDs Controller");

            if (hWnd == IntPtr.Zero)
            {
                _logger?.LogDebug("Could not find existing window by title");
                return false;
            }

            _logger?.LogDebug("Found existing window handle: {Handle}", hWnd);

            if (IsIconic(hWnd))
            {
                _logger?.LogDebug("Window is minimized - restoring");
            }

            // Restore also covers the tray-hidden case better than only checking iconic state.
            ShowWindow(hWnd, _SW_RESTORE);

            // Bring the window to the foreground
            bool success = SetForegroundWindow(hWnd);
            _logger?.LogDebug("SetForegroundWindow result: {Success}", success);

            return success;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error while trying to activate existing instance");
            return false;
        }
    }

    /// <summary>
    /// Resolve all services from the DI container.
    /// </summary>
    private void ResolveServices()
    {
        _logger?.LogInformation("Resolving services from DI container");

        // Resolve all services - this triggers construction and validation
        _app = _serviceProvider!.GetRequiredService<ApplicationCoordinator>();
        _settingsPersistence = _serviceProvider!.GetRequiredService<SettingsPersistenceService>();

        _logger?.LogInformation("All services resolved successfully");
    }

    private void RequestExit()
    {
        _logger?.LogInformation("Exit requested - beginning shutdown sequence");

        try
        {
            // Only save if auto-save is enabled; otherwise discard temporary changes
            if (_settingsPersistence?.PersistSettingsOnChange ?? false)
            {
                _settingsPersistence.SaveCurrentSettings();
            }
            else
            {
                _logger?.LogInformation("PersistSettingsOnChange is disabled - discarding unsaved changes");
            }

            DisposeApplicationResources();

            _logger?.LogInformation("Shutdown sequence completed");

            SystemEvents.UserPreferenceChanged -= OnSystemUserPreferenceChanged;
            LoggingConfiguration.CloseAndFlush();
            Shutdown();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during shutdown sequence");
            LoggingConfiguration.CloseAndFlush();
            throw new InvalidOperationException("Error during shutdown sequence", ex);
        }
    }

    private void DisposeApplicationResources()
    {
        ApplicationThemeManager.Changed -= OnApplicationThemeChanged;

        try
        {
            _logger?.LogDebug("Disposing DI service provider (will dispose all services automatically)");
            (_serviceProvider as IDisposable)?.Dispose();
            _serviceProvider = null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to dispose DI host");
        }

        try
        {
            if (_mutexOwned)
            {
                _singleInstanceMutex?.ReleaseMutex();
                _mutexOwned = false;
            }
        }
        catch (ApplicationException ex)
        {
            _logger?.LogDebug(ex, "Single instance mutex was already released");
            _mutexOwned = false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to release single instance mutex");
        }

        try
        {
            _singleInstanceMutex?.Dispose();
            _singleInstanceMutex = null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to dispose single instance mutex");
        }
    }

}

