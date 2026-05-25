using BAM.Libraries.SettingsLib.Interface;
using BAM.Libraries.SettingsLib.Models;
using BAM.Libraries.WpfLib.Dialogs;
using BAM.Libraries.WpfLib.Dialogs.Windows;
using BAM.Modules.DataAccess;
using BAM.Modules.DataAccess.Interface;
using BAM.Modules.DataAccess.UI;
using BAM.Modules.DataService;
using BAM.Modules.DataSource;
using BAM.Modules.WebServer;
using BAM.Shell.WPF.Data.Settings;
using BAM.Shell.WPF.Interface;
using BAM.Shell.WPF.Services;
using BAM.Shell.WPF.ViewModels;
using BAM.Shell.WPF.Views;
using DryIoc;
using Prism.Container.DryIoc;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Navigation.Regions;
using Serilog;
using System.Threading.Tasks;
using System.Windows;

namespace BAM.Shell.WPF;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : PrismApplication
{
	public static Rules DefaultRules => Rules.Default.WithConcreteTypeDynamicRegistrations(reuse: Reuse.Transient)
																										.With(Made.Of(FactoryMethod.ConstructorWithResolvableArguments))
																										.WithFuncAndLazyWithoutRegistration()
																										.WithTrackingDisposableTransients()
																										.WithFactorySelector(Rules.SelectLastRegisteredFactory());

	protected override Window CreateShell()
	{
		// Use this line to test DialogService
		// Window mainWindow = Container.Resolve<MessageBoxTestWindow>();
		Window mainWindow = Container.Resolve<MainWindow>();

		// The UI Service is automatically created by the main window view model
		var themeService = Container.Resolve<IThemeService>();

		// Set the initial dark mode
		var settings = Container.Resolve<ISettingsProvider>().GetSettings<BAMShellWPFSettings>();
		themeService.SetDarkMode(settings.InDarkMode);

		// set statics where required, which I would like to avoid someday
		SecurityTradeImportView.Container = Container.GetContainer();

		return mainWindow;
	}

	protected override Rules CreateContainerRules()
	{
		return DefaultRules;
	}

	protected override void RegisterTypes(IContainerRegistry containerRegistry)
	{
		var regionManager = containerRegistry.GetContainer().Resolve<IRegionManager>();
		regionManager.RegisterViewWithRegion(Constants.RegionName_Content, typeof(DashboardView));
		regionManager.RegisterViewWithRegion(Constants.RegionName_Content, typeof(PortfolioView));
		regionManager.RegisterViewWithRegion(Constants.RegionName_Content, typeof(WatchlistsView));
		regionManager.RegisterViewWithRegion(Constants.RegionName_Content, typeof(AccountsView));
		regionManager.RegisterViewWithRegion(Constants.RegionName_Content, typeof(TransactionsView));
		regionManager.RegisterViewWithRegion(Constants.RegionName_Content, typeof(BudgetsView));
		regionManager.RegisterViewWithRegion(Constants.RegionName_Content, typeof(SettingsView));
		regionManager.RegisterViewWithRegion(Constants.RegionName_Content, typeof(Login2View));
		regionManager.RegisterViewWithRegion(Constants.RegionName_Content, typeof(LogoutView));
		regionManager.RegisterViewWithRegion(Constants.RegionName_Content, typeof(EntityView));

		containerRegistry.Register<IAppConfiguration, BAMShellWPFAppConfiguration>();
		containerRegistry.RegisterSingleton<ISettingsProvider, XmlSettingsProviderService>();
		containerRegistry.RegisterSingleton<IThemeService, ThemeService>();
		containerRegistry.RegisterSingleton<IBamDialogService, BamDialogService>();

		// Ensure LoginViewModel implements IDialogAware
		containerRegistry.RegisterDialog<LoginView, LoginViewModel>();
		containerRegistry.RegisterDialog<DataSourceManagerView, DataSourceManagerViewModel>();
		containerRegistry.RegisterDialog<DataSourceManager2View, DataSourceManager2ViewModel>();
		containerRegistry.RegisterDialog<SecurityTradeImportView, SecurityTradeImportViewModel>();

		// register shell services
		containerRegistry.RegisterSingleton<IShellService, ShellService>();
		containerRegistry.RegisterSingleton<IActionService, ActionService>();
	}

	protected override void OnInitialized()
	{
		var shellService = Container.Resolve<IShellService>();
		if (shellService.ShowInitialLoginView(out var isAuthenticated))
		{
			if (isAuthenticated)
			{
				// If the login view was shown and the user is authenticated, proceed with showing the main window
				base.OnInitialized();
			}
			else
			{
				// If the login view was not shown (e.g., user canceled), shut down the application
				Current.Shutdown();
			}
		}
		else // Show the MainWindow
		{
			base.OnInitialized();

			// Show Login view if there aren't any profiles in the current database
			using var uow = Container.Resolve<IFinanceDbUnitOfWork>();
			if (uow.ProfileRepository.Any())
			{
				var settings = Container.Resolve<ISettingsProvider>().GetSettings<BAMShellWPFSettings>();
				if (settings.AutoLogin)
				{
					Task.Run(async () => shellService.DoAutoLoginAsync());
				}
				else
				{
					var actionService = Container.Resolve<IActionService>();
					actionService.ShowView(EActionServiceViewType.LogonView);
				}
			}
		}
	}

	protected override IModuleCatalog CreateModuleCatalog()
	{
		// Modules will be registered in the following order
		ModuleCatalog catalog = new ModuleCatalog();
		catalog.AddModule(typeof(DataSourceModule));
		catalog.AddModule(typeof(DataAccessModule));
		catalog.AddModule(typeof(DataServiceModule));
		catalog.AddModule(typeof(WebServerModule));
		catalog.AddModule(typeof(DataAccessUIModule));
		return catalog;
	}

	protected override void OnExit(ExitEventArgs e)
	{
		Log.CloseAndFlush();

		base.OnExit(e);
	}
}
