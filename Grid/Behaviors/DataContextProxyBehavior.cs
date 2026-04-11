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

using System;
using System.Security.Cryptography;
using System.Threading;
using System.Windows;
using System.Windows.Data;
using Microsoft.Xaml.Behaviors;

#nullable enable

namespace Swordfish.NET.WPF.Behaviors
{
  /// <summary>
  /// This is a DataContextProxy, you put in in your xaml file where you want to capture
  /// the data context, and then you can use it downstream.
  ///
  /// Use this at the top of your .xaml file:
  ///
  ///     xmlns:i="http://schemas.microsoft.com/xaml/behaviors"
  ///
  /// Use this at the top of you .cs file
  ///
  ///     using Microsoft.Xaml.Behaviors
  ///
  /// DO NOT use this old version (that is included in Prism 7.x):
  /// Don't use    xmlns:i="http://schemas.microsoft.com/expression/2010/interactivity"
  /// Don't use    using System.Windows.Interactivity;

  /// Usage Example:
  ///
  /// xmlns:sf="http://swordfish.com.au/WPF/1.0"
  /// xmlns:i="http://schemas.microsoft.com/xaml/behaviors"
  ///
  /// <DataGrid ItemsSource="{Binding JRFLList}" sf:DataGridSelectedItemsBinding.SelectedValues="{Binding SelectedJRFLItems}" VirtualizingPanel.IsVirtualizing="True">
  ///   <i:Interaction.Behaviors>
  ///     <sf:DataContextProxyBehavior />
  ///   </i:Interaction.Behaviors>
  ///   <DataGrid.Columns>
  ///     <DataGridTextColumn
  ///         Header = "Comments"
  ///         Binding="{Binding COMMENTS}"
  ///         Visibility="{Binding
  ///             Source={StaticResource DataContextProxy},
  ///             Path=DataSource.ShowOnlyLocationAndTimes,
  ///             Converter={sf:InverseVisibilityConverter}}"/>
  ///   </DataGrid.Columns>
  /// </DataGrid>
  ///
  /// Here's an alternate usage example that uses the TargetType and Override properties:
  ///
  /// <DataGrid
  ///     ItemsSource="{Binding JRFLList}"
  ///     sf:DataGridSelectedItemsBinding.SelectedValues="{Binding SelectedJRFLItems}"
  ///     VirtualizingPanel.IsVirtualizing="True">
  ///   <i:Interaction.Behaviors>
  ///     <sf:DataContextProxyBehavior
  ///         TargetType="{x:Type Visibility}"
  ///         Override="{Binding
  ///             ShowOnlyLocationAndTimes,
  ///             Converter={sf:InverseVisibilityConverter}}" />
  ///   </i:Interaction.Behaviors>
  ///   <DataGrid.Columns>
  ///     <DataGridTextColumn
  ///         Header = "Comments"
  ///         Binding="{Binding COMMENTS}"
  ///         Visibility="{Binding
  ///             Source={StaticResource DataContextProxy},
  ///             Path=DataSource}"/>
  ///   </DataGrid.Columns>
  /// </DataGrid>
  /// 
  /// </summary>
  public class DataContextProxyBehavior : Behavior<FrameworkElement>
  {
    protected override void OnAttached()
    {
      base.OnAttached();

      // Add the proxy to the resource collection of the target
      // so it will be available to nested controls
      AssociatedObject.Resources.Add(
          "DataContextProxy",
          this
      );

      // Postpone setting the data context to the next pass of
      // the UI thread because initialially we have no local
      // data context, only a more global one, which confuses
      // controls that are bound to this and causes Binding errors
      if (SynchronizationContext.Current != null)
      {
        SynchronizationContext.Current.Post(x =>
        {
          //// Binds the target datacontext to the proxy,
          //// so whenever it changes the proxy will be updated
          var binding = new Binding();
          binding.Source = AssociatedObject;
          binding.Mode = BindingMode.OneWay;
          if (Override == null)
          {
            // No override, just use DataContext
            binding.Path = new PropertyPath("DataContext");
          }
          else if (Override.Source == null)
          {
            // Overriding just the Path
            binding.Path = new PropertyPath("DataContext." + Override.Path.Path);
            binding.Converter = Override.Converter;
          }
          else
          {
            // Override the Path and the Source
            binding.Path = Override.Path;
            binding.Converter = Override.Converter;
            binding.Source = Override.Source;
          }
          BindingOperations.SetBinding(this, DataContextProxyBehavior.DataSourceProperty, binding);
        }, null);
      }
      else
      {
        throw new Exception("Behaviour requires SynchronizationContext to be set!");
      }
    }

    protected override void OnDetaching()
    {
      base.OnDetaching();

      // Remove the proxy from the Resources
      AssociatedObject.Resources.Remove(
          "DataContextProxy"
      );
    }

    private Type? _targetType;
    public Type? TargetType
    {
      get => _targetType;
      set
      {
        _targetType = value;

        // Ensure we don't have a wrong type sitting in DataSource
        // otherwise we could get an exception thrown on setup or teardown
        if (_targetType != null && !_targetType.IsAssignableFrom(DataSource?.GetType()))
        {
          DataSource = _targetType.IsValueType ? Activator.CreateInstance(_targetType) : null;
        }
      }
    }

    /// <summary>
    /// Sets the override, note this is a property, not a dependency property, can be confusing
    /// because you assign a binding to it, but its just a regular property, and with good reason
    /// because we need to break out the binding object into it's constituants, and also properties
    /// get set up front when assigning in Xaml, but dependency properties aren't evaluated until
    /// after.
    /// </summary>
    public Binding? Override
    {
      get;
      set;
    }

    /// <summary>
    /// Stores the DataContext
    /// </summary>
    public object? DataSource
    {
      get { return (object)GetValue(DataSourceProperty); }
      set { SetValue(DataSourceProperty, value); }
    }
    public static readonly DependencyProperty DataSourceProperty =
        DependencyProperty.Register("DataSource", typeof(object), typeof(DataContextProxyBehavior), new FrameworkPropertyMetadata(null, null, CoerceDataSourceCallback));

    private static object? CoerceDataSourceCallback(DependencyObject d, object baseValue)
    {
      // Ensure that we are returning a value that matches the target type
      // Particularly important for enumerated types as an exception will be
      // thrown if the wrong type is returned during setup or teardown of the
      // visual tree.
      if (d is DataContextProxyBehavior behavior && behavior.TargetType != null)
      {
        var targetType = behavior.TargetType;
        if (!targetType.IsAssignableFrom(baseValue?.GetType()))
        {
          return targetType.IsValueType? Activator.CreateInstance(targetType) : null;
        }
      }
      return baseValue;
    }

  }
}
