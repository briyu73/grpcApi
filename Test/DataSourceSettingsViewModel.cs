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
using Mad.Libraries.Settings.Interface;

namespace FlewsApp.Modules.DataSource.UI.ViewModels
{
  public class DataSourceSettingsViewModel
  {
    // *******************************************************************************************
    // Fields
    // *******************************************************************************************

    #region Fields

    #endregion Fields

    // *******************************************************************************************
    // Properties
    // *******************************************************************************************

    #region Properties

    public DataSourceModuleSettings Settings { get; }

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

    public DataSourceSettingsViewModel(ISettingsProvider settings)
    {
      Settings = settings.GetSettings<DataSourceModuleSettings>();
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

    #endregion

    // *******************************************************************************************
    // Disposal Support
    // *******************************************************************************************

    #region IDisposalSupport

    #endregion
  }
}
