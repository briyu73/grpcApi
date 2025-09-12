// Classification: OFFICIAL
//
// Copyright (C) 2023 Commonwealth of Australia.
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
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Security;

namespace FlewsApp.Modules.DataSource.Interface;

public interface IDataSourceManager : INotifyPropertyChanged
{
  public event EventHandler<DataSourceModuleSettings>? SettingsChanging;
  public event EventHandler<DataSourceModuleSettings>? SettingsChanged;

  bool ShowCreateUserView { get; set; }

  DataSourceModuleSettings Settings { get; set; }

  IEnumerable<IDataSource> DataSources { get; }

  IDataSource? GetDataSourceFor(string dataSourceType);

  IDataSource? SelectedSource { get; set; }

  // This is only used in the DataStore to create a new DataSourceManager. Can we remove it?
  void UpdateSettings(DataSourceModuleSettings settings);

  void RemoveDataSourceSetting(DataSourceSettings source);

  // This is only used in the DataStore to create a new DataSourceManager. Can we remove it?
  void ResetDataSourceSetting(DataSourceSettings source);

  void TestUserConnection(DataSourceSettings dataSourceSettings);

  void CreateDatabase(DataSourceSettings dataSourceSettings, string adminName, SecureString adminPassword);

  /// <summary>
  /// Returns true if user created, false if it already exists. Otherwise will throw an exception stating error
  /// </summary>
  /// <param name="dataSourceSettings"></param>
  /// <param name="adminName"></param>
  /// <param name="adminPassword"></param>
  /// <returns></returns>
  bool CreateUser(DataSourceSettings dataSourceSettings, string adminName, SecureString adminPassword);

  void ClearDataSources();

  void AddDataSource(IDataSource dataSource);

  void ExportDatabase();

  void ImportDatabase();
}
