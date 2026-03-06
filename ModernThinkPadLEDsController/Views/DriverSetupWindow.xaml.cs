using System.Windows;
using ModernThinkPadLEDsController.Hardware;
using Wpf.Ui.Controls;

namespace ModernThinkPadLEDsController.Views;

public partial class DriverSetupWindow : FluentWindow
{
    // DriverReady is set to true when InpOut opens successfully.
    // App.xaml.cs reads this after ShowDialog() returns.
    public bool DriverReady { get; private set; }

    public DriverSetupWindow()
    {
        InitializeComponent();
    }

    private void InitButton_Click(object sender, RoutedEventArgs e)
    {
        InitButton.IsEnabled = false;
        StatusText.Visibility = Visibility.Collapsed;

        if (InpOutDriver.TryOpen(out _))
        {
            DriverReady = true;
            Close();
        }
        else
        {
            StatusText.Text       = "Failed to initialise the driver. Make sure inpoutx64.dll is next to the .exe and that you are running as Administrator.";
            StatusText.Visibility = Visibility.Visible;
            InitButton.IsEnabled  = true;
        }
    }
}
