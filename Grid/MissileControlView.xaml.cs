// Classification: OFFICIAL
//
// Copyright (C) 2022 Commonwealth of Australia.
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

using DryIoc;
using FlewsApp.Modules.CPX.Models;
using FlewsApp.Modules.CPX.ViewModels;
using System.Windows.Controls;

namespace FlewsApp.Modules.CPX.Views;

public partial class MissileControlView : UserControl
{
  public static IContainer? Container;

  public MissileControlView()
  {
    DataContext = Container?.Resolve<MissileControlViewModel>();
    InitializeComponent();
  }

  /// <summary>
  /// Handles when the Multi-Selected Items DataGrid adds columns to allow instant changing of
  /// value.s Don't do it with the main grid because there's an exception thrown due to a sort
  /// occuring during an edit.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  private void DataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
  {
    // Add properties to ignore here
    if (e.PropertyName == nameof(MissileCourseOfActionData.SimTime) ||
        e.PropertyName == nameof(MissileCourseOfActionData.LauncherEntity) ||
        e.PropertyName == nameof(MissileCourseOfActionData.MissileEntity) ||
        e.PropertyName == nameof(MissileCourseOfActionData.TargetEntity) ||
        e.PropertyName == nameof(MissileCourseOfActionData.MissileEntityStatus) ||
        e.PropertyName == nameof(MissileCourseOfActionData.TargetEntityStatus))
    {
      e.Cancel = true;
    }

    e.Column.Header = e.Column.Header.ToString() switch
    {
      nameof(MissileCourseOfActionData.FormattedSimTime) => "Sim Time",
      nameof(MissileCourseOfActionData.LauncherEntityName) => "Launcher Entity",
      nameof(MissileCourseOfActionData.LauncherEntityTeam) => "Team",
      nameof(MissileCourseOfActionData.MissileEntityName) => "Missile Entity",
      nameof(MissileCourseOfActionData.MissileEntityStatusStr) => "Missile Status",
      nameof(MissileCourseOfActionData.TargetEntNameOrPos) => "Target Entity/Position",
      nameof(MissileCourseOfActionData.TargetEntityStatusStr) => "Target Status",
      nameof(MissileCourseOfActionData.DistanceToTarget) => "Distance To Tgt",
      nameof(MissileCourseOfActionData.IsActionable) => "Is Actionable",
      nameof(MissileCourseOfActionData.ChangedTarget) => "Changed Target",
      _ => e.Column.Header.ToString(),
    };
  }
}
