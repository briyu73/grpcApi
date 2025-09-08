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

using FlewsApp.Modules.UI.Interface;
using Mad.Libraries.Core.Events;
using Mad.Libraries.ShellBase.ViewModels;
using Prism.Events;
using Swordfish.NET.ViewModel;
using System;
using System.ComponentModel;
using System.Windows.Input;

namespace FlewsApp.Modules.UI.ViewModels
{
  public class ProgressViewModel : ViewModelBase, IDisposable
  {
    // *******************************************************************************************
    // Fields
    // *******************************************************************************************

    #region Fields

    private readonly IEventAggregator _eventAggregator;
    private readonly IFlewsUIService _uiService;

    private bool _updateStatusBar;

    #endregion

    // *******************************************************************************************
    // Properties
    // *******************************************************************************************

    #region Properties

    public string DisplayText => $"{_displayText ?? "Processing"} {(Indeterminate ? "" : $"[{_percentage}%]")}";

    /// <summary>
    /// Whether the background job has been started.
    /// </summary>
    private bool _isStarted;
    private bool IsStarted
    {
      get => _isStarted;
      set
      {
        if (SetProperty(ref _isStarted, value))
        {
          RaisePropertyChanged(nameof(IsBusy));
        }
      }
    }

    public BackgroundWorker Worker { get; } = new BackgroundWorker();

    private string _displayText;
    public string Text
    {
      get => _displayText;
      set
      {
        if (SetProperty(ref _displayText, value))
        {
          RaisePropertyChanged(nameof(DisplayText));
          if (_updateStatusBar)
          {
            _eventAggregator.StatusUpdate(true, _percentage, 100, false, DisplayText);
          }
        }
      }
    }

    private int _percentage;
    public int Percentage
    {
      get => _percentage;
      set
      {
        if (SetProperty(ref _percentage, value))
        {
          RaisePropertyChanged(nameof(DisplayText));
          if (_updateStatusBar)
          {
            _eventAggregator.StatusUpdate(true, _percentage, 100, false, DisplayText);
          }
        }
      }
    }

    private bool _indeterminate = false;
    public bool Indeterminate
    {
      get => _indeterminate;
      set
      {
        if (SetProperty(ref _indeterminate, value))
        {
          RaisePropertyChanged(nameof(DisplayText));
          if (_updateStatusBar)
          {
            if (value)
            {
              _eventAggregator.StatusUpdate(true, 0, 0, true, DisplayText);
            }
            else
            {
              _eventAggregator.StatusUpdate(true, _percentage, 100, false, DisplayText);
            }
          }
        }
      }
    }

    private bool _multilineMode = false;
    public bool MultilineMode
    {
      get => _multilineMode || DisplayText.Contains("\n");
      set
      {
        SetProperty(ref _multilineMode, value);
      }
    }

    public bool IsBusy => (!IsStarted || Worker.IsBusy);

    public bool UpdateStatusBar => _updateStatusBar;

    #endregion

    // *******************************************************************************************
    // Commands
    // *******************************************************************************************

    #region Commands

    public bool? CancelRequested { get; set; }
    private RelayCommandFactory _cancelProcessCommand = new RelayCommandFactory();
    public ICommand CancelProcessCommand => _cancelProcessCommand.GetCommand(p =>
    {
      CancelRequested = true;
      Worker.CancelAsync();
    }, p => IsBusy);

    #endregion

    // *******************************************************************************************
    // Public Methods
    // *******************************************************************************************

    #region Public Methods

    public ProgressViewModel(IEventAggregator eventAggregator, IFlewsUIService uiService,
                             string displayText, bool updateStatusBar = false,
                             bool supportsCancel = true, bool indeterminate = false) : base(true)
    {
      _eventAggregator = eventAggregator;
      _uiService = uiService;
      _displayText = displayText;
      _updateStatusBar = updateStatusBar;
      Indeterminate = indeterminate;
      IsStarted = true;   // Mark we've "started" even though we haven't so that IsBusy returns false

      Worker.ProgressChanged += Worker_ProgressChanged;
      Worker.WorkerReportsProgress = true;
      Worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
      Worker.WorkerSupportsCancellation = supportsCancel;
    }

    public void Run(object? data, bool showProgressBar)
    {
      CancelRequested = null;
      if (IsBusy)
      {
        _uiService.ShowError("Process is currently busy and cannot run multiple tasks concurrently",
                             "Unable to run process");
        return;
      }
      IsStarted = false;

      if (showProgressBar)
      {
        // show the progress bar window
        _uiService.ShowProgressBar(this);
      }

      // now run the process
      if (_updateStatusBar)
      {
        if (Indeterminate)
        {
          _eventAggregator.StatusUpdate(true, 0, 0, true, _displayText);
        }
        else
        {
          _eventAggregator.StatusUpdate(true, _percentage, 100, false, DisplayText);
        }
      }

      // Run the background worker
      Worker.RunWorkerAsync(data);
      IsStarted = true;
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

    private void Worker_ProgressChanged(object? sender, ProgressChangedEventArgs e)
    {
      Percentage = e.ProgressPercentage;
    }

    private void Worker_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
    {
      if (_updateStatusBar)
      {
        if (Indeterminate)
        {
          // clears the status bar
          _eventAggregator.StatusUpdate(false, 0, 0, true, "Ready");
        }
        else if (CancelRequested ?? false)
        {
          _eventAggregator.StatusUpdate(true, _percentage, 100, false, DisplayText);
        }
        else
        {
          // The following line also update the status bar if necessary
          Percentage = 100;
        }
      }

      // hide the progress bar window if showing
      _uiService.HideProgressBar();
    }

    #endregion

    // *******************************************************************************************
    // Disposal Support
    // *******************************************************************************************

    #region IDisposalSupport

    protected virtual void Dispose(bool disposing)
    {
      Worker?.Dispose();
    }

    public void Dispose()
    {
      Dispose(true);
    }

    #endregion
  }
}
