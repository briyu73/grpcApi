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

using Swordfish.NET.General;
using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Swordfish.NET.WPF.Controls;

public class DigitalClock : Control
{
  // **************************************************************************
  // Private Fields
  // **************************************************************************
  #region Private Fields

  private readonly ThrottledAction _setTimeThrottle;
  private readonly ThrottledAction _invalidateThrottle;
  private readonly GlyphTypeface _typeFace1 = new GlyphTypeface(new Uri("pack://application:,,,/Swordfish.NET.WPF;component/Resources/digital.ttf"));
  private readonly GlyphTypeface _typeFace2 = new GlyphTypeface(new Uri("pack://application:,,,/Swordfish.NET.WPF;component/Resources/Electrolize-Regular.ttf"));
  private readonly GlyphTypeface _typeFace3 = new GlyphTypeface(new Uri("pack://application:,,,/Swordfish.NET.WPF;component/Resources/Michroma-Regular.ttf"));
  private readonly GlyphTypeface _typeFace4 = new GlyphTypeface(new Uri("pack://application:,,,/Swordfish.NET.WPF;component/Resources/Orbitron-Regular.ttf"));
  private GlyphTypeface _selectedTypeFace;
  private GeometryGroup _foreground = null;
  private TimeSpan _displayTime = TimeSpan.FromSeconds(0);
  private double _scaleX;

  #endregion Private Fields

  // **************************************************************************
  // Public Methods
  // **************************************************************************
  #region Public Methods


  public DigitalClock()
  {
    SnapsToDevicePixels = true;
    Foreground = new SolidColorBrush(Color.FromRgb(0x76, 0xFF, 0x05));
    // Throttle to every 50ms to be close to the accuracy of the clock
    _setTimeThrottle = new ThrottledAction(TimeSpan.FromMilliseconds(50));
    _invalidateThrottle = new ThrottledAction(TimeSpan.FromMilliseconds(50));

    // Use these 2 together to get the font looking perfect
    _selectedTypeFace = _typeFace3;
    // Shrink the width to make _typeFace3 less fat
    _scaleX = 0.7;
  }

  /// <summary>
  /// Creates a bitmap containing all the numbers and letters in the font family passed in,
  /// and saves the bitmap to the filename passed in.
  /// </summary>
  /// <param name="filename"></param>
  /// <param name="text"></param>
  /// <param name="fontSize"></param>
  public void CreateBitmap(string filename, string text, double fontSize)
  {
    var foreground = CalculateForegroundGroupFromFont(fontSize, text);
    var size = DoMeasure(fontSize, text);

    // The Visual to use as the source of the RenderTargetBitmap.
    DrawingVisual drawingVisual = new DrawingVisual();
    DrawingContext drawingContext = drawingVisual.RenderOpen();
    drawingContext.DrawGeometry(Background, null, new RectangleGeometry(new Rect(size)));
    drawingContext.DrawGeometry(Foreground, null, foreground);
    drawingContext.Close();

    // The BitmapSource that is rendered with a Visual.
    RenderTargetBitmap rtb = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
    rtb.Render(drawingVisual);

    // Encoding the RenderBitmapTarget as a PNG file.
    PngBitmapEncoder png = new PngBitmapEncoder();
    png.Frames.Add(BitmapFrame.Create(rtb));
    using (Stream fileStream = File.Create(filename))
    {
      png.Save(fileStream);
    }
  }

  #endregion Public Methods

  // **************************************************************************
  // Protected Methods
  // **************************************************************************
  #region Protected Methods

  protected override Size MeasureOverride(Size constraint)
  {
    double fontSize = FontSize;
    string text = ClockText;
    return DoMeasure(fontSize, text);
  }

  /// <summary>
  /// Dammit this would've been much easier if the stupid digital.ttf font was a proportional font
  /// </summary>
  /// <param name="drawingContext"></param>
  protected override void OnRender(DrawingContext drawingContext)
  {
    _foreground ??= CalculateForegroundGroupFromFont(FontSize, ClockText);
    drawingContext.DrawGeometry(Background, null, new RectangleGeometry(new Rect(new Size(ActualWidth, ActualHeight))));
    drawingContext.DrawGeometry(Foreground, null, _foreground);
  }

  #endregion Protected Methods


  // **************************************************************************
  // Private Methods
  // **************************************************************************
  #region Private Methods

  /// <summary>
  /// Sets the time to be displayed
  /// </summary>
  /// <param name="timeSpan"></param>
  private void SetTime(TimeSpan timeSpan)
  {
    _displayTime = timeSpan;
    var context = SynchronizationContext.Current;
    _setTimeThrottle.InvokeAction(() =>
    {
      Func<int, string> D2 = x => x.ToString().PadLeft(2, '0');
      Func<int, string> T1 = x => x.ToString().PadLeft(3, '0').Substring(0, 1);
      string text = $"{D2(_displayTime.Hours)}:{D2(_displayTime.Minutes)}:{D2(_displayTime.Seconds)}:{T1(_displayTime.Milliseconds)}";
      context.Send(d => ClockText = (string)d, text);
    });
  }

  /// <summary>
  /// Function that measures the total size of the text being displayed
  /// </summary>
  /// <returns></returns>
  private Size DoMeasure(double fontSize, string text)
  {
    var characterSize = GetCharacterSize(fontSize);

    double x = 0;
    double y = characterSize.Height;
    for (int i = 0; i < text.Length; i++)
    {
      ushort glyphIndex = _selectedTypeFace.CharacterToGlyphMap[text[i]];
      double width = _selectedTypeFace.AdvanceWidths[glyphIndex] * fontSize;
      if (_forceMonoSpacing && char.IsLetterOrDigit(text[i]))
      {
        x += characterSize.Width;
      }
      else
      {
        x += width;
      }
    }
    return new Size(x, y);
  }

  /// <summary>
  /// Gets a fixed character size for the font passed in
  /// </summary>
  /// <param name="fontSize"></param>
  /// <returns></returns>
  private Size GetCharacterSize(double fontSize)
  {
    ushort glyphIndex = _selectedTypeFace.CharacterToGlyphMap['8'];
    double width = _selectedTypeFace.AdvanceWidths[glyphIndex] * fontSize * _scaleX + 1;
    double height = _selectedTypeFace.AdvanceHeights[glyphIndex] * fontSize;
    return new Size(width, height);
  }

  /// <summary>
  /// Precalculate the geometry, ideally on a background (throttled) task
  /// </summary>
  /// <param name="fontSize"></param>
  /// <param name="clockText"></param>
  private GeometryGroup CalculateForegroundGroupFromFont(double fontSize, string text)
  {
    var characterSize = GetCharacterSize(fontSize);

    GeometryGroup group = new GeometryGroup();
    group.FillRule = FillRule.Nonzero;

    double x = 0;
    double y = characterSize.Height;
    for (int i = 0; i < text.Length; i++)
    {
      ushort glyphIndex = _selectedTypeFace.CharacterToGlyphMap[text[i]];
      Geometry glyphGeometry = _selectedTypeFace.GetGlyphOutline(glyphIndex, fontSize, fontSize);
      TransformGroup glyphTransform = new TransformGroup();
      double width = _selectedTypeFace.AdvanceWidths[glyphIndex] * fontSize;
      if (_forceMonoSpacing && char.IsLetterOrDigit(text[i]))
      {
        glyphTransform.Children.Add(new ScaleTransform(_scaleX, 1.0));
        glyphTransform.Children.Add(new TranslateTransform(x + characterSize.Width - width * _scaleX, y));
        x += characterSize.Width;
      }
      else
      {
        glyphTransform.Children.Add(new TranslateTransform(x, y));
        x += width;
      }

      glyphGeometry.Transform = glyphTransform;
      group.Children.Add(glyphGeometry);
    }

    group.Transform = new TranslateTransform(0, (group.Bounds.Height - characterSize.Height) / 2);
    group.Freeze();

    return group;
  }

  #endregion Private Methods

  // **************************************************************************
  // Properties
  // **************************************************************************
  #region Properties

  /// <summary>
  /// Flag to let the dev specify if we render from font, or render using the precalculated bitmaps
  /// for convenience we also have a BitmapBased flag that is the opposite of this one.
  /// </summary>
  public bool FontBased
  {
    get => _fontBased;
    set
    {
      _fontBased = value;
      _bitmapBased = !value;
    }
  }
  private bool _fontBased = true;

  /// <summary>
  /// Flag to let the dev specify if we render from font, or render using the precalculated bitmaps
  /// for convenience we also have a FontBased flag that is the opposite of this one.
  /// </summary>
  public bool BitmapBased
  {
    get => _bitmapBased;
    set
    {
      _bitmapBased = value;
      _fontBased = !value;
    }
  }
  private bool _bitmapBased = false;

  /// <summary>
  /// Force mono spacing, in case the font being used is a proportional font
  /// </summary>
  public bool ForceMonoSpacing
  {
    get => _forceMonoSpacing;
    set => _forceMonoSpacing = value;
  }
  private bool _forceMonoSpacing = true;

  #endregion Properties


  // **************************************************************************
  // Dependency Properties
  // **************************************************************************
  #region Dependency Properties

  public string ClockText
  {
    get { return (string)GetValue(ClockTextProperty); }
    set { SetValue(ClockTextProperty, value); }
  }
  private string _clockText = "00:00:00.0"; // variable that can be used on background thread

  // Using a DependencyProperty as the backing store for ClockText.  This enables animation, styling, binding, etc...
  public static readonly DependencyProperty ClockTextProperty =
      DependencyProperty.Register("ClockText", typeof(string), typeof(DigitalClock), new FrameworkPropertyMetadata("00:00:00.0", (s, e) =>
      {
        if (s is DigitalClock clock)
        {
          var context = SynchronizationContext.Current;
          var fontSize = clock.FontSize;
          clock._clockText = e.NewValue as string;
          clock._invalidateThrottle.InvokeAction(() =>
          {
            clock._foreground = clock.CalculateForegroundGroupFromFont(fontSize, clock._clockText);
            context.Send(d =>
            {
              clock.InvalidateMeasure();
              clock.InvalidateVisual();
            }, null);
          });
        }
      }));

  // --------------------------------------------------------------------------

  public object TimeSource
  {
    get { return (object)GetValue(TimeSourceProperty); }
    set { SetValue(TimeSourceProperty, value); }
  }

  // Using a DependencyProperty as the backing store for TimeSource.  This enables animation, styling, binding, etc...
  public static readonly DependencyProperty TimeSourceProperty =
      DependencyProperty.Register("TimeSource", typeof(object), typeof(DigitalClock), new FrameworkPropertyMetadata(null, (s, e) =>
      {
        DigitalClock clock = s as DigitalClock;
        if (clock != null)
        {
          if (e.NewValue is TimeSpan timeSpan)
          {
            clock.SetTime(timeSpan);
          }
          if (e.NewValue is double doubleValue)
          {
            clock.SetTime(TimeSpan.FromSeconds(doubleValue));
          }
          if (e.NewValue is DateTime dateTime)
          {
            TimeSpan timeSpanFromDateTime = new TimeSpan(0, dateTime.Hour, dateTime.Minute, dateTime.Second, dateTime.Millisecond);
            clock.SetTime(timeSpanFromDateTime);
          }
        }
      }));

  #endregion Dependency Properties
}
