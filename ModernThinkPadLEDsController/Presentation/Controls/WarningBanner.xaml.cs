using System.Windows;
using System.Windows.Controls;

namespace ModernThinkPadLEDsController.Presentation.Controls
{
    /// <summary>
    /// Reusable warning/caution banner with message text
    /// </summary>
    public partial class WarningBanner : UserControl
    {
        public WarningBanner()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Warning message text
        /// </summary>
        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register(nameof(Message), typeof(string), typeof(WarningBanner),
                new PropertyMetadata(string.Empty));

        public string Message
        {
            get => (string)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }
    }
}
