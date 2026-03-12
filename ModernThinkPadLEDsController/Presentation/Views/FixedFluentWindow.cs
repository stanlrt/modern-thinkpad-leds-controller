using System.Reflection;
using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace ModernThinkPadLEDsController.Presentation.Views;

/// <summary>
/// Base class for all FluentWindow instances that fixes the memory leak in WPF UI 4.2.0.
/// See: https://github.com/lepoco/wpfui/issues/1648
/// </summary>
/// <remarks>
/// FluentWindow subscribes to the static event ApplicationThemeManager.Changed without
/// unsubscribing when closed, preventing garbage collection of the window and its DataContext.
/// This base class overrides OnClosed to properly unsubscribe from the event.
/// </remarks>
public abstract class FixedFluentWindow : FluentWindow
{
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        // Workaround for memory leak: unsubscribe from ApplicationThemeManager.Changed
        UnsubscribeFromThemeManager();
    }

    private void UnsubscribeFromThemeManager()
    {
        Type? type = typeof(ApplicationThemeManager);
        FieldInfo? eventField = type.GetField("Changed", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Instance);

        if (eventField?.GetValue(null) is MulticastDelegate eventDelegate)
        {
            foreach (Delegate handler in eventDelegate.GetInvocationList())
            {
                if (handler.Target == this)
                {
                    ApplicationThemeManager.Changed -= (ThemeChangedEvent)handler;
                }
            }
        }
    }
}
