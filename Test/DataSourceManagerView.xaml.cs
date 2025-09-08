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

using System.Windows.Controls;

namespace FlewsApp.Modules.DataSource.UI.Views
{
  /// <summary>
  /// Interaction logic for DataSourceManagerView.xaml
  /// </summary>
  public partial class DataSourceManagerView : UserControl
  {
    public DataSourceManagerView()
    {
      InitializeComponent();
    }

    private void SourcePasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
      if (sender is PasswordBox passwordBox)
      {
        var securePasswordProperty = DataContext?.GetType().GetProperty("SourcePassword");
        if (securePasswordProperty?.SetMethod != null)
        {
          securePasswordProperty.SetMethod.Invoke(DataContext, new object[] { passwordBox.SecurePassword });
        }
      }
    }

    private void AdminPasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
      if (sender is PasswordBox passwordBox)
      {
        var securePasswordProperty = DataContext?.GetType().GetProperty("AdminPassword");
        if (securePasswordProperty?.SetMethod != null)
        {
          securePasswordProperty.SetMethod.Invoke(DataContext, new object[] { passwordBox.SecurePassword });
        }
      }
    }
  }
}
