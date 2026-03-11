using System.Windows;
using System.Windows.Controls;

namespace ModernThinkPadLEDsController.Presentation.Controls
{
    /// <summary>
    /// Reusable hex register ID input field
    /// </summary>
    public partial class HexRegisterInput : UserControl
    {
        public HexRegisterInput()
        {
            InitializeComponent();
        }

        /// <summary>
        /// The hex value (as byte?)
        /// </summary>
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(byte?), typeof(HexRegisterInput),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public byte? Value
        {
            get => (byte?)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        /// <summary>
        /// Example hex value for tooltip (e.g., "00", "0A")
        /// </summary>
        public static readonly DependencyProperty HexExampleProperty =
            DependencyProperty.Register(nameof(HexExample), typeof(string), typeof(HexRegisterInput),
                new PropertyMetadata("00"));

        public string HexExample
        {
            get => (string)GetValue(HexExampleProperty);
            set => SetValue(HexExampleProperty, value);
        }
    }
}
