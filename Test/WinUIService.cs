// Classification: OFFICIAL
// 
// Copyright (C) 2021 Commonwealth of Australia.
// 
// All rights reserved.
// 
// The copyright herein resides with the Commonwealth of Australia.
// The material(s) may not be used, modified, copied and/or distributed
// without the written permission of the Commonwealth of Australia
// represented by Defence Science and Technology Group, the Department
// of Defence. The copyright notice above does not evidence any actual or 
// intended publication of such material(s).
// 
// This material is provided on an "AS IS" basis and the Commonwealth of
// Australia makes no representation or warranties of any kind, express 
// or implied, of merchantability or fitness for any purpose. The
// Commonwealth of Australia does not accept any liability arising from or
// connected to the use of the material.
// 
// Use of the material is entirely at the Licensee's own risk.

using FlewsApp.Common.DataModels.Interface.Documents;
using FlewsApp.Common.DataModels.Interface.UI;
using FlewsApp.Common.Infrastructure.Messaging;
using FlewsApp.Modules.Config.Data.Settings;
using FlewsApp.Modules.Config.Services;
using FlewsApp.Modules.UI.Extensions;
using FlewsApp.Modules.UI.Interface;
using FlewsApp.Modules.UI.Messaging;
using FlewsApp.Modules.UI.Models;
using FlewsApp.Modules.UI.Services.DialogService;
using FlewsApp.Modules.UI.ViewModels;
using FlewsApp.Modules.UI.Views;
using FlewsApp.Modules.UI.Windows;
using FlewseDotNetLib.Settings;
using Mad.Libraries.Core.Events;
using Mad.Libraries.Settings.Interface;
using Mad.Libraries.ShellBase;
using Mad.Libraries.ShellBase.Events;
using Mad.Libraries.ShellBase.Interface;
using Mad.Libraries.ShellBase.ViewModels.MenuItems;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Prism.Events;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using Swordfish.NET.General;
using Swordfish.NET.ViewModel;
using Swordfish.NET.WPF;
using Swordfish.NET.WPF.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using Xceed.Wpf.Toolkit;

namespace FlewsApp.Modules.UI.Services
{
  // This is the Windows implementation version of this service
  public class WinUIService : BindableBase, IFlewsUIService, IDisposable
  {
    // *******************************************************************************************
    // Fields
    // *******************************************************************************************

    #region Fields

    /// <summary>
    /// Class logger for logging issues
    /// </summary>
    private readonly ILogger _logger;

    // FLEWS Communication bus
    private readonly IEventAggregator _eventAggregator;
    private readonly ConfigModuleSettings _configModuleSettings;
    private readonly IDialogService _dialogService;
    private readonly IAppSettingsAdapter _appSettingsAdapter;

    private static Window? _shellWindow;
    private static int _waitWindowCount;
    private static WaitWindow? _lastInstance;
    private static ProgressWindow? _progressWin;

    #endregion

    // *******************************************************************************************
    // Properties
    // *******************************************************************************************

    #region Properties

    public IAppSettings AppSettings { get; }

    public string IconFolder => Path.Combine(AppSettings.FlewsSettings.ProjectConfig, "CPIcons");

    public Point LastLeftClickPoint { get; set; }

    public Point LastRightClickPoint { get; set; }

    public string AboutDialogText
    {
      get
      {
        string buildTime = BuildTime;
        string version = AppVersion;
        string company = AppSettings.Adapter.Company ?? GetEntryAssemblyAttribute<AssemblyCompanyAttribute>().Company;
        string copyright = GetEntryAssemblyAttribute<AssemblyCopyrightAttribute>().Copyright;

        return $"{AppSettings.Adapter.ApplicationName}, Developed by {company}{Environment.NewLine}{Environment.NewLine}Version {version}, built at {buildTime}{Environment.NewLine}{Environment.NewLine}Copyright {copyright}{Environment.NewLine}";
      }
    }

    public string BuildTime => PeHeaderReader.GetAssemblyHeader(Application.Current.GetType()).TimeStamp.ToString("dd MMM yyyy HH:mm");

    public string AppVersion => Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? String.Empty;

    public bool IsRunningInProjectHome => Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)?.Contains(AppSettings.FlewsSettings.ProjectHome) ?? false;

    public string AppTitle => $"{AppSettings.Adapter.ProductName} ({AppSettings.Adapter.ApplicationName}) - (Version {AppVersion}, built {BuildTime}){(IsRunningInProjectHome ? "" : $" - Running from {Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}")}";

    #endregion

    // *******************************************************************************************
    // Commands
    // *******************************************************************************************

    #region Commands

    private readonly RelayCommandFactory _showAppSettingsViewCommand = new RelayCommandFactory();
    public ICommand ShowAppSettingsViewCommand => _showAppSettingsViewCommand.GetCommand(
      p => _dialogService.Show(nameof(MainSettingsView), null, null));

    // Attempts to open the file in Notepad++ first, and if that fails it falls back on the default application
    private readonly RelayCommandFactory _openFileCommand = new();
    public ICommand OpenFileCommand => _openFileCommand.GetCommandAsync(
      async p =>
      {
        var filePath = p!.ToString()!;
        if (filePath.Contains(DesignPathProvider.FLEWS_COMPRESSED_FILE_EXT, StringComparison.OrdinalIgnoreCase))
        {
          using FileStream compressedFileStream = File.Open(filePath, FileMode.Open);
          using var decompressor = new BrotliStream(compressedFileStream, CompressionMode.Decompress);
          var doc = XDocument.Load(decompressor);

          filePath = Path.Combine(AppSettings.UserAppTempPath, Path.GetFileName(filePath)).Replace(DesignPathProvider.FLEWS_COMPRESSED_FILE_EXT, ".xml");

          // save the decompressed file to the new file path - these files will be automatically delete when the application exits
          doc.Save(filePath);
        }

        var process = HelperFns.GetNotepadPlusPlusProcessInfo(filePath) ?? new Process
        {
          StartInfo = new ProcessStartInfo(filePath!)
          {
            UseShellExecute = true,
          }
        };
        await Task.Run(() => process.Start());
      },
      p => p != null
    );

    private readonly RelayCommandFactory _openFlewzFileInReadableFormatCommand = new RelayCommandFactory();
    public ICommand OpenFlewzFileInReadableFormatCommand => _openFlewzFileInReadableFormatCommand.GetCommand(
      p =>
      {
        string? filename = p as string;
        if (filename == null)
        {
          var initialPath = Path.Combine(AppSettings.FlewsSettings.ProjectHome, "UserData", "Scenarios");
          filename = OpenFileDialog(
                                initialPath,
                                ".flewz",
                                "Flewz files (*.flewz)|*.flewz",
                                caption: "Select FLEWS File");
        }

        if (!string.IsNullOrEmpty(filename))
        {
          if (OpenFileCommand.CanExecute(filename))
          {
            OpenFileCommand.Execute(filename);
          }
        }
      }
    );

    #endregion

    // *******************************************************************************************
    // Public Methods
    // *******************************************************************************************

    #region Public Methods

    public WinUIService(IShellMenuService shellMenuService, ISettingsProvider settingsProvider,
      IEventAggregator eventAggregator, IAppSettings appSettings, IDialogService dialogService,
      IAppSettingsAdapter appSettingsAdapter, ILogger logger)
    {
      _logger = logger;
      _eventAggregator = eventAggregator;
      _configModuleSettings = settingsProvider.GetSettings<ConfigModuleSettings>();
      AppSettings = appSettings;
      _dialogService = dialogService;
      _appSettingsAdapter = appSettingsAdapter;

      eventAggregator.GetEvent<PromptUserToCloseAllDocumentsEvent>().Subscribe(PromptUserToCloseAllDocumentsEventHandler);

      if (_shellWindow == null)
      {
        _shellWindow = Application.Current?.MainWindow;
      }

      if (string.IsNullOrEmpty(AppSettings.FlewsSettings.ProjectHome))
      {
        this.ShowError($"Environment variable {nameof(AppSettings.FlewsSettings.ProjectHome)} is not set. This will cause problems!",
          caption: "Application Environment Not Set");
      }

      // tools menu items
      string serviceName = nameof(UIModule);

      shellMenuService.AddItem(EShellMenuNames.Tools, new SeparatorMenuItemViewModel() { Tag = "95", Component = serviceName });

      shellMenuService.AddItem(EShellMenuNames.Tools, new CustomMenuItemViewModel
      {
        Header = "Settings",
        Tag = "99",
        Command = ShowAppSettingsViewCommand,
        Component = serviceName,
        Icon = CreateMenuItemIcon("pack://application:,,,/FlewsApp.Common.Resources;component/Images/settings.png")
      });

      SubscribeEvents();

      _eventAggregator.GetEvent<ServiceLoadedEvent>().Publish(serviceName);
    }

    public void SetMainWindow(object windowObj) => _shellWindow = (Window)windowObj;

    public ButtonResult? ShowMessageBoxDialog(MessageBoxNotification notify)
    {
      return Application.Current?.Dispatcher.Invoke(() =>
      {
        //string resultData = null;
        ButtonResult result = ButtonResult.None;

        var callBack = new Action<IDialogResult>((dr) =>
        {
          if (dr != null)
          {
            //resultData = dr.Parameters.GetValue<string>("ResultData");
            result = (ButtonResult)dr.Result;
          }
        });

        var parameters = new DialogParameters
        {
          { "notify", notify }
        };

        _dialogService.ShowDialog(nameof(MessageBoxView), parameters, callBack);

        return result;
      });
    }

    public void ShowProgressBar(ProgressViewModel viewModel)
    {
      _eventAggregator.GetEvent<ProgressStarted>().Publish();
      Application.Current.Dispatcher?.Invoke(() =>
      {
        _progressWin = new ProgressWindow(viewModel, _eventAggregator) { Owner = _shellWindow };
        _progressWin.Show();
        _progressWin.Topmost = true;
      });
    }

    public void HideProgressBar()
    {
      _eventAggregator.GetEvent<ProgressEnded>().Publish();
      if (_progressWin != null)
      {
        Application.Current.Dispatcher?.Invoke(() =>
        {
          if (_progressWin != null)
          {
            _progressWin.Close();
            _progressWin = null;
          }
        });
      }
    }

    public ButtonResult PromptUserForText(string question, string defaultValue, out string? answer, bool leftClick = true)
    {
      string? resultData = null;
      ButtonResult result = ButtonResult.None;

      var callBack = new Action<IDialogResult>((dr) =>
      {
        if (dr != null)
        {
          resultData = dr.Parameters.GetValue<string>("Answer");
          result = (ButtonResult)dr.Result;
        }
      });

      var parameters = new DialogParameters
      {
        { "question", question },
        { "defaultValue", defaultValue }
      };

      _dialogService.ShowDialog(nameof(PromptUserForSingleValueView), parameters, callBack);

      answer = resultData;
      return result;
    }

    public ButtonResult PromptUserForChoice<T>(string title, string caption, IEnumerable<T?> options, T? initialChoice, out T? chosenOption)
    {
      ButtonResult buttonResult = ButtonResult.None;
      T? tmpChosenOption = default;

      var parameters = new DialogParameters
      {
        { nameof(PromptUserForChoiceViewModel.Caption), caption },
        { nameof(PromptUserForChoiceViewModel.InitialChoices), new T?[]{ initialChoice } },
        { nameof(PromptUserForChoiceViewModel.MultipleChoice), false },
        { nameof(PromptUserForChoiceViewModel.Options), options.OfType<object?>() },
        { nameof(PromptUserForChoiceViewModel.Title), title },
      };

      _dialogService.ShowDialog(nameof(PromptUserForChoiceView), parameters, dialogResult =>
      {
        if (dialogResult != null)
        {
          buttonResult = dialogResult.Result;
          tmpChosenOption = dialogResult.Parameters.GetValue<IEnumerable<object?>>(nameof(PromptUserForChoiceViewModel.ChosenOptions)).OfType<T>().FirstOrDefault();
        }
      });

      chosenOption = tmpChosenOption;
      return buttonResult;
    }

    public ButtonResult PromptUserForMultipleChoice<T>(string title, string caption, IEnumerable<T?> options, IEnumerable<T?> initialChoices, out IEnumerable<T?> chosenOptions)
    {
      ButtonResult buttonResult = ButtonResult.None;
      IEnumerable<T?> tmpChosenOptions = [];

      var parameters = new DialogParameters
      {
        { nameof(PromptUserForChoiceViewModel.Caption), caption },
        { nameof(PromptUserForChoiceViewModel.InitialChoices), initialChoices },
        { nameof(PromptUserForChoiceViewModel.MultipleChoice), true },
        { nameof(PromptUserForChoiceViewModel.Options), options.OfType<object?>() },
        { nameof(PromptUserForChoiceViewModel.Title), title },
      };

      _dialogService.ShowDialog(nameof(PromptUserForChoiceView), parameters, dialogResult =>
      {
        if (dialogResult != null)
        {
          buttonResult = dialogResult.Result;
          tmpChosenOptions = dialogResult.Parameters.GetValue<IEnumerable<object?>>(nameof(PromptUserForChoiceViewModel.ChosenOptions)).OfType<T>();
        }
      });

      chosenOptions = tmpChosenOptions;
      return buttonResult;
    }

    public string? OpenFileDialog(string defaultPath, string defaultExt, string filter, string? filename = null,
                                 string? caption = null)
    {
      OpenFileDialog dialog = new OpenFileDialog()
      {
        AddExtension = !string.IsNullOrEmpty(defaultExt),
        DefaultExt = defaultExt,
        Filter = filter,
        InitialDirectory = defaultPath,
        Title = caption,
        FileName = filename
      };

      if (dialog.ShowDialog() == true)
        return dialog.FileName;

      return null;
    }

    public IEnumerable<string> OpenFilesDialog(string defaultPath, string defaultExt, string filter, string? filename = null,
                             string? caption = null)
    {
      OpenFileDialog dialog = new OpenFileDialog()
      {
        AddExtension = !string.IsNullOrEmpty(defaultExt),
        DefaultExt = defaultExt,
        Filter = filter,
        InitialDirectory = defaultPath,
        Title = caption,
        FileName = filename,
        Multiselect = true
      };

      if (dialog.ShowDialog() == true)
        return dialog.FileNames;

      return Enumerable.Empty<string>();
    }


    public string? SaveFileDialog(string defaultPath, string defaultExt, string filter, string? filename = null,
                                 string? caption = null, bool overwritePrompt = true)
    {
      // NOTE: This creates and deletes a file to check for valid filenames which doesn't work for us
      // as we usually have a file system watcher monitoring the folder
      SaveFileDialog dialog = new SaveFileDialog()
      {
        AddExtension = !string.IsNullOrEmpty(defaultExt),
        DefaultExt = defaultExt,
        Filter = filter,
        InitialDirectory = defaultPath,
        Title = caption,
        FileName = filename,
        OverwritePrompt = overwritePrompt
      };

      if (dialog.ShowDialog() == true)
        return dialog.FileName;

      return null;
    }

    public bool? OpenFolderDialog(string defaultPath, out string? filename, string? caption = null)
    {
      OpenFolderDialog dialog = new OpenFolderDialog()
      {
        InitialDirectory = defaultPath,
        Title = caption,
        ValidateNames = true,
        AddToRecent = false,
      };

      var result = dialog.ShowDialog();
      filename = result == true ? dialog.FolderName : null;
      return result;
    }

    public IEnumerable<DirectoryInfo> OpenFoldersDialog(string defaultPath, string? caption = null)
    {
      OpenFolderDialog dialog = new OpenFolderDialog()
      {
        InitialDirectory = defaultPath,
        Title = caption,
        ValidateNames = true,
        AddToRecent = false,
        Multiselect = true
      };

      return dialog.ShowDialog() switch
      {
        true => dialog.FolderNames.Select(i => new DirectoryInfo(i)),
        _ => Array.Empty<DirectoryInfo>()
      };
    }

    public object CreateMenuItem(ContextMenuItem fmi)
    {
      switch (fmi)
      {
        case SeperatorContextMenuItem _:
          return new Separator();

        case NodeColourContextMenuItem nodeColorMI:
          {
            ColorPicker picker = new ColorPicker
            {
              Width = 100,
              Margin = new Thickness(-30, 0, -60, 0)
            };

            picker.SetBinding(ColorPicker.SelectedColorProperty, nodeColorMI.BindingSrc);

            var changeNodeColourMenuItem = new MenuItem
            {
              Header = fmi.Header,
              Icon = CreateMenuItemIcon(nodeColorMI.UriStr)
            };

            changeNodeColourMenuItem.Items.Add(picker);
            return changeNodeColourMenuItem;
          }

        case MultiContextMenuItem mmi:
          {
            var mainMenuItem = new MenuItem
            {
              Header = mmi.Header,
              Icon = CreateMenuItemIcon(mmi.UriStr)
            };

            foreach (var smi in mmi.SubMenuItems)
            {
              var newSubMenuItem = new MenuItem
              {
                Header = smi.Item1,
              };

              var handler = new RoutedEventHandler(smi.Item2);
              newSubMenuItem.Click += handler;
              mainMenuItem.Items.Add(newSubMenuItem);
            }

            return mainMenuItem;
          }

        default:
          return new MenuItem
          {
            Header = fmi.Header,
            Command = fmi.Command,
            CommandParameter = fmi.CommandParameter,
            Icon = CreateMenuItemIcon(fmi.UriStr)
          };
      }
    }

    public object CreateMenuItem(string header, ICommand command, string uriStr, object? commandParameter = null)
    {
      return new MenuItem
      {
        Header = header,
        Command = command,
        CommandParameter = commandParameter,
        Icon = CreateMenuItemIcon(uriStr)
      };
    }

    public void ShowContextMenu(List<IContextMenuItem> cmItems)
    {
      var cm = new ContextMenu { FontSize = 10 };
      cmItems.ForEach(fmi => cm.Items.Add(CreateMenuItem((ContextMenuItem)fmi)));

      if (cm.Items.Count > 0)
        cm.IsOpen = true; //diplay the context menu
    }

    public bool BindingEnableVisual(object visualObject, object bindToObject, string propertyName)
    {
      if (visualObject == null)
        return false;

      var binding = new Binding("IsEditable") { Source = bindToObject };
      BindingEnableVisual((Visual)visualObject, binding);
      return true;
    }

    public void CompareFilesInExternalViewer(string file1, string file2)
    {
      // Try to run WinMerge to compare the two files..
      try
      {
        FlewsProcess mWinMergeApp = FlewsProcess.GetWinMergeProcessInfo();
        mWinMergeApp.Arguments = $"{file1} {file2}";

        // start the external viewer
        if (mWinMergeApp.Initialise(null))
          mWinMergeApp.Start(true);
        else
        {
          this.ShowError("Bad WinMerge app info", "Failed to run WinMerge application");
        }
      }
      catch (Exception ex)
      {
        this.ShowException("Failed to run WinMerge application", ex, true);
      }
    }

    public bool RunProcess(string name, string target, string location, string type, string comment, string arguments)
    {
      FlewsProcess process = new FlewsProcess()
      {
        Name = name,
        Target = target,
        TargetLocation = location,
        Comment = comment,
        Arguments = arguments
      };

      Enum.TryParse(type, out tTargetType processType);
      process.TargetType = processType;

      // start the external viewer
      if (process.Initialise(null))
      {
        process.Start(true);
        return File.Exists(process.Command);
      }

      return false;
    }

    public void OpenFileInExternalViewer(IFileDocument fileNode)
    {
      // create this Data Node's external editor process if it hasn't already 
      var ext = fileNode.FullFileName != null ? Path.GetExtension(fileNode.FullFileName).ToLower() : null;
      FlewsProcess externalViewer;
      switch (ext)
      {
        case ".ext":
          {
            externalViewer = FlewsProcess.GetXMLEditorProcessInfo();
            externalViewer.Arguments = fileNode.FullFileName;
            break;
          }
        case ".3ds":
          {
            string compileFlag = string.Empty;
#if DEBUG
            compileFlag = "Debug";
#else
            compileFlag = "Release";
#endif
            // We can send 3D Models to John's 3D Model editor
            string modelViewerFn = $"{AppSettings.FlewsSettings.ProjectHome}\\Bin\\{compileFlag}\\ModelEditorDX9.exe";
            if (File.Exists(modelViewerFn))
            {
              externalViewer = new FlewsProcess()
              {
                Name = "ModelEditorDX9",
                Target = "ModelEditorDX9.exe",
                TargetLocation = $"%{AppSettings.FlewsSettings.ProjectHome}%\\Bin\\{compileFlag}",
                TargetType = tTargetType.APPLICATION,
                Comment = "Opens the FLEWS ModelEditorDX9 Viewer"
              };
            }
            else
              externalViewer = FlewsProcess.GetDefaultProcessInfo();

            externalViewer.Arguments = fileNode.FullFileName;
            break;
          }
        case ".tmp": // RunConfigs use this extenstion for their xml file
        case ".xml":
          {
            externalViewer = FlewsProcess.GetNotepadPlusPlusProcessInfo();
            externalViewer.Arguments = fileNode.FullFileName;
            break;
          }
        default:
          {
            // create a default external viewer, if necessary
            externalViewer = FlewsProcess.GetDefaultProcessInfo();
            break;
          }
      }

      if (!externalViewer.Initialise(null))
        return;

      // wrap the arguments inside a "" in case we have spaces inside the path
      if (externalViewer.Arguments != null && externalViewer.Arguments.Contains(" "))
        externalViewer.WrapArgumentinQuotes = true;

      externalViewer.Start(true);

      // check success, if failed, just open the file location instead
      if (File.Exists(externalViewer.Command) == false && fileNode.CanOpenItemLocation())
        fileNode.OpenItemLocation();
    }

    public void OpenSchemaFileInExternalViewer(IFileDocument fileNode)
    {
      string? xsdFileName = Common.Infrastructure.HelperFns.GetXMLSchemaFilename(AppSettings.FlewsSettings.SchemaPath, fileNode.FullFileName);

      FlewsProcess xsdViewer = FlewsProcess.GetNotepadPlusPlusProcessInfo();

      xsdViewer.Arguments = xsdFileName;
      if (!xsdViewer.Initialise(null))
        return;

      // wrap the arguments inside a "" in case we have spaces inside the path
      if (xsdViewer.Arguments != null && xsdViewer.Arguments.Contains(" "))
        xsdViewer.WrapArgumentinQuotes = true;

      // start the external viewer
      xsdViewer.Start(true);

      // check result - if failed, open xsd file location for the user to check
      if (File.Exists(xsdViewer.Command) == false && fileNode.CanOpenItemLocation())
        fileNode.OpenItemLocation();
    }

    public Image CreateMenuItemIcon(string? uriStr)
    {
      if (string.IsNullOrEmpty(uriStr))
      {
        return new Image
        {
          Width = 15,
        };
      }

      BitmapImage logo = new BitmapImage();
      logo.BeginInit();
      logo.UriSource = new Uri(uriStr, UriKind.RelativeOrAbsolute);
      logo.EndInit();

      return new Image
      {
        Width = 15,
        Source = logo
      };
    }

    public void AddWindowInputBinding(InputBinding binding)
    {
      _shellWindow?.InputBindings.Add(binding);
    }

    public void RemoveWindowInputBinding(InputBinding binding)
    {
      _shellWindow?.InputBindings.Remove(binding);
    }

    #region ScreenShotHelper fns

    public void SaveScreenShotToFile(FrameworkElement element, string defaultFilenameNoExt)
    {
      var dialog = new SaveFileDialog
      {
        // get the application's folder location for temporary files
        InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        DefaultExt = ".png",
        Filter = "PNG|*.png",
        Title = "Save to file.."
      };
      // set a default file name
      if (defaultFilenameNoExt == null)
      {
        dialog.FileName = "Screenshot.png";
      }
      else
      {
        dialog.FileName = $"{defaultFilenameNoExt}.png";
      }

      if (dialog.ShowDialog().GetValueOrDefault(false))
      {
        var bitmap = ScreenshotHelper.CreateElementScreenshot(element, 300);
        ScreenshotHelper.SaveBitmapToFile(bitmap, dialog.FileName, 300);
      }
    }

    public void CopyFrameworkElemenToPNGFile(FrameworkElement copyTarget, double width, double height, string filename, bool append)
    {
      var pngEncoder = new PngBitmapEncoder();
      using FileStream fileSteam = new FileStream(filename, append ? FileMode.Append : FileMode.Create);
      CopyFrameworkElementToStream(copyTarget, width, height, pngEncoder, fileSteam, 300.0);
    }

    public void CopyFrameworkElemenToBMPFile(FrameworkElement copyTarget, double width, double height, string filename, bool append)
    {
      BitmapEncoder bmpEncoder = new BmpBitmapEncoder();
      using var fileSteam = new FileStream(filename, append ? FileMode.Append : FileMode.Create);
      CopyFrameworkElementToStream(copyTarget, width, height, bmpEncoder, fileSteam, 300.0);
    }

    public Stream CopyFrameworkElementToStream(FrameworkElement copyTarget,
              double width, double height, BitmapEncoder enc, Stream outStream, double? dpi)
    {
      if (copyTarget == null)
        return outStream;

      // Store the Frameworks current layout transform, as this will be restored later
      Transform storedTransform = copyTarget.LayoutTransform;

      // Set the layout transform to unity to get the nominal width and height
      copyTarget.LayoutTransform = new ScaleTransform(1, 1);
      copyTarget.UpdateLayout();

      double baseHeight = copyTarget.ActualHeight;
      double baseWidth = copyTarget.ActualWidth;

      // Now scale the layout to fit the bitmap
      copyTarget.LayoutTransform =
      new ScaleTransform(baseWidth / width, baseHeight / height);
      copyTarget.UpdateLayout();

      // Render to a Bitmap Source, the DPI is changed for the 
      // render target as a way of scaling the FrameworkElement
      var rtb = new RenderTargetBitmap(
        (int)width,
        (int)height,
        96d * width / baseWidth,
        96d * height / baseHeight,
        PixelFormats.Default);

      rtb.Render(copyTarget);

      // Convert from a WinFX Bitmap Source to a Win32 Bitamp

      if (dpi == null)
      {
        enc.Frames.Add(BitmapFrame.Create(rtb));
      }
      else
      {
        // Change the DPI

        ImageBrush brush = new ImageBrush(BitmapFrame.Create(rtb));

        var rtbDpi = new RenderTargetBitmap(
          (int)width, // PixelWidth
          (int)height, // PixelHeight
          (double)dpi, // DpiX
          (double)dpi, // DpiY
          PixelFormats.Default);

        DrawingVisual drawVisual = new DrawingVisual();
        using (DrawingContext dc = drawVisual.RenderOpen())
        {
          dc.DrawRectangle(brush, null,
          new Rect(0, 0, rtbDpi.Width, rtbDpi.Height));
        }

        rtbDpi.Render(drawVisual);

        enc.Frames.Add(BitmapFrame.Create(rtbDpi));
      }
      enc.Save(outStream);
      // Restore the Framework Element to it's previous state
      copyTarget.LayoutTransform = storedTransform;
      copyTarget.UpdateLayout();

      return outStream;
    }

    #endregion

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

    private void SubscribeEvents()
    {
      if (Application.Current == null)
        return;

      _eventAggregator.GetEvent<PopupWindowEvent>().Subscribe(p =>
      {
        PopupWindow.Show(_appSettingsAdapter, p.Message, p.Window as Window, p.Duration);
      }, ThreadOption.UIThread);

      // &&& NOTE: Really would prefer this to be on the Publisher thread but can't get it to work
      // properly when executed from a background thread. e.g. open scenario config file in external viewer
      _eventAggregator.GetEvent<WaitWindowEvent>().Subscribe(e =>
      {
        DoWaitWindow(e);
      }, ThreadOption.UIThread);

      _eventAggregator.GetEvent<StringNotificationPopupEvent>().Subscribe(message =>
      {
        this.ShowMessage(message, DialogButtons.OK, DialogIcon.Information,
                               ButtonResult.None, "Flews Message");

      }, ThreadOption.PublisherThread);

      // Unhandled ExceptionEvent
      // In the case of an unhandled exception we can pop up a Notification to the user
      _eventAggregator.GetEvent<RaisedExceptionEvent>().Subscribe(e =>
      {
        this.ShowException(e.Message, e.TheException, e.LogIt);

      }, ThreadOption.PublisherThread);

      // Cancel adding the "Developer" Top Level Menu item if not running as Developer
      _eventAggregator.GetEvent<FilterTopLevelMenuEvent>().Subscribe(CancelDeveloperTopLevelMenu, ThreadOption.PublisherThread);
    }

    /// <summary>
    /// Cancel adding the "Developer" Top Level Menu item if not running as Developer
    /// </summary>
    /// <param name="args"></param>
    private void CancelDeveloperTopLevelMenu(FilterTopLevelMenuArgs args)
    {
      args.Cancel = (args.TopLevelMenuName == EShellMenuNames.Developer.ToString() && !_configModuleSettings.RunAsDeveloper);
    }

    private void DoWaitWindow(WaitWindowEventArgs e)
    {
      if (e.Show) // true = show wait window
      {
        if (!_configModuleSettings.ShowWaitWindow)
          return;

        var workingWindow = (Window?)e.Window ?? _shellWindow ?? Application.Current.MainWindow;
        var workingWindowRect = workingWindow.GetWindowRect();

        if (Interlocked.Increment(ref _waitWindowCount) == 1)
        {
          EventWaitHandle waitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
          Thread thread = new Thread(() =>
          {
            var waitWindow = new WaitWindow();
            waitWindow.Show();
            waitWindow.Closed += (_, __) => waitWindow.Dispatcher.InvokeShutdown();
            waitWindow.Left = (workingWindowRect.X + (workingWindowRect.Width - waitWindow.Width)/2);
            waitWindow.Top = (workingWindowRect.Y + (workingWindowRect.Height - waitWindow.Height)/2);
            _lastInstance = waitWindow;
            waitHandle.Set();
            System.Windows.Threading.Dispatcher.Run();
          });
          thread.SetApartmentState(ApartmentState.STA);
          thread.Start();
          waitHandle.WaitOne();
        }
      }
      else // close wait window
      {
        if (!_configModuleSettings.ShowWaitWindow)
          return;

        if (_lastInstance != null && Interlocked.Decrement(ref _waitWindowCount) == 0)
        {
          _lastInstance.Dispatcher.Invoke(() => _lastInstance.Close());
        }
      }
    }

    private void BindingEnableVisual(Visual _visual, Binding binding)
    {
      for (int i = 0; i < VisualTreeHelper.GetChildrenCount(_visual); i++)
      {
        // Retrieve child visual at specified index value.
        Visual childVisual = (Visual)VisualTreeHelper.GetChild(_visual, i);

        bool isEditControl = false;

        isEditControl |= childVisual is TextBox;
        isEditControl |= childVisual is TextBlock; // no effect because TextBlock does have different template for not enabled
        isEditControl |= childVisual is Button;
        isEditControl |= childVisual is ComboBox;
        isEditControl |= childVisual is CheckBox;
        isEditControl |= childVisual is ListView;
        isEditControl |= childVisual is DataGrid;

        if (isEditControl)
        {
          if (childVisual is FrameworkElement visual)
          {
            // Some framework element always need to be false - we need to check for these
            switch (visual.Name)
            {
              case "MediumTypeTbDisabled":
              case "RadioConfigCbDisabled":
              case "AgentTypeTbDisabled":
              case "AgentControlTypeTbDisabled":
              case "AgentAuthorTbDisabled":
              case "FilePathTbDisabled":
              case "PlatformNameTbDisabled":
              case "NameTbDisabled":
              case "NumNodesTbDisabled":
              case "WeaponTypeTbDisabled":
              case "WeaponConfigTbDisabled":
                continue;
              default:
                break;
            }

            //_logger.LogDebug($"setting binding on {visual.GetType().ToString()} - {visual.Name}",
            //  Category.Debug, Priority.Medium);
            visual.SetBinding(UIElement.IsEnabledProperty, binding);
          }
        }

        // Enumerate children of the child visual object.
        BindingEnableVisual(childVisual, binding);
      }
    }

    private T GetEntryAssemblyAttribute<T>() where T : Attribute
    {
      if (Assembly.GetEntryAssembly() != null)
      {
        if (Attribute.GetCustomAttribute(Assembly.GetEntryAssembly()!, typeof(T), false) is T att)
        {
          return (T)att;
        }
      }
      throw new NotImplementedException($"Unable to get assembly attribute of type {typeof(T)}");
    }

    // Fallback handler for this event as it is required for proper sequence of functionality to work
    // NOTE: This code is to be replaced by a proper request/wait-for-reply event system
    private void PromptUserToCloseAllDocumentsEventHandler()
    {
      if (!UI.HelperFns.IsInRequestSaveViewsMode)
      {
        UI.HelperFns.IsInRequestSaveViewsMode = true;
        UI.HelperFns.CanCloseAllDocuments = true;
      }
    }

    #endregion

    // *******************************************************************************************
    // Disposal Support
    // *******************************************************************************************

    #region IDisposable Support

    private bool _disposedValue = false; // To detect redundant calls

    protected virtual void Dispose(bool disposing)
    {
      if (!_disposedValue)
      {
        if (disposing)
        {
          // TODO: dispose managed state (managed objects).
        }

        // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
        // TODO: set large fields to null.

        _disposedValue = true;
      }
    }

    // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
    ~WinUIService()
    {
      // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
      Dispose(true);
    }

    // This code added to correctly implement the disposable pattern.
    public void Dispose()
    {
      // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
      Dispose(true);
      // TODO: uncomment the following line if the finalizer is overridden above.
      GC.SuppressFinalize(this);
    }

    #endregion
  }
}
