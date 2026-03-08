using System.Windows;
using System.Windows.Interop;
using ModernThinkPadLEDsController.Presentation.Views;

namespace ModernThinkPadLEDsController.Shell;

/// <summary>
/// Owns shell-level interactions with the main window.
/// </summary>
public sealed class MainWindowHost
{
    private readonly MainWindow _mainWindow;

    public MainWindowHost(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;
    }

    public event EventHandler? SourceInitialized
    {
        add => _mainWindow.SourceInitialized += value;
        remove => _mainWindow.SourceInitialized -= value;
    }

    public IntPtr EnsureMainWindowHandle()
    {
        return new WindowInteropHelper(_mainWindow).EnsureHandle();
    }

    public void ShowMainWindow()
    {
        _mainWindow.Show();
    }

    public void ShowAndActivateMainWindow()
    {
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    public void Dispatch(Action action)
    {
        _mainWindow.Dispatcher.Invoke(action);
    }
}
