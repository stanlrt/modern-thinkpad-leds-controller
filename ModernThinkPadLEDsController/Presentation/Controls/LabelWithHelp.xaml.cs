using System.Windows;
using System.Windows.Controls;

namespace ModernThinkPadLEDsController.Presentation.Controls
{
    /// <summary>
    /// Reusable label with optional help icon and tooltip
    /// </summary>
    public partial class LabelWithHelp : UserControl
    {
        public LabelWithHelp()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Label text
        /// </summary>
        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(nameof(Label), typeof(string), typeof(LabelWithHelp),
                new PropertyMetadata(string.Empty));

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        /// <summary>
        /// Help tooltip text
        /// </summary>
        public static readonly DependencyProperty HelpTextProperty =
            DependencyProperty.Register(nameof(HelpText), typeof(string), typeof(LabelWithHelp),
                new PropertyMetadata(string.Empty));

        public string HelpText
        {
            get => (string)GetValue(HelpTextProperty);
            set => SetValue(HelpTextProperty, value);
        }

        /// <summary>
        /// Help icon visibility
        /// </summary>
        public static readonly DependencyProperty HelpVisibilityProperty =
            DependencyProperty.Register(nameof(HelpVisibility), typeof(Visibility), typeof(LabelWithHelp),
                new PropertyMetadata(Visibility.Visible));

        public Visibility HelpVisibility
        {
            get => (Visibility)GetValue(HelpVisibilityProperty);
            set => SetValue(HelpVisibilityProperty, value);
        }

        /// <summary>
        /// Font weight
        /// </summary>
        public static new readonly DependencyProperty FontWeightProperty =
            DependencyProperty.Register(nameof(FontWeight), typeof(FontWeight), typeof(LabelWithHelp),
                new PropertyMetadata(FontWeights.Normal));

        public new FontWeight FontWeight
        {
            get => (FontWeight)GetValue(FontWeightProperty);
            set => SetValue(FontWeightProperty, value);
        }
    }
}
