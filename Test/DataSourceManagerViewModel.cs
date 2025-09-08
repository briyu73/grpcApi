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

using FlewsApp.Modules.DataSource.Data.Settings;
using FlewsApp.Modules.DataSource.Exceptions;
using FlewsApp.Modules.DataSource.Interface;
using FlewsApp.Modules.UI;
using FlewsApp.Modules.UI.Interface;
using FlewsApp.Modules.UI.Messaging;
using FlewsApp.Modules.UI.ViewModels;
using FlewseDotNetLib.Settings;
using Mad.Libraries.Settings.Interface;
using Mad.Libraries.ShellBase.Events;
using Microsoft.Win32;
using Prism.Events;
using Prism.Services.Dialogs;
using Swordfish.NET.General;
using Swordfish.NET.ViewModel;
using System;
using System.ComponentModel;
using System.Linq;
using System.Security;
using System.Windows;
using System.Windows.Input;

namespace FlewsApp.Modules.DataSource.UI.ViewModels
{
  public class DataSourceManagerViewModel : FlewsDialogViewModelBase
  {
    // *******************************************************************************************
    // Fields
    // *******************************************************************************************

    #region Fields

    private readonly IDataSourceManager _dataSourceManager;
    private readonly IEventAggregator _eventAggregator;
    private readonly IFlewsUIService _flewsUIService;
    private readonly IAppSettings _appSettings;

    private readonly EventHandler<PropertyChangedEventArgs> _sourcePropertyChanged;

    private string _prevSelectedDataSourceName = string.Empty;

    #endregion Fields

    // *******************************************************************************************
    // Properties
    // *******************************************************************************************

    #region Properties

    private DataSourceModuleSettings _settings;
    public DataSourceModuleSettings Settings
    {
      get { return _settings; }
      set
      {
        var oldSettings = _settings;
        if (SetProperty(ref _settings, value))
        {
          AddSettingsChangeHandlers(oldSettings, _settings);
        }
      }
    }

    private DataSourceSettings? _selectedSource = null;
    public DataSourceSettings? SelectedSource
    {
      get { return _selectedSource; }
      set
      {
        if (SetProperty(ref _selectedSource, value))
        {
          ModifySourceCommand.OnCanExecuteChanged();
        }
      }
    }

    private bool _hasUnappliedChanges = false;
    public bool HasUnappliedChanges
    {
      get { return _hasUnappliedChanges; }
      set => SetProperty(ref _hasUnappliedChanges, value);
    }

    private bool _isEditingSource = false;
    public bool IsEditingSource
    {
      get { return _isEditingSource; }
      set { SetProperty(ref _isEditingSource, value); }
    }

    private EDataSourceType _sourceType = EDataSourceType.DATABASE_POSTGRES;
    public EDataSourceType SourceType
    {
      get { return _sourceType; }
      set
      {
        // if a new source is being created then the Type can be changed
        // and when it's changed a new DataSourceSetting needs to be made
        // and assigned to NewSourceSettings
        //
        // If a source is being edited, then SourceType should not be
        // allowed to be changed - the UI should display it as read only
        if (SetProperty(ref _sourceType, value)
          && IsNewSource)
        {
          CreateDataSettings(SourceType);
        }
      }
    }

    private DataSourceSettings? _newSourceSettings = null;
    public DataSourceSettings? NewSourceSettings
    {
      get { return _newSourceSettings; }
      set
      {
        if (SetProperty(ref _newSourceSettings, value)
          && value != null)
        {
          SourceType = value.SourceType;
        }
      }
    }

    private string _adminName = "postgres";
    public string AdminName
    {
      get { return _adminName; }
      set { SetProperty(ref _adminName, value); }
    }

    private string _adminPasswordEncrypted = string.Empty;
    public string AdminPasswordEncrypted
    {
      get { return _adminPasswordEncrypted; }
      set { SetProperty(ref _adminPasswordEncrypted, value); }
    }

    private bool _isNewSource = false;
    public bool IsNewSource
    {
      get { return _isNewSource; }
      set
      {
        if (SetProperty(ref _isNewSource, value))
        {
          RaisePropertyChanged(nameof(EnableDataSourceResetting));
          RaisePropertyChanged(nameof(CancelSourceButtonText));
        }
      }
    }

    private bool _isWorking = false;
    public bool IsWorking
    {
      get { return _isWorking; }
      set
      {
        if (SetProperty(ref _isWorking, value))
        {
          RaisePropertyChanged(nameof(EnableDataSourceResetting));
        }
      }
    }

    public SecureString SourcePassword
    {
      set
      {
        if (NewSourceSettings is SqlDataSourceSettings sql)
        {
          sql.PasswordEncrypted = CryptoUtils.EncryptString(value, System.Security.Cryptography.DataProtectionScope.LocalMachine);
        }
      }
    }

    public SecureString AdminPassword
    {
      set
      {
        AdminPasswordEncrypted = CryptoUtils.EncryptString(value, System.Security.Cryptography.DataProtectionScope.LocalMachine);
      }
    }

    public bool EnableDataSourceResetting => !IsWorking && !IsNewSource;

    public string CancelSourceButtonText => IsNewSource ? "Cancel" : "Done";

    private bool _expandAdminFunctions;
    public bool ExpandAdminFunctions
    {
      get { return _expandAdminFunctions; }
      set { SetProperty(ref _expandAdminFunctions, value); }
    }

    #endregion

    // *******************************************************************************************
    // Commands
    // *******************************************************************************************

    #region Commands

    private readonly RelayCommandFactory _applyChangesCommand = new RelayCommandFactory();
    public ICommand ApplyChangesCommand => _applyChangesCommand.GetCommand(
      p =>
      {
        // Only allow 1 selected data source for now
        if (Settings.Sources.Count(i => i.Enabled) > 1)
        {
          _flewsUIService.ShowError("Must have only 1 data source Enabled.");
          return;
        }

        if (_flewsUIService.AppSettings.Adapter.ApplicationName == "DataLibrary Editor")
        {
          // Any modules with with open views can listen to this event if they need to check
          // with user if it is ok to close the views prior to changing databases
          _eventAggregator.GetEvent<PromptUserToCloseAllDocumentsEvent>().Publish();

          _eventAggregator.GetEvent<RequestCloseDocumentViewsEvent>().Publish(
            new RequestViewCloseEventArgs { ViewSelector = view => true, ViewModelSelector = vm => true });

          if (HelperFns.IsInRequestSaveViewsMode)
          {
            if (HelperFns.CanCloseAllDocuments)
            {
              ApplyDataSourceChanges();
            }
            else
            {
              // revert back to previously selected data source
              var prevDataSource = Settings.Sources.FirstOrDefault(s => s.SourceName == _prevSelectedDataSourceName);
              if (prevDataSource != null)
              {
                prevDataSource.Enabled = true;
              }
              Settings.Sources.Where(s => s.SourceName != _prevSelectedDataSourceName).ForEach(s => s.Enabled = false);
              HasUnappliedChanges = false;
            }
          }
          else
          {
            ApplyDataSourceChanges();
          }

          // reset flag after having prompted user 
          HelperFns.IsInRequestSaveViewsMode = false;
          HelperFns.CanCloseAllDocuments = false;
        }
        else
        {
          ApplyDataSourceChanges();
        }

        void ApplyDataSourceChanges()
        {
          IsWorking = true;
          System.Threading.Tasks.Task.Run(() =>
          {
            try
            {
              _dataSourceManager.UpdateSettings(Settings);
            }
            finally
            {
              IsWorking = false;
              HasUnappliedChanges = false;
            }
          });
        }
      }
    );

    private readonly RelayCommandFactory _newSourceCommand = new RelayCommandFactory();
    public ICommand NewSourceCommand => _newSourceCommand.GetCommand(
      p =>
      {
        IsEditingSource = true;
        IsNewSource = true;
        CreateDataSettings(EDataSourceType.DATABASE_POSTGRES);
      });

    private readonly RelayCommandFactory _modifySourceCommand = new RelayCommandFactory();
    public RelayCommand ModifySourceCommand => _modifySourceCommand.GetCommand(
      p =>
      {
        IsEditingSource = true;
        IsNewSource = false;
        NewSourceSettings = SelectedSource;
        SourceType = NewSourceSettings!.SourceType;
        HasUnappliedChanges = NewSourceSettings.Enabled;
      },
      p => SelectedSource != null);

    private readonly RelayCommandFactory _cancelSourceCommand = new RelayCommandFactory();
    public ICommand CancelSourceCommand => _cancelSourceCommand.GetCommand(
      p =>
      {
        IsEditingSource = false;
        IsNewSource = false;
      });

    private readonly RelayCommandFactory _saveSourceCommand = new RelayCommandFactory();
    public ICommand SaveSourceCommand => _saveSourceCommand.GetCommand(
      p =>
      {
        if (IsNewSource && NewSourceSettings != null)
        {
          Settings.Sources.Add(NewSourceSettings);

          // if this is the only data source, then default it to enabled
          // do this *after* it's been added to the Sources collection so
          // this ViewModel will pick up the change to the .Disabled property
          if (Settings.Sources.Count == 1)
          {
            NewSourceSettings.Enabled = true;
          }
        }
        IsEditingSource = false;
        NewSourceSettings = null;
        IsNewSource = false;
      });

    private readonly RelayCommandFactory _removeSourceCommand = new RelayCommandFactory();
    public ICommand RemoveSourceCommand => _removeSourceCommand.GetCommand(
      p =>
      {
        if (p is DataSourceSettings source)
        {
          _dataSourceManager.RemoveDataSourceSetting(source);

          if (source.Enabled)
          {
            // If the removed source was the enabled one, then the user needs to select
            // a different one and 'apply' the changes, otherwise there's no source enabled
            HasUnappliedChanges = true;
          }
        }
      });

    private readonly RelayCommandFactory _resetDataSourceCommand = new RelayCommandFactory();
    public ICommand ResetDataSourceCommand => _resetDataSourceCommand.GetCommand(
      p =>
      {
        if (SelectedSource == null)
        {
          _flewsUIService.ShowMessage("No DataSource selected!", DialogButtons.OK, DialogIcon.Exclamation);
          return;
        }

        var result = _flewsUIService.PromptUserForText("This will DELETE ALL DATA in this data source. Type DELETE to continue.", "", out var response);

        if (result == ButtonResult.OK)
        {
          if (response != "DELETE")
          {
            _flewsUIService.ShowMessage("Nothing deleted, response must match exactly to reset data.", DialogButtons.OK, DialogIcon.Information);
            return;
          }

          var progressViewModel = new ProgressViewModel(_eventAggregator, _flewsUIService, "Clearing data source", true, false, true);
          progressViewModel.Worker.DoWork += (sender, args) =>
          {
            IsWorking = true;
            try
            {
              progressViewModel.Text = "Refreshing data source";

              _dataSourceManager.ResetDataSourceSetting(SelectedSource);

              _flewsUIService.HideProgressBar();
              _flewsUIService.ShowMessage("Reset Complete!", DialogButtons.OK, DialogIcon.Information);
            }
            catch (Exception ex)
            {
              _flewsUIService.ShowException($"There was an error while resetting the data source.", ex, true);
            }
            finally
            {
              if (!SelectedSource.Enabled)
              {
                // Require the user to 'apply' changes if the enabled data source
                // was reset - this is probably not always required, but it's the safe option
                HasUnappliedChanges = true;
              }
              IsWorking = false;
            }
          };

          progressViewModel.Run(progressViewModel, true);
        }
      });

    private static readonly RelayCommandFactory _setDirectoryCommand = new RelayCommandFactory();
    public static ICommand SetDirectoryCommand => _setDirectoryCommand.GetCommand(
      p =>
      {
        if (p is FileDataSourceSettings fileSettings)
        {
          var sfd = new OpenFolderDialog()
          {
            ValidateNames = true,
          };
          if (sfd.ShowDialog() == true)
          {
            fileSettings.Directory = sfd.FolderName;
          }
        }
      });

    private readonly RelayCommandFactory _testUserConnectionCommand = new RelayCommandFactory();
    public ICommand TestUserConnectionCommand => _testUserConnectionCommand.GetCommand(
      p =>
      {
        if (NewSourceSettings != null)
        {
          try
          {
            _dataSourceManager.TestUserConnection(NewSourceSettings);
            _flewsUIService.ShowMessage("Connection test successful.", DialogButtons.OK, DialogIcon.Information);
          }
          catch (MigrationException)
          {
            _flewsUIService.ShowMessage("Connection test successful, there are pending migrations", DialogButtons.OK, DialogIcon.Warning);
          }
          catch (Exception ex)
          {
            _flewsUIService.ShowException("Connection test failed.", ex, true);
          }
        }
      });

    private readonly RelayCommandFactory _createDatabaseCommand = new RelayCommandFactory();
    public ICommand CreateDatabaseCommand => _createDatabaseCommand.GetCommand(
      p =>
      {
        if (!(NewSourceSettings is SqlDataSourceSettings userSettings))
        {
          _flewsUIService.ShowError("No SQLDataSource settings available.");
          return;
        }
        if (string.IsNullOrWhiteSpace(userSettings.UserName))
        {
          _flewsUIService.ShowError("Username cannot be empty.");
          return;
        }
        if (string.IsNullOrWhiteSpace(userSettings.DatabaseName))
        {
          _flewsUIService.ShowError("User DatabaseName cannot be empty.");
          return;
        }

        try
        {
          _dataSourceManager.CreateDatabase(userSettings, AdminName, AdminPasswordEncrypted);

          _flewsUIService.ShowMessage($"Created database '{userSettings.DatabaseName}' for user '{userSettings.UserName}'.", DialogButtons.OK, DialogIcon.Information, caption: "Success");
        }
        catch (Exception ex)
        {
          _flewsUIService.ShowException("Failed to create database", ex, true);
        }
      });

    private readonly RelayCommandFactory _createUserCommand = new RelayCommandFactory();
    public ICommand CreateUserCommand => _createUserCommand.GetCommand(
      p =>
      {
        if (!(NewSourceSettings is SqlDataSourceSettings userSettings))
        {
          _flewsUIService.ShowError("No SQLDataSource settings available.");
          return;
        }

        if (string.IsNullOrWhiteSpace(userSettings.UserName))
        {
          _flewsUIService.ShowError("Username cannot be empty.");
          return;
        }

        if (string.IsNullOrWhiteSpace(userSettings.PasswordEncrypted))
        {
          _flewsUIService.ShowError("User Password cannot be empty.");
          return;
        }

        try
        {
          if (_dataSourceManager.CreateUser(userSettings, AdminName, AdminPasswordEncrypted))
          {
            _flewsUIService.ShowMessage($"User '{userSettings.UserName}' created.", DialogButtons.OK, DialogIcon.Information, caption: "Success");
          }
          else
          {
            var txt = $"User '{userSettings.UserName}' already exists.";
            if (_dataSourceManager.ShowCreateUserView)
            {
              txt += $"{Environment.NewLine}Role privileges has been updated as required. Changes will take effect when application is restarted.";
              _dataSourceManager.ShowCreateUserView = false;
              ExpandAdminFunctions = false;
            }

            _flewsUIService.ShowMessage(txt, DialogButtons.OK, DialogIcon.Information, caption: "Success");
          }

        }
        catch (Exception ex)
        {
          _flewsUIService.ShowException($"Failed to create user.", ex, true);
        }
      });

    private readonly RelayCommandFactory _selectLiteDbFileCommand = new RelayCommandFactory();
    public ICommand SelectLiteDbFileCommand => _selectLiteDbFileCommand.GetCommand(
      p =>
      {
        var dbFile = _flewsUIService.SaveFileDialog($"{_appSettings.FlewsSettings.FlewsData}\\DataSources",
          "litedb", "LiteDB Files|*.litedb", null, "Select a file for the LiteDB database");
        if (dbFile != null
          && NewSourceSettings is LiteDbDataSourceSettings liteSettings)
        {
          liteSettings.Filename = dbFile;
        }
      });

    private readonly RelayCommandFactory _unTickOtherSettingsBoxesCommand = new RelayCommandFactory();
    public ICommand UnTickOtherSettingsBoxesCommand => _unTickOtherSettingsBoxesCommand.GetCommand(
      p =>
      {
        var sourceSettings = (DataSourceSettings)p!;

        Settings.Sources.Where(x => x.SourceName != sourceSettings.SourceName).ToArray().ForEach(s => s.Enabled = false);

      }, p => p is DataSourceSettings);

    #endregion

    // *******************************************************************************************
    // Public Methods
    // *******************************************************************************************

    #region Public Methods

    public DataSourceManagerViewModel(IDataSourceManager dataSourceManager, IEventAggregator eventAggregator,
      IFlewsUIService flewsUIService, IAppSettings appSettings, DryIoc.IContainer container,
      ISettingsProvider settingsProvider)
      : base(container)
    {
      _dataSourceManager = dataSourceManager;
      _eventAggregator = eventAggregator;
      _flewsUIService = flewsUIService;
      _appSettings = appSettings;

      _sourcePropertyChanged = new EventHandler<PropertyChangedEventArgs>(SourcePropertyChangedHandler);
      Title = "Data Source Manager";

      _settings = dataSourceManager.Settings;
      AddSettingsChangeHandlers(null, _settings);

      _prevSelectedDataSourceName = _settings.Sources.SingleOrDefault(s => s.Enabled)?.SourceName ?? string.Empty;

      if (_dataSourceManager.ShowCreateUserView)
      {
        SelectedSource = Settings.Sources.FirstOrDefault(s => s.Enabled);
        if (SelectedSource != null)
        {
          ExpandAdminFunctions = true;
          if (ModifySourceCommand.CanExecute(null))
          {
            ModifySourceCommand.Execute(null);
          }
        }
      }
    }

    /// <summary>
    /// Before letting the dialog close, make sure there are no un-applied changes
    /// and that there is only 1 data source enabled
    /// </summary>
    /// <returns></returns>
    public override bool CanCloseDialog()
    {
      if (HasUnappliedChanges)
      {
        _flewsUIService.ShowError("Must Apply changes before closing.");
        return false;
      }

      // Only allow 1 selected data source for now
      if (Settings.Sources.Count(i => i.Enabled) > 1)
      {
        _flewsUIService.ShowError("Must have only 1 data source Enabled.");
        return false;
      }

      return true;
    }


    /// <summary>
    /// Check if a 'new source' is being created as the dialog is closed and
    /// ask the user what they want to do with un-saved information
    /// </summary>
    public override void OnDialogClosed()
    {
      if (IsEditingSource
        && IsNewSource)
      {
        var result = _flewsUIService.ShowQuestion($"Should the previously open source settings be saved?",
                                                  DialogButtons.YesNo, "Save current settings?");
        if (result == ButtonResult.Yes)
        {
          SaveSourceCommand.Execute(null);
        }
      }
      base.OnDialogClosed();
    }

    /// <summary>
    /// Listen for changes to the .Disabled property on any DataSourceSettings and
    /// set the HasUnappliedChanges property to make sure some actions are taken
    /// to apply the source changes
    /// </summary>
    /// <param name="src"></param>
    /// <param name="args"></param>
    public void SourcePropertyChangedHandler(object? src, PropertyChangedEventArgs args)
    {
      if (src is DataSourceSettings sourceSettings
        && args.PropertyName == nameof(DataSourceSettings.Enabled))
      {
        HasUnappliedChanges = true;
      }
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

    /// <summary>
    /// Add property change handlers to all DataSourceSetting objects to listen
    /// for changes to the .Disabled property, this needs to be tracked to know when
    /// changes need to be applied.
    ///
    /// This handler also adds a CollectionChanged listener to the DataStoreModuleSettings
    /// object so it can add the property changed handler to any new sources as
    /// they are created.
    /// </summary>
    /// <param name="oldSource"></param>
    /// <param name="newSource"></param>
    private void AddSettingsChangeHandlers(DataSourceModuleSettings? oldSource, DataSourceModuleSettings newSource)
    {
      if (oldSource != null)
      {
        oldSource.Sources.CollectionChanged -= Sources_CollectionChanged;
        foreach (var source in oldSource.Sources)
        {
          WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>
            .RemoveHandler(source, nameof(INotifyPropertyChanged.PropertyChanged),
                           _sourcePropertyChanged);
        }
      }

      newSource.Sources.CollectionChanged += Sources_CollectionChanged;
      foreach (var source in newSource.Sources)
      {
        WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>
          .AddHandler(source, nameof(INotifyPropertyChanged.PropertyChanged),
                      _sourcePropertyChanged);
      }
    }

    /// <summary>
    /// @see AddSettingsChangeHandlers for details
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Sources_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
      if (e.NewItems != null)
      {
        foreach (var source in e.NewItems.OfType<DataSourceSettings>())
        {
          WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>
            .AddHandler(source, nameof(INotifyPropertyChanged.PropertyChanged),
                        _sourcePropertyChanged);
        }
      }
    }

    private void CreateDataSettings(EDataSourceType sourceType)
    {
      DataSourceSettings newSource = sourceType switch
      {
        EDataSourceType.DATABASE_POSTGRES => new SqlDataSourceSettings(),
        EDataSourceType.MEMORY => new MemoryDataSourceSettings(),
        EDataSourceType.LITE_DB => new LiteDbDataSourceSettings(),
        EDataSourceType.SQL_LITE => new FileDataSourceSettings(),
        _ => throw new ArgumentOutOfRangeException(nameof(sourceType), $"unable to create settings for unknown source type '{sourceType}'."),
      };
      newSource.Enabled = false;
      newSource.SourceType = sourceType;
      NewSourceSettings = newSource;
    }

    #endregion
  }
}
