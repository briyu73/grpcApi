using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BAM.Libraries.WpfLib.CustomControls
{
	public partial class ToggleSwitch : UserControl
	{
		public static readonly DependencyProperty IsCheckedProperty =
				DependencyProperty.Register("IsChecked", typeof(bool), typeof(ToggleSwitch),
						new PropertyMetadata(false, OnIsCheckedChanged));

		public bool IsChecked
		{
			get { return (bool)GetValue(IsCheckedProperty); }
			set { SetValue(IsCheckedProperty, value); }
		}

		public ToggleSwitch()
		{
			InitializeComponent();
			MouseLeftButtonDown += ToggleSwitch_MouseLeftButtonDown;
		}

		private void ToggleSwitch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			IsChecked = !IsChecked;
			e.Handled = true;
		}

		private static void OnIsCheckedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			// Handle property changed if needed
		}
	}
}
