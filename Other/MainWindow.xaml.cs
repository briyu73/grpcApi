using BAM.Shell.WPF.Interface;
using BAM.Shell.WPF.Services;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace BAM.Shell.WPF.Views
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		// *******************************************************************************************
		// Fields
		// *******************************************************************************************

		#region Fields

		private readonly IShellService _shellService;

		#endregion Fields

		// *******************************************************************************************
		// Properties
		// *******************************************************************************************

		#region Properties

		#endregion

		// *******************************************************************************************
		// Commands
		// *******************************************************************************************

		#region Commands

		#endregion

		// *******************************************************************************************
		// Public Methods
		// *******************************************************************************************

		#region Public Methods

		public MainWindow(IShellService shellService)
		{
			_shellService = shellService;

			InitializeComponent();

			Loaded += MainWindow2_Loaded;
			Closing += MainWindow2_Closing;
		}

		#endregion

		// *******************************************************************************************
		// Protected Methods
		// *******************************************************************************************

		#region Protected Methods

		#endregion

		// *******************************************************************************************
		// Private Methods
		// *******************************************************************************************

		#region Private Methods

		private void Window_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (e.LeftButton == MouseButtonState.Pressed)
			{
				DragMove();
			}
		}

		private void btnMinimize_Click(object sender, RoutedEventArgs e)
		{
			WindowState = WindowState.Minimized;
		}

		private void btnMaximize_Click(object sender, RoutedEventArgs e)
		{
			WindowState = WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
		}

		private void btnClose_Click(object sender, RoutedEventArgs e)
		{
			System.Windows.Application.Current.Shutdown();
		}

		private void panelControlBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			WindowInteropHelper helper = new WindowInteropHelper(this);
			SendMessage(helper.Handle, 161, 2, 0);
		}

		private void panelControlBar_MouseEnter(object sender, MouseEventArgs e)
		{
			// make the window maximize to the windows desktop workspace only
			MaxHeight = SystemParameters.MaximizedPrimaryScreenHeight;
		}

		[DllImport("user32.dll")]
		private static extern IntPtr SendMessage(IntPtr hwnd, int msg, int wParam, int lParam);

		private void MainWindow2_Closing(object? sender, CancelEventArgs e)
		{
			_shellService.LogoutUser();

			// Only save size and position if the window is in Normal state
			if (this.WindowState == WindowState.Normal)
			{
				Properties.Settings.Default.WindowLeft = this.Left;
				Properties.Settings.Default.WindowTop = this.Top;
				Properties.Settings.Default.WindowWidth = this.Width;
				Properties.Settings.Default.WindowHeight = this.Height;
			}

			// Save the window state
			Properties.Settings.Default.WindowStateMaximised = (this.WindowState == WindowState.Maximized);

			// Save the settings
			Properties.Settings.Default.Save();
		}

		private void MainWindow2_Loaded(object sender, RoutedEventArgs e)
		{
			// Restore window position and size
			this.Left = Properties.Settings.Default.WindowLeft;
			this.Top = Properties.Settings.Default.WindowTop;
			this.Width = Properties.Settings.Default.WindowWidth;
			this.Height = Properties.Settings.Default.WindowHeight;

			// Restore window state (Normal, Maximized, Minimized)
			if (Properties.Settings.Default.WindowStateMaximised)
			{
				this.WindowState = WindowState.Maximized;
			}

			// Ensure the window is within screen bounds
			EnsureWindowIsVisible();
		}

		private void EnsureWindowIsVisible()
		{
			// Get the working area of the screen
			var workArea = SystemParameters.WorkArea;

			// Ensure window is within screen bounds
			if (this.Left < workArea.Left || this.Left >= workArea.Right)
				this.Left = workArea.Left;
			if (this.Top < workArea.Top || this.Top >= workArea.Bottom)
				this.Top = workArea.Top;

			// Ensure window size is not larger than screen
			if (this.Width > workArea.Width)
				this.Width = workArea.Width;
			if (this.Height > workArea.Height)
				this.Height = workArea.Height;
		}

		#endregion

		// *******************************************************************************************
		// Disposal Support
		// *******************************************************************************************

		#region IDisposalSupport

		#endregion

		private void ToolsRadioButton_MouseEnter(object sender, MouseEventArgs e)
		{
			if (!ToolsHoverPopup.IsOpen && sender is RadioButton button)
			{
				ToolsHoverPopup.IsOpen = true;
			}
		}

		private void ToolsRadioButton_MouseLeave(object sender, MouseEventArgs e)
		{
			if (ToolsHoverPopup.IsOpen && sender is RadioButton button)
			{
				ToolsHoverPopup.IsOpen = false;
			}
		}

		private void ToolsHoverRadioButton_Click(object sender, RoutedEventArgs e)
		{
			if (ToolsHoverPopup.IsOpen && sender is RadioButton button)
			{
				ToolsHoverPopup.IsOpen = false;
			}
		}

		private void ToolsHoverBorder_MouseEnter(object sender, MouseEventArgs e)
		{
			if (!ToolsHoverPopup.IsOpen && sender is Border)
			{
				ToolsHoverPopup.IsOpen = true;
			}
		}

		private void ToolsHoverBorder_MouseLeave(object sender, MouseEventArgs e)
		{
			if (ToolsHoverPopup.IsOpen && sender is Border)
			{
				ToolsHoverPopup.IsOpen = false;
			}
		}
	}
}
