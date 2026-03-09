using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using ModernThinkPadLEDsController.Hardware;
using Wpf.Ui.Controls;

namespace ModernThinkPadLEDsController.Presentation.Views;

/// <summary>
/// Guides the user through PawnIO installation.
/// </summary>
public partial class PawnIOSetupWindow : FluentWindow
{
    private const string PAWN_IO_DOWNLOAD_URL = "https://pawnio.eu/";

    private enum SetupState
    {
#pragma warning disable RCS1181 // Convert comment to documentation comment
        Initial = 0,                    // Fresh start, button says "Download PawnIO"
        WaitingForManualInstall = 1,    // Browser opened, button says "Restart to Verify"
        Verifying = 2,                  // Checking if PawnIO installed (brief)
        InstallationFailed = 3,         // Error shown, button says "Retry"
        ManualRestartRequired = 4       // Success but restart failed, button says "Close"
    }
#pragma warning restore RCS1181 // Convert comment to documentation comment

    private SetupState _currentState = SetupState.Initial;
    private readonly ILogger<PawnIOSetupWindow> _logger;

    public PawnIOSetupWindow()
    {
        InitializeComponent();
        _logger = LoggerFactory.Create(builder => builder.AddDebug()).CreateLogger<PawnIOSetupWindow>();

        _logger.LogInformation("PawnIO setup window opened");
    }

    private void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        _logger.LogDebug("Install button clicked in state: {State}", _currentState);

        switch (_currentState)
        {
            case SetupState.Initial:
                HandleInitialInstall();
                break;
            case SetupState.WaitingForManualInstall:
                HandleVerification();
                break;
            case SetupState.InstallationFailed:
                HandleRetry();
                break;
            case SetupState.ManualRestartRequired:
                HandleClose();
                break;
            case SetupState.Verifying:
                // Button should be disabled in this state
                _logger.LogWarning("Button clicked while in Verifying state");
                break;
        }
    }

    private void HandleInitialInstall()
    {
        _logger.LogInformation("Attempting PawnIO installation");
        bool hasInstallerLaunched = PawnIOInstaller.InstallPawnIO();

        if (!hasInstallerLaunched)
        {
            _logger.LogInformation("Embedded installer not available, opening download page");
            OpenDownloadPage();
            return;
        }

        _logger.LogInformation("Embedded installer completed, checking installation");

        if (PawnIOInstaller.IsPawnIOInstalled())
        {
            _logger.LogInformation("PawnIO installation verified, attempting restart");
            AttemptApplicationRestart();
        }
        else
        {
            _logger.LogWarning("PawnIO installation failed after embedded installer ran");
            TransitionToState(SetupState.InstallationFailed,
                "PawnIO installation did not complete successfully. Please try installing manually or contact support.",
                System.Windows.Media.Brushes.Red);
        }
    }

    private void HandleVerification()
    {
        _logger.LogInformation("Starting PawnIO verification");
        TransitionToState(SetupState.Verifying,
            "Verifying PawnIO installation...",
            System.Windows.Media.Brushes.Blue);

        if (PawnIOInstaller.IsPawnIOInstalled())
        {
            _logger.LogInformation("PawnIO verified successfully, attempting restart");
            StatusText.Text = "✓ PawnIO verified! Restarting application...";
            StatusText.Foreground = System.Windows.Media.Brushes.Green;

            AttemptApplicationRestart();
        }
        else
        {
            _logger.LogWarning("PawnIO verification failed - not detected");
            TransitionToState(SetupState.InstallationFailed,
                "PawnIO is still not detected. Please make sure you installed it and are running as Administrator.",
                System.Windows.Media.Brushes.Red);
        }
    }

    private void HandleRetry()
    {
        _logger.LogInformation("User clicked retry, opening download page");
        OpenDownloadPage();
    }

    private void HandleClose()
    {
        _logger.LogInformation("User manually closing window after successful installation");
        Close();
    }

    private void OpenDownloadPage()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = PAWN_IO_DOWNLOAD_URL,
                UseShellExecute = true
            });

            _logger.LogInformation("Opened download page in browser");
            TransitionToState(SetupState.WaitingForManualInstall,
                "Opening download page in browser. Please download and install PawnIO, then click the button below.",
                System.Windows.Media.Brushes.IndianRed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open download page in browser");
            TransitionToState(SetupState.WaitingForManualInstall,
                $"Please download and install PawnIO manually from {PAWN_IO_DOWNLOAD_URL}",
                System.Windows.Media.Brushes.IndianRed);
        }
    }

    private void AttemptApplicationRestart()
    {
        try
        {
            string? exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;

            if (string.IsNullOrEmpty(exePath))
            {
                _logger.LogWarning("Could not determine executable path for restart");
                TransitionToState(SetupState.ManualRestartRequired,
                    "✓ PawnIO installed successfully!\n\nCould not determine application path. Please manually restart the application.",
                    System.Windows.Media.Brushes.Green);
                return;
            }

            _logger.LogInformation("Starting new application instance: {ExePath}", exePath);

            // Restart the application
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true
            });

            _logger.LogInformation("New instance started, shutting down current instance");

            // Exit current instance
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to automatically restart application");
            TransitionToState(SetupState.ManualRestartRequired,
                $"✓ PawnIO installed successfully!\n\nAuto-restart failed: {ex.Message}\n\nPlease manually close and restart the application.",
                System.Windows.Media.Brushes.Green);
        }
    }

    private void TransitionToState(SetupState newState, string statusMessage, System.Windows.Media.Brush statusColor)
    {
        _logger.LogDebug("Transitioning from {OldState} to {NewState}", _currentState, newState);
        _currentState = newState;

        UpdateUIForState(statusMessage, statusColor);
    }

    private void UpdateUIForState(string statusMessage, System.Windows.Media.Brush statusColor)
    {
        StatusText.Text = statusMessage;
        StatusText.Foreground = statusColor;
        StatusText.Visibility = Visibility.Visible;

        switch (_currentState)
        {
            case SetupState.Initial:
                UpdateButtonText("Download PawnIO");
                InstallButton.IsEnabled = true;
                break;

            case SetupState.WaitingForManualInstall:
                UpdateButtonText("Restart to Verify");
                InstallButton.IsEnabled = true;
                break;

            case SetupState.Verifying:
                // Button stays disabled during verification
                InstallButton.IsEnabled = false;
                break;

            case SetupState.InstallationFailed:
                UpdateButtonText("Retry");
                InstallButton.IsEnabled = true;
                break;

            case SetupState.ManualRestartRequired:
                UpdateButtonText("Close");
                InstallButton.IsEnabled = true;
                break;
        }
    }

    private void UpdateButtonText(string text)
    {
        // Pattern match to update button text while preserving icon
        if (InstallButton.Content is StackPanel sp &&
            sp.Children.Count > 1 &&
            sp.Children[1] is System.Windows.Controls.TextBlock tb)
        {
            tb.Text = text;
        }
        else
        {
            _logger.LogWarning("Could not update button text - unexpected button structure");
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _logger.LogInformation("User cancelled PawnIO setup");
        Close();
    }
}
