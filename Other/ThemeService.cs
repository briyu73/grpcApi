
using BAM.Shell.WPF.Interface;
using MaterialDesignThemes.Wpf;

namespace BAM.Shell.WPF.Services;

public class ThemeService : IThemeService
{
	// *******************************************************************************************
	// Fields
	// *******************************************************************************************

	#region Fields

	//private bool _inDarkMode;

	//private ResourceDictionary? _themeResourceDictionary;

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

	public ThemeService()
	{ }

	public bool IsDarkMode { get; private set; }

	public void SetDarkMode(bool isDark)
	{
		var paletteHelper = new PaletteHelper();
		var theme = paletteHelper.GetTheme();

		theme.SetBaseTheme(isDark ? BaseTheme.Dark : BaseTheme.Light);

		paletteHelper.SetTheme(theme);

		IsDarkMode = isDark;
	}

	public void SwitchDarkMode() => SetDarkMode(!IsDarkMode);

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

	#endregion

	// *******************************************************************************************
	// Disposal Support
	// *******************************************************************************************

	#region IDisposalSupport

	#endregion
}
