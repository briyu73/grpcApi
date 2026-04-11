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

using Swordfish.NET.General;
using Swordfish.NET.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Windows.Win32.Graphics.Gdi;

namespace Swordfish.NET.WPF.Controls
{
  public static class Extensions
  {

    private static readonly log4net.ILog _log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()!.DeclaringType);

    /// <summary>
    /// Used for using the DisplayName attribute as the column header when generating columns in DataGrid controls
    /// </summary>
    /// <example>
    /// 
    /// include this at the top of the xaml file
    /// xmlns:SwordfishControls="http://swordfish.com.au/WPF/1.0"
    /// 
    /// have the event handler in the control and AutoGenerateColumns = True
    /// <DataGrid x:Name="theGrid" AutoGenerateColumns="True" AutoGeneratingColumn="dg_AutoGeneratingColumn"/>
    /// 
    /// </example>
    /// <param name="control"></param>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    public static void dg_AutoGeneratingColumn(this System.Windows.Controls.ContentControl control, object sender, DataGridAutoGeneratingColumnEventArgs e)
    {
      dg_AutoGeneratingColumn(e);
    }

    /// <summary>
    /// Allow handing the column generation of a DataGrid that is a child of any ContentControl type (e.g. UserControl, Window, TabItem, etc)
    /// </summary>
    /// <param name="control"></param>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    public static void dg_AutoGeneratingColumnOneWay(this System.Windows.Controls.ContentControl control, object sender, DataGridAutoGeneratingColumnEventArgs e)
    {
      dg_AutoGeneratingColumn(e, BindingMode.OneWay);
    }

    /// <summary>
    /// Callback for accessing and updating columns during autogeneration, in case you want to filter or change the headers.
    /// DisplayNameAttribute meta data is read off the properties, if the DisplayName == "" then cancel creating the column.
    /// </summary>
    private static void dg_AutoGeneratingColumn(DataGridAutoGeneratingColumnEventArgs e, BindingMode bindingMode = BindingMode.TwoWay)
    {
      // Need to make sure we aren't trying to do two way binding on a read only property
      var prop = e.PropertyDescriptor as System.ComponentModel.PropertyDescriptor;
      if (prop?.IsReadOnly == true)
      {
        bindingMode = BindingMode.OneWay;
      }

      // If the display name is "" then we don't show this property
      string displayName = PropertyHelper.GetPropertyDisplayName(e.PropertyDescriptor);
      string oldHeader = e.Column.Header.ToString();
      if (!string.IsNullOrEmpty(displayName))
      {
        e.Column.Header = displayName;
      }
      else if (displayName == "")
      {
        e.Cancel = true;
        return;
      }

      // This is so we can toggle checkBoxes with one click, rather than the default behaviour of
      // DataGridCheckBoxColumn where you have to select the row, then toggle the checkbox
      if (e.Column is DataGridCheckBoxColumn)
      {
        //Set up the CheckBox Factory
        FrameworkElementFactory cbFactory = new FrameworkElementFactory(typeof(CheckBox));
        Binding binding = new Binding(oldHeader);
        binding.Mode = bindingMode;
        binding.UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;
        cbFactory.SetBinding(CheckBox.IsCheckedProperty, binding);
        cbFactory.SetValue(CheckBox.HorizontalAlignmentProperty, HorizontalAlignment.Center);

        // Set up the DataTemplate for the column
        var checkBoxTemplate = new System.Windows.DataTemplate();
        checkBoxTemplate.VisualTree = cbFactory;

        // Now create a new column and assign it
        DataGridTemplateColumn newColumn = new DataGridTemplateColumn();
        newColumn.CellTemplate = checkBoxTemplate;
        newColumn.Header = e.Column.Header;
        newColumn.SortMemberPath = e.Column.SortMemberPath;
        e.Column = newColumn;
      }
      else if (e.PropertyType == typeof(System.Windows.Input.ICommand))
      {
        //Set up the CheckBox Factory
        FrameworkElementFactory cbFactory = new FrameworkElementFactory(typeof(Button));
        Binding binding = new Binding(oldHeader);
        binding.Mode = bindingMode;
        binding.UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;
        cbFactory.SetBinding(Button.CommandProperty, binding);
        cbFactory.SetValue(Button.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        cbFactory.SetValue(Button.ContentProperty, e.Column.Header);

        // Set up the DataTemplate for the column
        var buttonTemplate = new System.Windows.DataTemplate();
        buttonTemplate.VisualTree = cbFactory;

        // Now create a new column and assign it
        DataGridTemplateColumn newColumn = new DataGridTemplateColumn();
        newColumn.CellTemplate = buttonTemplate;
        newColumn.Header = e.Column.Header;
        newColumn.SortMemberPath = e.Column.SortMemberPath;
        e.Column = newColumn;
      }

      if (e.Column is DataGridBoundColumn boundColumn)
      {
        Binding binding = boundColumn.Binding as Binding;
        if (binding != null)
        {
          binding.Mode = bindingMode;
        }
      }
    }

    public static void MakeDatePickerColumn(this DataGridAutoGeneratingColumnEventArgs e)
    {
      // Need to make sure we aren't trying to do two way binding on a read only property
      if (!(e.PropertyDescriptor is System.ComponentModel.PropertyDescriptor prop))
        return;

      var bindingMode =
          prop.IsReadOnly == true ?
          BindingMode.OneWay :
          BindingMode.TwoWay;

      //Set up the CheckBox Factory
      FrameworkElementFactory datePickerFactory = new FrameworkElementFactory(typeof(DatePicker));
      Binding binding = new Binding(prop.Name);
      binding.Mode = bindingMode;
      binding.UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;
      datePickerFactory.SetBinding(DatePicker.SelectedDateProperty, binding);

      // Set up the DataTemplate for the column
      var datePickerTemplate = new System.Windows.DataTemplate();
      datePickerTemplate.VisualTree = datePickerFactory;

      // Now create a new column and assign it
      DataGridTemplateColumn newColumn = new DataGridTemplateColumn();
      newColumn.CellTemplate = datePickerTemplate;
      newColumn.Header = e.Column.Header;
      e.Column = newColumn;
    }


    /// <summary>
    /// This sets the text from a TextBox to the bound property and clears the keyboard focus from the text box.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    public static void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
      if (e.Key != Key.Enter)
      {
        return;
      }
      TextBox textBox = e.Source as TextBox;
      if (textBox == null)
      {
        return;
      }
      BindingExpression binding = BindingOperations.GetBindingExpression(textBox, TextBox.TextProperty);
      if (binding == null)
      {
        return;
      }
      try
      {
        binding.UpdateSource();
        Keyboard.ClearFocus();
      }
      catch (Exception ex)
      {
        _log.Error(ex);
      }
    }

    private static readonly System.Reflection.PropertyInfo InheritanceContextProp = typeof(DependencyObject).GetProperty("InheritanceContext", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

    public static IEnumerable<DependencyObject> GetParents(this DependencyObject child)
    {
      while (child != null)
      {
        var parent = LogicalTreeHelper.GetParent(child);
        if (parent == null)
        {
          if (child is FrameworkElement)
          {
            parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
          }
          if (parent == null && child is ContentElement)
          {
            parent = ContentOperations.GetParent((ContentElement)child);
          }
          if (parent == null)
          {
            parent = InheritanceContextProp.GetValue(child, null) as DependencyObject;
          }
        }
        child = parent;
        yield return parent;
      }
    }

    /// <summary>
    /// Gets the children but in order of depth from the parent, rather than recursively
    /// </summary>
    /// <param name="panel"></param>
    /// <returns></returns>
    public static IEnumerable<UIElement> GetAllChildren(this Panel panel)
    {
      if (panel == null)
      {
        yield break;
      }

      List<UIElement> previousLayer = new List<UIElement>();
      previousLayer.AddRange(panel.Children.OfType<UIElement>());
      while (previousLayer.Count > 0)
      {
        foreach(var child in previousLayer)
        {
          yield return child;
        }
        // Get the next layer of children
        previousLayer = previousLayer.OfType<Panel>().SelectMany(p => p.Children.OfType<UIElement>()).ToList();
      }
    }

    private delegate void VoidDelegate();
    public static void BeginInvoke(this System.Windows.Threading.Dispatcher dispatcher, System.Windows.Threading.DispatcherPriority priority, Action action)
    {
      dispatcher.BeginInvoke(priority, new VoidDelegate(action));
    }

    /// <summary>
    /// Returns position and size information for the monitor the window is
    /// currently on, or nearest to if invalid. Note this returns in "logical
    /// units" (what WPF uses) instead of pixels.
    /// </summary>
    public static Rect GetMonitorRect(this Window window)
    {
      // JG: I need to have a rant because all of this is required *just* to get the dimensions of
      //     a window when it is maximized, because apparently that isn't important enough to be
      //     included in WPF. Window.Left and Window.Top actually return the "restore" point for
      //     a maximized window, not the actual Left/Top when maximized (because that makes sense).
      var fromPixels = PresentationSource.FromVisual(window).CompositionTarget.TransformFromDevice;
      Win32.MONITORINFO monitorInfo = new()
      {
        cbSize = (uint)Win32.MONITORINFO.SizeOf
      };

      // Convert from "logical units" to pixels for Win32 and get monitor dimensions
      var monitor = API.MonitorFromWindow(new WindowInteropHelper(window).EnsureHandle(), 0x02);
      API.GetMonitorInfo(monitor, ref monitorInfo);

      // Convert to "logical units" for WPF and return
      var xy = fromPixels.Transform(new Point(monitorInfo.rcWork.left, monitorInfo.rcWork.top));
      var wh = fromPixels.Transform(new Point(monitorInfo.rcWork.right - monitorInfo.rcWork.left, monitorInfo.rcWork.bottom - monitorInfo.rcWork.top));
      return new Rect
      {
        X = xy.X,
        Y = xy.Y,
        Width = wh.X,
        Height = wh.Y,
      };
    }

    /// <summary>
    /// Center's the window within the parent window.
    /// This must be called on this window's owning thread!
    /// </summary>
    /// <param name="parent">Parent window, defaults to main window if not specified</param>
    public static void CenterInParent(this Window window, Window parent = null)
    {
      Rect workingWindowRect;
      Window workingWindow = parent ?? Application.Current.MainWindow;
      workingWindow.Dispatcher.Invoke(() =>
      {
        workingWindowRect = workingWindow.GetWindowRect();
      });

      window.Left = workingWindowRect.Left + (workingWindowRect.Width - window.Width) / 2;
      window.Top = workingWindowRect.Top + (workingWindowRect.Height - window.Height) / 2;
    }

    /// <summary>
    /// Returns a Rect describing the position and size of the window.
    /// Takes into account being maximised, etc.
    /// This must be called on this window's owning thread!
    /// </summary>
    public static Rect GetWindowRect(this Window window)
    {
      if (window.WindowState == WindowState.Maximized)
      {
        return GetMonitorRect(window);
      }
      else
      {
        return new Rect
        {
          X = window.Left,
          Y = window.Top,
          Width = window.ActualWidth,
          Height = window.ActualHeight,
        };
      }
    }

    /// <summary>
    /// Positions the window offset from the bottom right of the provided parent window.
    /// This must be called on this window's owning thread!
    /// </summary>
    /// <param name="parent">Parent window, defaults to main window if not specified</param>
    public static void BottomRightLocationToParent(this Window window, double xSpacing, double ySpacing, Window parent = null)
    {
      Rect workingWindowRect;
      Window workingWindow = parent ?? Application.Current.MainWindow;
      workingWindow.Dispatcher.Invoke(() =>
      {
        workingWindowRect = workingWindow.GetWindowRect();
      });

      window.Left = (workingWindowRect.Left + workingWindow.ActualWidth) - window.ActualWidth - xSpacing;
      window.Top = (workingWindowRect.Top + workingWindow.ActualHeight) - window.ActualHeight - ySpacing;
    }

    public static byte A(this uint color) => (byte)((color & 0xFF000000) >> 24);
    public static byte R(this uint color) => (byte)((color & 0x00FF0000) >> 16);
    public static byte G(this uint color) => (byte)((color & 0x0000FF00) >> 8);
    public static byte B(this uint color) => (byte)(color & 0x000000FF);

    /// <summary>
    /// Returns the mouse cursor location.  This method is necessary during 
    /// a drag-drop operation because the WPF mechanisms for retrieving the
    /// cursor coordinates are unreliable.
    /// </summary>
    /// <param name="relativeTo">The Visual to which the mouse coordinates will be relative.</param>
    /// <remarks>
    /// Original version written by Dan Crevier (Microsoft).  
    /// http://blogs.msdn.com/llobo/archive/2006/09/06/Scrolling-Scrollviewer-on-Mouse-Drag-at-the-boundaries.aspx

    /// </remarks>
    public static Point GetMousePosition(this Visual relativeTo)
    {
      Win32.POINT mouse = new Win32.POINT();
      API.GetCursorPos(ref mouse);

      // Using PointFromScreen instead of Dan Crevier's code (commented out below)
      // is a bug fix created by William J. Roberts.  Read his comments about the fix:

      // After picking my brain for a long while, I think I have come up with a solution.
      // I believe the problem resides in Dan's GetMousePosition(...) static method.
      // First he grabs the mouses position and converts the value to be relative to the hWnd.
      // This point is represented by pixels.
      // Next, Dan gets the hWnd's transform. This is where I believe he is incorrect.
      // The problem is, WPF's internal representation of coordinates is not pixel values.
      // Instead, a virtual coordinate system is used under the hood.
      // He then takes this virtual coordinate offset and subtracts it from actual
      // screen pixel coordinates. Although the coordiantes seemed to luckily work out
      // in standard resolutions, the virtual coordinates are scaled for wide screen
      // resolution. Hence, the issue I was seeing would occur.
      // I noticed that the values where being scaled as I moved the mouse in the X direction.
      // I believe his post was made prior to the PointFromScreen and ScreenToPoint methods
      // being added. Using the PointFromScreen method, I believe I have created a much more
      // elegant solution. I have tested the below fix on both a wide screen monitor and a
      // standard monitor. Please let me know if my assumptions are incorrect. I am by no
      // means a WPF expert yet.

      return relativeTo.PointFromScreen(new Point(mouse.x, mouse.y));

      //System.Windows.Interop.HwndSource presentationSource = (System.Windows.Interop.HwndSource)PresentationSource.FromVisual( relativeTo );
      //ScreenToClient( presentationSource.Handle, ref mouse );
      //GeneralTransform transform = relativeTo.TransformToAncestor( presentationSource.RootVisual );
      //Point offset = transform.Transform( new Point( 0, 0 ) );
      //return new Point( mouse.X - offset.X, mouse.Y - offset.Y );
    }
  }
}
