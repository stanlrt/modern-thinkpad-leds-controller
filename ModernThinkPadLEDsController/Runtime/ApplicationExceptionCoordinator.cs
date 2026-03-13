using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using ModernThinkPadLEDsController.Logging;

namespace ModernThinkPadLEDsController.Runtime;

/// <summary>
/// Centralizes global exception handling and fatal shutdown behavior for the application.
/// </summary>
public sealed class ApplicationExceptionCoordinator
{
    private readonly Dispatcher _dispatcher;
    private readonly Func<ILogger?> _loggerProvider;
    private readonly Action _disposeApplicationResources;
    private readonly Action<int> _shutdown;

    private int _fatalShutdownStarted;

    public ApplicationExceptionCoordinator(
        Dispatcher dispatcher,
        Func<ILogger?> loggerProvider,
        Action disposeApplicationResources,
        Action<int> shutdown)
    {
        _dispatcher = dispatcher;
        _loggerProvider = loggerProvider;
        _disposeApplicationResources = disposeApplicationResources;
        _shutdown = shutdown;
    }

    public void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (IsKnownRecoverableException(e.Exception))
        {
            Logger?.LogWarning(e.Exception, "Recoverable exception on UI thread");
            e.Handled = true;
            return;
        }

        Logger?.LogCritical(e.Exception, "Unhandled exception on UI thread - shutting down");
        e.Handled = true;
        BeginFatalShutdown("UI Thread Exception", e.Exception);
    }

    public void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is not Exception ex)
        {
            return;
        }

        if (Logger?.IsEnabled(LogLevel.Critical) == true)
        {
            Logger.LogCritical(ex, "Unhandled exception on background thread. IsTerminating: {IsTerminating}", e.IsTerminating);
        }

        if (e.IsTerminating)
        {
            HandleTerminatingFatalException("Background Thread Exception", ex);
            return;
        }

        BeginFatalShutdown("Background Thread Exception", ex);
    }

    public void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        if (IsKnownRecoverableException(e.Exception))
        {
            Logger?.LogWarning(e.Exception, "Recoverable task exception observed during shutdown/runtime");
            e.SetObserved();
            return;
        }

        Logger?.LogCritical(e.Exception, "Unobserved task exception - shutting down");
        e.SetObserved();
        BeginFatalShutdown("Task Exception", e.Exception);
    }

    private ILogger? Logger => _loggerProvider();

    private bool IsKnownRecoverableException(Exception ex)
    {
        return ex switch
        {
            OperationCanceledException => true,
            ObjectDisposedException when _dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished => true,
            AggregateException aggregate when aggregate.InnerExceptions.Count > 0 => aggregate.InnerExceptions.All(IsKnownRecoverableException),
            _ => false,
        };
    }

    private void BeginFatalShutdown(string title, Exception ex)
    {
        if (Interlocked.Exchange(ref _fatalShutdownStarted, 1) == 1)
        {
            return;
        }

        ShowFatalException(title, ex);

        void ShutdownAction()
        {
            try
            {
                Logger?.LogWarning("Fatal exception shutdown - skipping settings save");
                _disposeApplicationResources();
                LoggingConfiguration.CloseAndFlush();
                _shutdown(-1);
            }
            catch (Exception shutdownEx)
            {
                Logger?.LogError(shutdownEx, "Failed during fatal shutdown cleanup");
                LoggingConfiguration.CloseAndFlush();
                _shutdown(-1);
            }
        }

        if (_dispatcher.CheckAccess())
        {
            ShutdownAction();
        }
        else
        {
            _dispatcher.BeginInvoke(ShutdownAction);
        }
    }

    private void HandleTerminatingFatalException(string title, Exception ex)
    {
        if (Interlocked.Exchange(ref _fatalShutdownStarted, 1) == 1)
        {
            return;
        }

        ShowFatalException(title, ex);
        _disposeApplicationResources();
        LoggingConfiguration.CloseAndFlush();
    }

    private void ShowFatalException(string title, Exception ex)
    {
        try
        {
            if (_dispatcher.CheckAccess())
            {
                LogAndShowException(title, ex);
            }
            else
            {
                _dispatcher.Invoke(() => LogAndShowException(title, ex));
            }
        }
        catch
        {
            try
            {
                LogAndShowException(title, ex);
            }
            catch
            {
                // Best-effort only when the dispatcher is already compromised.
            }
        }
    }

    private static void LogAndShowException(string title, Exception ex)
    {
        string message = $"{ex.GetType().Name}: {ex.Message}\n\n" +
                     $"Stack Trace:\n{ex.StackTrace}\n\n" +
                     "═══════════════════════════════════════════\n" +
                     $"Logs are saved to:\n{StartupEmergencyLogger.LogDirectory}\n" +
                     "═══════════════════════════════════════════";

        System.Windows.MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
