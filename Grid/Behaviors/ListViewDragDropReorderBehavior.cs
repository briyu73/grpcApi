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

using Microsoft.Xaml.Behaviors;
using Swordfish.NET.Win32;
using Swordfish.NET.WPF.Controls;
using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Swordfish.NET.WPF.Behaviors
{
  /// <summary>
  /// Manages the dragging and dropping of ListViewItems in a ListView.
  /// </summary>
  /// <remarks>
  /// Based on code by Josh Smith, Copyright (C) Josh Smith - January 2007
  /// https://www.codeproject.com/Articles/17266/Drag-and-Drop-Items-in-a-WPF-ListView
  /// </remarks>

  public class ListViewDragDropReorderBehavior : Behavior<ListView>
  {
    #region Private Fields

    private bool _canInitiateDrag;
    private DragAdorner _dragAdorner;
    private double _dragAdornerOpacity;
    private int _indexToSelect;
    private object _itemUnderDragCursor;
    private ListView _listView;
    private Point _mouseDownPos;
    private bool _showDragAdorner;

    #endregion Private Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of ListViewDragManager.
    /// </summary>
    public ListViewDragDropReorderBehavior()
    {
      _canInitiateDrag = false;
      _dragAdornerOpacity = 0.8;
      _indexToSelect = -1;
      _showDragAdorner = true;
    }


    #endregion // Constructors

    #region Protected Overrides

    /// <summary>
    /// Attaches to an ItemsControl
    /// </summary>
    protected override void OnAttached()
    {
      base.OnAttached();

      if (AssociatedObject is ListView listView)
      {
        ListView = listView;
      }
    }

    protected override void OnDetaching()
    {
      ListView = null;

      base.OnDetaching();
    }

    #endregion Protected Overrides

    #region Properties

    /// <summary>
    /// Gets/sets the opacity of the drag adorner.  This property has no
    /// effect if ShowDragAdorner is false. The default value is 0.7
    /// </summary>
    public double DragAdornerOpacity
    {
      get => _dragAdornerOpacity;
      set => _dragAdornerOpacity = Math.Max(0, Math.Min(1.0, value));
    }

    /// <summary>
    /// Gets/sets the ListView whose dragging is managed.  This property
    /// can be set to null, to prevent drag management from occuring.  If
    /// the ListView's AllowDrop property is false, it will be set to true.
    /// </summary>
    public ListView ListView
    {
      get => _listView;
      set
      {
        if (_listView != null)
        {
          // Unhook Events

          _listView.PreviewMouseLeftButtonDown -= listView_PreviewMouseLeftButtonDown;
          _listView.PreviewMouseMove -= listView_PreviewMouseMove;
          _listView.DragOver -= listView_DragOver;
          _listView.DragLeave -= listView_DragLeave;
          _listView.DragEnter -= listView_DragEnter;
          _listView.Drop -= listView_Drop;
        }

        _listView = value;

        if (_listView != null)
        {
          _listView.AllowDrop = true;

          // Hook Events

          _listView.PreviewMouseLeftButtonDown += listView_PreviewMouseLeftButtonDown;
          _listView.PreviewMouseMove += listView_PreviewMouseMove;
          _listView.DragOver += listView_DragOver;
          _listView.DragLeave += listView_DragLeave;
          _listView.DragEnter += listView_DragEnter;
          _listView.Drop += listView_Drop;
        }
      }
    }

    /// <summary>
    /// Gets/sets whether a visual representation of the ListViewItem being dragged
    /// follows the mouse cursor during a drag operation.  The default value is true.
    /// </summary>
    public bool ShowDragAdorner
    {
      get { return _showDragAdorner; }
      set => _showDragAdorner = value;
    }

    #endregion Properties

    #region Event Handling Methods

    void listView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
      if (IsMouseOverScrollbar)
      {
        // 4/13/2007 - Set the flag to false when cursor is over scrollbar.
        _canInitiateDrag = false;
        return;
      }

      int index = IndexUnderDragCursor;
      _canInitiateDrag = index > -1;

      if (_canInitiateDrag)
      {
        // Remember the location and index of the ListViewItem the user clicked on for later.
        _mouseDownPos = _listView.GetMousePosition();
        _indexToSelect = index;
      }
      else
      {
        _mouseDownPos = new Point(-10000, -10000);
        _indexToSelect = -1;
      }
    }

    void listView_PreviewMouseMove(object sender, MouseEventArgs e)
    {
      if (!CanStartDragOperation)
      {
        return;
      }

      // Select the item the user clicked on.
      if (_listView.SelectedIndex != _indexToSelect)
      {
        _listView.SelectedIndex = _indexToSelect;
      }

      // If the item at the selected index is null, there's nothing
      // we can do, so just return;
      if (_listView.SelectedItem == null)
      {
        return;
      }

      ListViewItem itemToDrag = GetListViewItem(_listView.SelectedIndex);
      if (itemToDrag == null)
      {
        return;
      }

      AdornerLayer adornerLayer = ShowDragAdornerResolved ? InitializeAdornerLayer(itemToDrag) : null;

      InitializeDragOperation(itemToDrag);
      PerformDragOperation();
      FinishDragOperation(itemToDrag, adornerLayer);
    }


    void listView_DragOver(object sender, DragEventArgs e)
    {
      e.Effects = DragDropEffects.Move;

      if (ShowDragAdornerResolved)
      {
        UpdateDragAdornerLocation();
      }

      // Update the item which is known to be currently under the drag cursor.
      int index = IndexUnderDragCursor;
      ItemUnderDragCursor = index < 0 ? null : ListView.Items[index];
    }


    void listView_DragLeave(object sender, DragEventArgs e)
    {
      if (!IsMouseOver(_listView))
      {
        if (ItemUnderDragCursor != null)
        {
          ItemUnderDragCursor = null;
        }

        if (_dragAdorner != null)
        {
          _dragAdorner.Visibility = Visibility.Collapsed;
        }
      }
    }

    void listView_DragEnter(object sender, DragEventArgs e)
    {
      if (_dragAdorner != null && _dragAdorner.Visibility != Visibility.Visible)
      {
        // Update the location of the adorner and then show it.				
        UpdateDragAdornerLocation();
        _dragAdorner.Visibility = Visibility.Visible;
      }
    }

    void listView_Drop(object sender, DragEventArgs e)
    {
      if (ItemUnderDragCursor != null)
      {
        ItemUnderDragCursor = null;
      }

      e.Effects = DragDropEffects.None;

      // Get the ObservableCollection<ItemType> which contains the dropped data object.
      IList itemsSource = _listView.ItemsSource as IList;
      if (itemsSource == null)
        throw new Exception(
          "A ListView managed by ListViewDragDropReorderBehavior must have its ItemsSource set to an IList.");

      int oldIndex = _indexToSelect;
      int newIndex = IndexUnderDragCursor;

      // Dragging and dropping between lists not implemented, could be added
      // Dropping an item back onto itself is not considered an actual 'drop'
      if (newIndex < 0 || oldIndex < 0 || oldIndex == newIndex)
      {
        return;
      }
      // Move the dragged data object from it's original index to the
      // new index (according to where the mouse cursor is).
      var oldData = itemsSource[oldIndex];
      itemsSource.RemoveAt(oldIndex);
      itemsSource.Insert(newIndex, oldData);

      // Set the Effects property so that the call to DoDragDrop will return 'Move'.
      e.Effects = DragDropEffects.Move;
    }

    #endregion // Event Handling Methods

    #region Private Helpers

    private bool CanStartDragOperation
    {
      get
      {
        if (Mouse.LeftButton != MouseButtonState.Pressed)
          return false;

        if (!_canInitiateDrag)
          return false;

        if (_indexToSelect == -1)
          return false;

        if (!HasCursorLeftDragThreshold)
          return false;

        return true;
      }
    }

    private void FinishDragOperation(ListViewItem draggedItem, AdornerLayer adornerLayer)
    {
      // Let the ListViewItem know that it is not being dragged anymore.
      ListViewItemDragState.SetIsBeingDragged(draggedItem, false);

      ItemUnderDragCursor = null;

      // Remove the drag adorner from the adorner layer.
      if (adornerLayer != null)
      {
        adornerLayer.Remove(_dragAdorner);
        _dragAdorner = null;
      }
    }

    private ListViewItem GetListViewItem(int index)
    {
      if (_listView.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated)
        return null;

      return _listView.ItemContainerGenerator.ContainerFromIndex(index) as ListViewItem;
    }

    private ListViewItem GetListViewItem(object dataItem)
    {
      if (_listView.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated)
        return null;

      return _listView.ItemContainerGenerator.ContainerFromItem(dataItem) as ListViewItem;
    }


    private bool HasCursorLeftDragThreshold
    {
      get
      {
        if (_indexToSelect < 0)
          return false;

        ListViewItem item = GetListViewItem(_indexToSelect);
        Rect bounds = VisualTreeHelper.GetDescendantBounds(item);
        Point ptInItem = _listView.TranslatePoint(_mouseDownPos, item);

        // In case the cursor is at the very top or bottom of the ListViewItem
        // we want to make the vertical threshold very small so that dragging
        // over an adjacent item does not select it.
        double topOffset = Math.Abs(ptInItem.Y);
        double btmOffset = Math.Abs(bounds.Height - ptInItem.Y);
        double vertOffset = Math.Min(topOffset, btmOffset);

        double width = SystemParameters.MinimumHorizontalDragDistance * 2;
        double height = Math.Min(SystemParameters.MinimumVerticalDragDistance, vertOffset) * 2;
        Size szThreshold = new Size(width, height);

        Rect rect = new Rect(_mouseDownPos, szThreshold);
        rect.Offset(szThreshold.Width / -2, szThreshold.Height / -2);
        Point ptInListView = _listView.GetMousePosition();
        return !rect.Contains(ptInListView);
      }
    }

    /// <summary>
    /// Returns the index of the ListViewItem underneath the
    /// drag cursor, or -1 if the cursor is not over an item.
    /// </summary>
    private int IndexUnderDragCursor
    {
      get
      {
        int index = -1;
        for (int i = 0; i < _listView.Items.Count; ++i)
        {
          ListViewItem item = GetListViewItem(i);
          if (IsMouseOver(item))
          {
            index = i;
            break;
          }
        }
        return index;
      }
    }

    private AdornerLayer InitializeAdornerLayer(ListViewItem itemToDrag)
    {
      // Create a brush which will paint the ListViewItem onto
      // a visual in the adorner layer.
      VisualBrush brush = new VisualBrush(itemToDrag);

      // Create an element which displays the source item while it is dragged.
      _dragAdorner = new DragAdorner(_listView, itemToDrag.RenderSize, brush);

      // Set the drag adorner's opacity.		
      _dragAdorner.Opacity = DragAdornerOpacity;

      AdornerLayer layer = AdornerLayer.GetAdornerLayer(_listView);
      layer.Add(_dragAdorner);

      // Save the location of the cursor when the left mouse button was pressed.
      _mouseDownPos = _listView.GetMousePosition();

      return layer;
    }

    private void InitializeDragOperation(ListViewItem itemToDrag)
    {
      // Set some flags used during the drag operation.
      _canInitiateDrag = false;

      // Let the ListViewItem know that it is being dragged.
      ListViewItemDragState.SetIsBeingDragged(itemToDrag, true);
    }


    private bool IsMouseOver(Visual target)
    {
      // We need to use MouseUtilities to figure out the cursor
      // coordinates because, during a drag-drop operation, the WPF
      // mechanisms for getting the coordinates behave strangely.

      Rect bounds = VisualTreeHelper.GetDescendantBounds(target);
      Point mousePos = target.GetMousePosition();
      return bounds.Contains(mousePos);
    }


    /// <summary>
    /// Returns true if the mouse cursor is over a scrollbar in the ListView.
    /// </summary>
    private bool IsMouseOverScrollbar
    {
      get
      {
        Point ptMouse = _listView.GetMousePosition();
        HitTestResult res = VisualTreeHelper.HitTest(_listView, ptMouse);
        if (res == null)
          return false;

        DependencyObject depObj = res.VisualHit;
        while (depObj != null)
        {
          if (depObj is ScrollBar)
            return true;

          // VisualTreeHelper works with objects of type Visual or Visual3D.
          // If the current object is not derived from Visual or Visual3D,
          // then use the LogicalTreeHelper to find the parent element.
          if (depObj is Visual || depObj is System.Windows.Media.Media3D.Visual3D)
            depObj = VisualTreeHelper.GetParent(depObj);
          else
            depObj = LogicalTreeHelper.GetParent(depObj);
        }

        return false;
      }
    }

    private object ItemUnderDragCursor
    {
      get { return _itemUnderDragCursor; }
      set
      {
        if (_itemUnderDragCursor == value)
          return;

        // The first pass handles the previous item under the cursor.
        // The second pass handles the new one.
        for (int i = 0; i < 2; ++i)
        {
          if (i == 1)
            _itemUnderDragCursor = value;

          if (_itemUnderDragCursor != null)
          {
            ListViewItem listViewItem = GetListViewItem(_itemUnderDragCursor);
            if (listViewItem != null)
              ListViewItemDragState.SetIsUnderDragCursor(listViewItem, i == 1);
          }
        }
      }
    }


    private void PerformDragOperation()
    {
      object selectedItem = _listView.SelectedItem;
      DragDropEffects allowedEffects = DragDropEffects.Move | DragDropEffects.Move | DragDropEffects.Link;
      if (DragDrop.DoDragDrop(_listView, selectedItem, allowedEffects) != DragDropEffects.None)
      {
        // The item was dropped into a new location,
        // so make it the new selected item.
        _listView.SelectedItem = selectedItem;
      }
    }


    private bool ShowDragAdornerResolved => ShowDragAdorner && DragAdornerOpacity > 0.0;


    private void UpdateDragAdornerLocation()
    {
      if (_dragAdorner != null)
      {
        Point ptCursor = ListView.GetMousePosition();

        double left = ptCursor.X - _mouseDownPos.X;

        // 4/13/2007 - Made the top offset relative to the item being dragged.
        ListViewItem itemBeingDragged = GetListViewItem(_indexToSelect);
        Point itemLoc = itemBeingDragged.TranslatePoint(new Point(0, 0), ListView);
        double top = itemLoc.Y + ptCursor.Y - _mouseDownPos.Y;

        _dragAdorner.SetOffsets(left, top);
      }
    }

    #endregion // Private Helpers
  }


  #region ListViewItemDragState

  /// <summary>
  /// Exposes attached properties used in conjunction with the ListViewDragDropManager class.
  /// Those properties can be used to allow triggers to modify the appearance of ListViewItems
  /// in a ListView during a drag-drop operation.
  /// </summary>
  public static class ListViewItemDragState
  {
    /// <summary>
    /// Identifies the ListViewItemDragState's IsBeingDragged attached property.  
    /// This field is read-only.
    /// </summary>
    public static readonly DependencyProperty IsBeingDraggedProperty =
      DependencyProperty.RegisterAttached(
        "IsBeingDragged",
        typeof(bool),
        typeof(ListViewItemDragState),
        new UIPropertyMetadata(false));

    /// <summary>
    /// Returns true if the specified ListViewItem is being dragged, else false.
    /// </summary>
    /// <param name="item">The ListViewItem to check.</param>
    public static bool GetIsBeingDragged(ListViewItem item)
    {
      return (bool)item.GetValue(IsBeingDraggedProperty);
    }

    /// <summary>
    /// Sets the IsBeingDragged attached property for the specified ListViewItem.
    /// </summary>
    /// <param name="item">The ListViewItem to set the property on.</param>
    /// <param name="value">Pass true if the element is being dragged, else false.</param>
    internal static void SetIsBeingDragged(ListViewItem item, bool value)
    {
      item.SetValue(IsBeingDraggedProperty, value);
    }

    /// <summary>
    /// Identifies the ListViewItemDragState's IsUnderDragCursor attached property.  
    /// This field is read-only.
    /// </summary>
    public static readonly DependencyProperty IsUnderDragCursorProperty =
      DependencyProperty.RegisterAttached(
        "IsUnderDragCursor",
        typeof(bool),
        typeof(ListViewItemDragState),
        new UIPropertyMetadata(false));

    /// <summary>
    /// Returns true if the specified ListViewItem is currently underneath the cursor 
    /// during a drag-drop operation, else false.
    /// </summary>
    /// <param name="item">The ListViewItem to check.</param>
    public static bool GetIsUnderDragCursor(ListViewItem item)
    {
      return (bool)item.GetValue(IsUnderDragCursorProperty);
    }

    /// <summary>
    /// Sets the IsUnderDragCursor attached property for the specified ListViewItem.
    /// </summary>
    /// <param name="item">The ListViewItem to set the property on.</param>
    /// <param name="value">Pass true if the element is underneath the drag cursor, else false.</param>
    internal static void SetIsUnderDragCursor(ListViewItem item, bool value)
    {
      item.SetValue(IsUnderDragCursorProperty, value);
    }

  }

  #endregion // ListViewItemDragState
}

