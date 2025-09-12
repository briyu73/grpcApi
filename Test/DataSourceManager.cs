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

using DryIoc;
using FlewsApp.Modules.DataSource.Data.Settings;
using FlewsApp.Modules.DataSource.Interface;
using FlewseDotNetLib.Settings;
using Mad.Libraries.Settings.Interface;
using Prism.Mvvm;
using Swordfish.NET.General;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading.Tasks;

namespace FlewsApp.Modules.DataSource.Services
{
  public class DataSourceManager : BindableBase, IDataSourceManager
  {
    // *******************************************************************************************
    // Fields
    // *******************************************************************************************

    #region Fields

    public const int MAX_DATABASE_CONNECTIONS = 20;

    private readonly IContainer _container;
    private readonly IAppSettings _appSettings;

    private ObservableCollection<IDataSource> _dataSources = new();

    public event EventHandler<DataSourceModuleSettings>? SettingsChanging;
    public event EventHandler<DataSourceModuleSettings>? SettingsChanged;

    #endregion Fields

    // *******************************************************************************************
    // Properties
    // *******************************************************************************************

    #region Properties

    protected DataSourceModuleSettings _dataSourceModuleSettings = null!;
    public DataSourceModuleSettings Settings
    {
      get => _dataSourceModuleSettings;
      set
      {
        if (_dataSourceModuleSettings != value)
        {
          if (_dataSourceModuleSettings != null)
          {
            SettingsChanging?.Invoke(this, _dataSourceModuleSettings);
          }
          _dataSourceModuleSettings = value;
          SettingsChanged?.Invoke(this, _dataSourceModuleSettings);
        }
      }
    }

    public IEnumerable<IDataSource> DataSources => _dataSources;

    private IDataSource? _selectedSource = null;
    public IDataSource? SelectedSource
    {
      get => _selectedSource;
      set => SetProperty(ref _selectedSource, value);
    }

    public IEnumerable<IDataContextProvider> DataContextProviders => _container.Resolve<IDataContextProvider[]>();

    public bool ShowCreateUserView { get; set; }

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

    public DataSourceManager(IContainer container, ISettingsProvider settingsProvider, IAppSettings appSettings)
    {
      _container = container;
      _appSettings = appSettings;

      Settings = settingsProvider.GetSettings<DataSourceModuleSettings>();

      InitialiseDataSources();
    }

    public IDataSource? GetDataSourceFor(string sourceName) => _dataSources.SingleOrDefault(ds => ds.Settings.SourceName == sourceName);

    public void UpdateSettings(DataSourceModuleSettings dataSourceModuleSettings)
    {
      _dataSources.Clear();
      InitialiseDataSources();

      // Only needed for the DataStore module.
      DataContextProviders.ForEach(p => p.UpdateSettings(dataSourceModuleSettings));

      // Only needed for the DataAccess module for now, but this may change later.
      if (SelectedSource != null)
      {
        DataContextProviders.ForEach(p => p.InitDatabase(SelectedSource));
      }
    }

    public void RemoveDataSourceSetting(DataSourceSettings dataSourceSettings) => Settings.Sources.Remove(dataSourceSettings);

    public void ResetDataSourceSetting(DataSourceSettings dataSourceSettings) => DataContextProviders.ForEach(p => p.ResetDataSourceSetting(dataSourceSettings));

    public void TestUserConnection(DataSourceSettings dataSourceSettings) => DataContextProviders.ForEach(p => p.TestUserConnection(dataSourceSettings));

    public void CreateDatabase(DataSourceSettings dataSourceSettings, string adminName, SecureString adminPassword)
    {
      // Note: We need to explicitly wait for the tasks to complete for any exceptions to bubble up,
      //       otherwise they get swallowed (see FL-6992).
      var createDatabaseTasks = DataContextProviders.Select(p => p.CreateDatabase(dataSourceSettings, adminName, adminPassword)).ToArray();
      Task.WaitAll(createDatabaseTasks);
    }

    public bool CreateUser(DataSourceSettings dataSourceSettings, string adminName, SecureString adminPassword)
    {
      return DataContextProviders.Select(p => p.CreateUser(dataSourceSettings, adminName, adminPassword)).Max();
    }

    public void ClearDataSources() => _dataSources.Clear();

    public void AddDataSource(IDataSource dataSource) => _dataSources.Add(dataSource);

    public void ExportDatabase()
    {
      if (SelectedSource != null)
      {
        var defaultObjectsDir = Path.Combine(_appSettings.FlewsSettings.ProjectData, Config.HelperFns.DEFAULT_DATASTORE_OBJECTS);
        DataContextProviders.ForEach(p => Task.Run(async () => await p.ExportDatabase(SelectedSource, defaultObjectsDir)).Wait());
      }
    }

    public void ImportDatabase()
    {
      if (SelectedSource != null)
      {
        var defaultObjectsDir = Path.Combine(_appSettings.FlewsSettings.ProjectData, Config.HelperFns.DEFAULT_DATASTORE_OBJECTS);
        DataContextProviders.ForEach(p => Task.Run(async () => await p.ImportDatabase(SelectedSource, defaultObjectsDir)).Wait());
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

    private void InitialiseDataSources()
    {
      foreach (var dataSourceSettings in Settings.Sources.Where(ds => ds.Enabled))
      {
        var dataSource = _container.Resolve<IDataSource>(dataSourceSettings.SourceType.ToString(), args: new object[] { dataSourceSettings });
        _dataSources.Add(dataSource);
      }

      SelectedSource = _dataSources.LastOrDefault();
    }

    #endregion

    // *******************************************************************************************
    // Disposal Support
    // *******************************************************************************************

    #region IDisposalSupport

    #endregion
  }
}
