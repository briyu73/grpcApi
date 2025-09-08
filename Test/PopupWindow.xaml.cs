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

// PopupWindow Class
// William Eddins - http://ascendedguard.com
// 
// A window for displaying a short message on the screen, with
// intentions of no interaction, and disappearing after a given time.
// 8/1/07 - Initial Version
// 8/13/07 - Show added to imitate MessageBox.
//         - Loaded event removed, replaced with an invoke, which allows for changing the left and top manually.
//         - Removed the need for private Timer variables, removed DoneClosingTimer (replaced with Storyboard.Completed)

using FlewseDotNetLib.Settings;
using Swordfish.NET.WPF.Controls;
using System;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Media.Animation;

namespace FlewsApp.Modules.UI.Windows
{
  /// <summary>
  /// Interaction logic for PopupWindow.xaml
  /// </summary>

  public partial class PopupWindow : Window
  {
    //Delegate used for invoking anonymous functions
    delegate void VoidDelegate();

    private readonly Timer? _closeTimer = null;
    public PopupWindow(IAppSettingsAdapter appSettingsAdapter, string message, TimeSpan duration, Window? parentWindow)
    {
      InitializeComponent();

      //Message to be displayed in the window
      string appName = appSettingsAdapter.ProductAcronym ?? appSettingsAdapter.ApplicationName;
      TitleLabel.Content = $".{appName}.";
      Message.Content = message;
      if (parentWindow != null)
      {
        Owner = parentWindow;
      }

      //Begin closing the window after the specified duration has elapsed.
      _closeTimer = new Timer(duration);
      _closeTimer.Elapsed += new ElapsedEventHandler(closeTimer_Elapsed);
      _closeTimer.Start();

      //This cannot be in the constructor directly, because the ActualWidth of items
      //does not get set until AFTER the constructor. Since I couldn't get the Initialize
      //event to fire, this method seems to work good.
      Application.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new VoidDelegate(delegate
      {
        Width = Math.Max(TitleLabel.ActualWidth, Message.ActualWidth) + 50;

        //Set the default placement to the bottom right corner, above the Taskbar.
        this.BottomRightLocationToParent(30, 20, parentWindow);
      }));
    }

    void closeTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
      (sender as Timer)?.Stop();

      //We must begin the storyboard on the main window thread.
      try
      {
        Dispatcher.Invoke(() =>
          {
            Storyboard story = (Storyboard)FindResource("FadeAway");
            story.Completed += new EventHandler(story_Completed);
            BeginStoryboard(story);
          });
      }
      catch (TaskCanceledException)
      {
        // Check ex.CancellationToken.IsCancellationRequested here.
        // If false, it's pretty safe to assume it was a timeout.
      }
    }

    public void ResetTimer()
    {
      if (_closeTimer != null)
      {
        _closeTimer.Stop();
        _closeTimer.Start();
      }
    }

    /// <summary>
    /// Closes the window after we're done fading out.
    /// </summary>
    void story_Completed(object? sender, EventArgs e)
    {
      Close();
      _bShowing = false;
    }

    #region Static Functions (Show)

    private static bool _bShowing = false;
    private static PopupWindow? _popupWin = null;

    /// <summary>
    /// Shows a message window, focusing on the parent after creation.
    /// </summary>
    /// <param name="message">Message to display</param>
    /// <param name="duration">Amount of time to show the window, defaults to 5 seconds if null</param>
    /// <param name="parentWindow">Window to send focus to</param>
    public static void Show(IAppSettingsAdapter appSettingsAdapter, string message, Window? parentWindow, TimeSpan? duration = null)
    {
      Application.Current.Dispatcher.Invoke(() =>
      {
        // Only show popup window if we have a main window showing
        if (!Application.Current.MainWindow.IsActive)
          return;

        if (_bShowing && _popupWin != null)
        {
          _popupWin.Message.Content += $"\n{message}";
          _popupWin.Message.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
          _popupWin.Message.Arrange(new Rect(_popupWin.DesiredSize));
          _popupWin.UpdateLayout();
          _popupWin.BottomRightLocationToParent(30, 20, parentWindow);
          _popupWin.ResetTimer();
          return;
        }

        _popupWin = new PopupWindow(appSettingsAdapter, message, duration ?? TimeSpan.FromSeconds(5), parentWindow);
        _popupWin.Show();
        _bShowing = true;

        parentWindow?.Focus();
      });
    }

    #endregion
  }

}
