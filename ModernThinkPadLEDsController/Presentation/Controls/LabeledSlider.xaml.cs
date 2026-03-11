using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LabeledSlider control)
            {
                control.ValidateValue((double)e.NewValue);
            }
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

        /// <summary>
        /// Validation error message
        /// </summary>
        public static readonly DependencyProperty ValidationErrorProperty =
            DependencyProperty.Register(nameof(ValidationError), typeof(string), typeof(LabeledSlider),
                new PropertyMetadata(string.Empty));

        public string ValidationError
        {
            get => (string)GetValue(ValidationErrorProperty);
            set => SetValue(ValidationErrorProperty, value);
        }

        /// <summary>
        /// Validation error visibility
        /// </summary>
        public static readonly DependencyProperty ValidationErrorVisibilityProperty =
            DependencyProperty.Register(nameof(ValidationErrorVisibility), typeof(Visibility), typeof(LabeledSlider),
                new PropertyMetadata(Visibility.Collapsed));

        public Visibility ValidationErrorVisibility
        {
            get => (Visibility)GetValue(ValidationErrorVisibilityProperty);
            set => SetValue(ValidationErrorVisibilityProperty, value);
        }

        private static readonly Regex _regex = new Regex("^[0-9]+$");

        /// <summary>
        /// Validates that only numeric input is allowed in the TextBox
        /// </summary>
        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            // Allow only digits and optionally a minus sign at the beginning
            e.Handled = !IsTextNumeric(e.Text);
        }

        private static bool IsTextNumeric(string text)
        {
            // Allow digits only (no decimal point since Value is bound as integer)
            return _regex.IsMatch(text);
        }

        /// <summary>
        /// Validates the value when the TextBox loses focus
        /// </summary>
        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if (double.TryParse(textBox.Text, out double value))
                {
                    Value = value; // This will trigger validation through OnValueChanged
                }
                else
                {
                    ValidationError = "Please enter a valid number";
                    ValidationErrorVisibility = Visibility.Visible;
                    Value = Minimum;
                }
            }
        }

        /// <summary>
        /// Validates the current value and shows/hides error message accordingly
        /// </summary>
        private void ValidateValue(double value)
        {
            if (value < Minimum)
            {
                ValidationError = $"Value must be at least {Minimum}";
                ValidationErrorVisibility = Visibility.Visible;
            }
            else if (value > Maximum)
            {
                ValidationError = $"Value must be at most {Maximum}";
                ValidationErrorVisibility = Visibility.Visible;
            }
            else
            {
                ValidationError = string.Empty;
                ValidationErrorVisibility = Visibility.Collapsed;
            }
        }
    }
}
