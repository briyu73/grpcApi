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
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Xaml.Behaviors;

#nullable enable

namespace Swordfish.NET.WPF.Behaviors
{
  /// <summary>
  /// Attaches a count in brackets to the end of the text of the text block.
  /// No brakets or count added if the count is zero.
  /// Optionally sets the background to red if the count is above zero.
  /// See demo Swordfish.NET.Demo under
  /// "New 2023" -> "TextBlockAddCounter Test"
  /// </summary>
  /// <example>
  ///   xmlns:sf="http://swordfish.com.au/WPF/1.0"
  ///   ...
  ///   <TextBlock sf:TextBlockAddCounter.CounterSource="{Binding FilteredPackagesList.Count}" sf:TextBlockAddCounter.MakeRed="True">Filtered Packages</TextBlock>
  /// </example>
  public class TextBlockAddCounter : DependencyObject
  {
    public static bool GetMakeRed(DependencyObject obj)
    {
      return (bool)obj.GetValue(MakeRedProperty);
    }

    public static void SetMakeRed(DependencyObject obj, bool value)
    {
      obj.SetValue(MakeRedProperty, value);
    }

    // Using a DependencyProperty as the backing store for MakeRed.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty MakeRedProperty =
        DependencyProperty.RegisterAttached("MakeRed", typeof(bool), typeof(TextBlockAddCounter), new PropertyMetadata(false));


    public static int? GetCounterSource(DependencyObject obj)
    {
      return (int?)obj.GetValue(CounterSourceProperty);
    }

    public static void SetCounterSource(DependencyObject obj, int value)
    {
      obj.SetValue(CounterSourceProperty, value);
    }

    // Using a DependencyProperty as the backing store for CounterSource.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty CounterSourceProperty =
        DependencyProperty.RegisterAttached("CounterSource", typeof(int?), typeof(TextBlockAddCounter), new PropertyMetadata(null, (s, e) =>
        {
          if (s is TextBlock textBlock)
          {
            var rawText = textBlock.Text ?? "";
            SolidColorBrush? background = null;

            // Stip off the 
            if (!string.IsNullOrEmpty(rawText))
            {
              var lastSpacePosition = rawText.LastIndexOf(' ');
              if (lastSpacePosition > 0 && rawText.EndsWith(')'))
              {
                rawText = rawText.Substring(0, lastSpacePosition);
              }
            }
            if (e.NewValue is int count && count > 0)
            {
              rawText = $"{rawText} ({count})";
              if (GetMakeRed(textBlock))
              {
                background = new SolidColorBrush(Colors.Pink);
              }
            }
            textBlock.Text = rawText;
            textBlock.Background = background;
          }
        }));
  }
}
