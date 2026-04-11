// Classification: OFFICIAL
//
// Copyright (C) 2024 Commonwealth of Australia.
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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Swordfish.NET.WPF.Behaviors;

/// <summary>
/// Behaviour which will "forward" attached properties Canvas.Left and Canvas.Top
/// to a parent ContentPresenter.
/// </summary>
public class ForwardCanvasAttachedPropertiesBehavior : Behavior<FrameworkElement>
{
  protected override void OnAttached()
  {
    base.OnAttached();
    if (VisualTreeHelper.GetParent(AssociatedObject) is not ContentPresenter parentContentPresenter)
    {
      return;
    }

    parentContentPresenter.SetBinding(Canvas.LeftProperty, new Binding
    {
      Source = AssociatedObject,
      Path = new PropertyPath(Canvas.LeftProperty),
      Mode = BindingMode.OneWay
    });
    parentContentPresenter.SetBinding(Canvas.TopProperty, new Binding
    {
      Source = AssociatedObject,
      Path = new PropertyPath(Canvas.TopProperty),
      Mode = BindingMode.OneWay
    });
  }

  protected override void OnDetaching()
  {
    if (VisualTreeHelper.GetParent(AssociatedObject) is not ContentPresenter parentContentPresenter)
    {
      return;
    }

    BindingOperations.ClearBinding(parentContentPresenter, Canvas.LeftProperty);
    BindingOperations.ClearBinding(parentContentPresenter, Canvas.TopProperty);
    base.OnDetaching();
  }
}
