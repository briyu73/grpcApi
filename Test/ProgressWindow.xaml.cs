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

using FlewsApp.Modules.UI.ViewModels;
using Mad.Libraries.Core.Events;
using Prism.Events;
using Swordfish.NET.WPF.Controls;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace FlewsApp.Modules.UI.Windows
{
  /// <summary>
  /// Interaction logic for ProgressWindow.xaml
  /// </summary>
  public partial class ProgressWindow : Window
  {
    // *******************************************************************************************
    // Fields
    // *******************************************************************************************

    #region Fields

    private readonly IEventAggregator _eventAggregator;

    #endregion

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

    public ProgressWindow(ProgressViewModel viewModel, IEventAggregator eventAggregator)
    {
      _eventAggregator = eventAggregator;
      DataContext = viewModel;
      Owner = Application.Current.MainWindow;
      this.CenterInParent();

      InitializeComponent();

      viewModel.Worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
      Closed += ProgressWindow_Closed;
    }

    private void ProgressWindow_Closed(object? sender, System.EventArgs e)
    {
      var progressVM = (ProgressViewModel)DataContext;
      if (progressVM.UpdateStatusBar && !string.IsNullOrEmpty(progressVM.DisplayText))
      {
        // clear the progress status bar
        _eventAggregator.StatusUpdate(true, 0, 0, false, "");
        // show final display text in main status bar area
        _eventAggregator.StatusUpdate(false, 0, 0, true, progressVM.DisplayText);
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

    private void Worker_RunWorkerCompleted(object? sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
    {
      if (IsVisible)
      {
        // visual delay
        Thread.Sleep(500);

        // close the window
        Close();
      }
    }

    //This ensures that this stays on top of all other windows
    private void Window_PreviewLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
      var window = (Window)sender;
      window.Topmost = true;
    }

    #endregion

    // *******************************************************************************************
    // Disposal Support
    // *******************************************************************************************

    #region IDisposalSupport

    #endregion
  }
}
