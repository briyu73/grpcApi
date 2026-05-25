using System.Windows;

namespace BAM.Shell.WPF.Interface;

public interface IThemeService
{
	bool IsDarkMode { get; }

	void SetDarkMode(bool isDark);

	void SwitchDarkMode();
}
