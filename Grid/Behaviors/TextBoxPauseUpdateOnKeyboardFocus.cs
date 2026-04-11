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

using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Swordfish.NET.WPF.Behaviors;

public sealed class TextBoxPauseUpdateOnKeyboardFocus : Behavior<TextBox>
{
  private BindingExpression _previousBinding = null;

  protected override void OnAttached()
  {
    base.OnAttached();
    AssociatedObject.GotKeyboardFocus += AssociatedObject_GotKeyboardFocus;
    AssociatedObject.LostKeyboardFocus += AssociatedObject_LostKeyboardFocus;
  }

  private void AssociatedObject_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
  {
    AssociatedObject.SetBinding(TextBox.TextProperty, _previousBinding.ParentBinding);
  }

  private void AssociatedObject_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
  {
    _previousBinding = AssociatedObject.GetBindingExpression(TextBox.TextProperty);

    // Create a new binding that goes on one way back to the source
    // Some extra parameters may need to be added for your use case

    var binding = new Binding { Mode = BindingMode.OneWayToSource };
    if (_previousBinding.ParentBinding.Source != null)
    {
      binding.Source = _previousBinding.ParentBinding.Source;
    }
    if (_previousBinding.ParentBinding.Path?.Path != null)
    {
      binding.Path = new PropertyPath(_previousBinding.ParentBinding.Path.Path);
    }
    if (_previousBinding.ParentBinding.Converter != null)
    {
      binding.Converter = _previousBinding.ParentBinding.Converter;
    }
    if (_previousBinding.ParentBinding.ConverterParameter != null)
    {
      binding.ConverterParameter = _previousBinding.ParentBinding.ConverterParameter;
    }
    AssociatedObject.SetBinding(TextBox.TextProperty, binding);
  }

  protected override void OnDetaching()
  {
    AssociatedObject.GotKeyboardFocus -= AssociatedObject_GotKeyboardFocus;
    AssociatedObject.LostKeyboardFocus -= AssociatedObject_LostKeyboardFocus;
    base.OnDetaching();
  }

}
