using System.Windows;
using System.Windows.Controls;

namespace ModernThinkPadLEDsController.Presentation.Controls
{
    /// <summary>
    /// Reusable slider control with label, value display, and description
    /// </summary>
    public partial class LabeledSlider : UserControl
    {
        public LabeledSlider()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Label (title)
        /// </summary>
        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(nameof(Label), typeof(string), typeof(LabeledSlider),
                new PropertyMetadata(string.Empty));

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        /// <summary>
        /// Value
        /// </summary>
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(double), typeof(LabeledSlider),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        /// <summary>
        /// Minimum
        /// </summary>
        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(LabeledSlider),
                new PropertyMetadata(0.0));

        public double Minimum
        {
            get => (double)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        /// <summary>
        /// Maximum
        /// </summary>
        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(LabeledSlider),
                new PropertyMetadata(100.0));

        public double Maximum
        {
            get => (double)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        /// <summary>
        /// TickFrequency
        /// </summary>
        public static readonly DependencyProperty TickFrequencyProperty =
            DependencyProperty.Register(nameof(TickFrequency), typeof(double), typeof(LabeledSlider),
                new PropertyMetadata(1.0));

        public double TickFrequency
        {
            get => (double)GetValue(TickFrequencyProperty);
            set => SetValue(TickFrequencyProperty, value);
        }

        /// <summary>
        /// Unit (e.g., "ms", "%")
        /// </summary>
        public static readonly DependencyProperty UnitProperty =
            DependencyProperty.Register(nameof(Unit), typeof(string), typeof(LabeledSlider),
                new PropertyMetadata(string.Empty));

        public string Unit
        {
            get => (string)GetValue(UnitProperty);
            set => SetValue(UnitProperty, value);
        }

        /// <summary>
        /// Description (help text)
        /// </summary>
        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register(nameof(Description), typeof(string), typeof(LabeledSlider),
                new PropertyMetadata(string.Empty));

        public string Description
        {
            get => (string)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        /// <summary>
        /// Description visibility
        /// </summary>
        public static readonly DependencyProperty DescriptionVisibilityProperty =
            DependencyProperty.Register(nameof(DescriptionVisibility), typeof(Visibility), typeof(LabeledSlider),
                new PropertyMetadata(Visibility.Visible));

        public Visibility DescriptionVisibility
        {
            get => (Visibility)GetValue(DescriptionVisibilityProperty);
            set => SetValue(DescriptionVisibilityProperty, value);
        }
    }
}
