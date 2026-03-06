using System.ComponentModel;
using System.Windows;
using ModernThinkPadLEDsController.ViewModels;
using Wpf.Ui.Controls;

namespace ModernThinkPadLEDsController;

public partial class MainWindow : FluentWindow
{
    public MainViewModel    MainVm     { get; }
    public SettingsViewModel SettingsVm { get; }

    public MainWindow(MainViewModel mainVm, SettingsViewModel settingsVm)
    {
        MainVm     = mainVm;
        SettingsVm = settingsVm;
        InitializeComponent();
        DataContext = this;
    }

    // When the user clicks ✕, hide to tray instead of closing.
    // App.xaml.cs listens to TrayIconService.ExitRequested for a real exit.
    private void Window_Closing(object sender, CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}
