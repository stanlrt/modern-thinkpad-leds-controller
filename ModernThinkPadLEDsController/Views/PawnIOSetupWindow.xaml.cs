using System;
using System.Windows;
using System.Windows.Controls;
using ModernThinkPadLEDsController.Hardware;
using Wpf.Ui.Controls;

namespace ModernThinkPadLEDsController.Views;

public partial class PawnIOSetupWindow : FluentWindow
{
    // InstalledSuccessfully is set to true when PawnIO installs and opens successfully.
    // App.xaml.cs reads this after ShowDialog() returns.
    public bool InstalledSuccessfully { get; private set; }

    // Track if we're in verification mode (after opening download page)
    private bool _verificationMode = false;

    public PawnIOSetupWindow()
    {
        InitializeComponent();
    }

    private void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        InstallButton.IsEnabled = false;
        StatusText.Visibility = Visibility.Collapsed;

        // If we're in verification mode, just check if PawnIO is installed
        if (_verificationMode)
        {
            // Show verifying status
            StatusText.Text = "Verifying PawnIO installation...";
            StatusText.Foreground = System.Windows.Media.Brushes.Blue;
            StatusText.Visibility = Visibility.Visible;

            // Check if PawnIO service is running
            if (PawnIOInstaller.IsPawnIOInstalled())
            {
                StatusText.Text = "✓ PawnIO verified! Restarting application...";
                StatusText.Foreground = System.Windows.Media.Brushes.Green;

                try
                {
                    // Get the exe path - works for both debug and release
                    var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;

                    if (!string.IsNullOrEmpty(exePath))
                    {
                        // Restart the application (without runas - we're already admin if PawnIO works)
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = exePath,
                            UseShellExecute = true
                        });

                        // Exit current instance
                        System.Windows.Application.Current.Shutdown();
                    }
                    else
                    {
                        StatusText.Text = "✓ PawnIO verified! Please manually restart the application.";
                        InstalledSuccessfully = true;
                        Close();
                    }
                }
                catch (Exception ex)
                {
                    StatusText.Text = $"✓ PawnIO verified! Auto-restart failed: {ex.Message}\n\nPlease manually restart the application.";
                    InstalledSuccessfully = true;
                    Close();
                }
            }
            else
            {
                StatusText.Text = "PawnIO is still not detected. Please make sure you installed it and are running as Administrator.";
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
                StatusText.Visibility = Visibility.Visible;
                InstallButton.IsEnabled = true;
            }
            return;
        }

        // Try to launch the embedded PawnIO installer
        bool installerLaunched = PawnIOInstaller.InstallPawnIO();

        if (!installerLaunched)
        {
            // Installer not embedded - open browser to download page
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://pawnio.eu/",
                    UseShellExecute = true
                });
                StatusText.Text = "Opening download page in browser. Please download and install PawnIO, then click the button below.";

                // Update button text
                if (InstallButton.Content is StackPanel sp && sp.Children.Count > 1 && sp.Children[1] is System.Windows.Controls.TextBlock tb)
                {
                    tb.Text = "Restart to verify";
                }

                _verificationMode = true;
            }
            catch
            {
                StatusText.Text = "Please download and install PawnIO manually from https://pawnio.eu/";
            }
            StatusText.Visibility = Visibility.Visible;
            InstallButton.IsEnabled = true;
            return;
        }

        // Embedded installer was launched - verify installation was successful
        if (PawnIOInstaller.IsPawnIOInstalled())
        {
            InstalledSuccessfully = true;
            Close();
        }
        else
        {
            StatusText.Text = "PawnIO installation did not complete successfully. Please try installing manually from https://pawnio.eu/ or contact support.";
            StatusText.Visibility = Visibility.Visible;
            InstallButton.IsEnabled = true;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        InstalledSuccessfully = false;
        Close();
    }
}
