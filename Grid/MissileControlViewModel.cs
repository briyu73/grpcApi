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

using EntityStateLib;
using FlewsApp.Common.DataModels.UI.Models;
using FlewsApp.Common.Infrastructure.Messaging;
using FlewsApp.Modules.CPX.Interface;
using FlewsApp.Modules.CPX.Models;
using FlewsApp.Modules.DataHub.Interface;
using Prism.Events;
using Swordfish.NET.Collections;
using Swordfish.NET.ViewModel;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using static FlewsApp.Modules.CPX.Services.CPXCommandService;

namespace FlewsApp.Modules.CPX.ViewModels;

/// <summary>
/// Contains logic for the Missile Control tab.
/// Only visible for adjudicators.
/// </summary>
[TabVisibility(Adjudicator = true, Commander = false)]
public class MissileControlViewModel : BaseCommandMessagesViewModel
{
  // *******************************************************************************************
  // Types
  // *******************************************************************************************

  #region Enumerated Types

  /// <summary>
  /// Enumerated type used to determing the source of a group or sort change, which
  /// can either be a change in the Group/Sort options, or a change from the DataGrid
  /// when its column headers are clicked.
  /// </summary>
  private enum UpdateMode
  {
    NotUpdating,
    UpdatingFromGroupSortOptions,
    UpdatingFromDataGrid
  }

  #endregion Enumerated Types

  // *******************************************************************************************
  // Fields
  // *******************************************************************************************

  #region Fields

  /// <summary>
  /// Tracks the current update mode, whether from the group/sort options, or from
  /// clicking on the DataGrid headers.
  /// </summary>
  private UpdateMode _updateMode = UpdateMode.NotUpdating;
  private readonly ISimTimeService _simTimeService;

  #endregion Fields

  // *******************************************************************************************
  // Properties
  // *******************************************************************************************

  #region Properties

  /// <summary>
  /// List of missiles fired while the CPX tool has been open.
  /// </summary>
  public ConcurrentObservableCollection<MissileCourseOfActionData> FiredMissileList { get; } = new();

  public ObservableCollection<SortDescription> SortDescriptions { get; } = new ObservableCollection<SortDescription>();

  public ObservableCollection<GroupDescription> GroupDescriptions { get; } = new ObservableCollection<GroupDescription>();

  public ObservableCollection<SortDescriptionOption> SortDescriptionOptions { get; } =
  new ObservableCollection<SortDescriptionOption>
  (
    new[]
    {
      new SortDescriptionOption(true, nameof(CommandRecord.SimTime)),
      new SortDescriptionOption(true, nameof(MissileCourseOfActionData.LauncherEntityName)),
      new SortDescriptionOption(false, nameof(MissileCourseOfActionData.LauncherEntityTeam)),
      new SortDescriptionOption(false, nameof(MissileCourseOfActionData.MissileEntityName)),
      new SortDescriptionOption(false, nameof(MissileCourseOfActionData.TargetEntNameOrPos)),
      new SortDescriptionOption(false, nameof(MissileCourseOfActionData.MissileEntityStatusStr)),
      new SortDescriptionOption(false, nameof(MissileCourseOfActionData.TargetEntityStatusStr)),
      new SortDescriptionOption(false, nameof(MissileCourseOfActionData.DistanceToTarget)),
      new SortDescriptionOption(false, nameof(MissileCourseOfActionData.IsActionable)),
    }
  );


  public ObservableCollection<GroupDescriptionOption> GroupDescriptionOptions { get; } =
  new ObservableCollection<GroupDescriptionOption>
  (
    new[]
    {
      new GroupDescriptionOption(false, nameof(MissileCourseOfActionData.LauncherEntityName)),
      new GroupDescriptionOption(false, nameof(MissileCourseOfActionData.LauncherEntityTeam)),
      new GroupDescriptionOption(false, nameof(MissileCourseOfActionData.MissileEntityName)),
      new GroupDescriptionOption(false, nameof(MissileCourseOfActionData.TargetEntNameOrPos)),
      new GroupDescriptionOption(false, nameof(MissileCourseOfActionData.IsActionable)),
      new GroupDescriptionOption(false, nameof(MissileCourseOfActionData.ChangedTarget)),
    }
  );


  private Predicate<object>? _filter;
  public Predicate<object>? Filter
  {
    get => _filter;
    set => SetProperty(ref _filter, value);
  }

  public ConcurrentObservableCollection<MissileCourseOfActionData> MultiSelectedFiredMissilesDetails { get; } = new();

  private bool _doActionableFiltering;
  public bool DoActionableFiltering
  {
    get => _doActionableFiltering;
    set
    {
      if (SetProperty(ref _doActionableFiltering, value))
      {
        if (_doActionableFiltering)
        {
          Filter = p => p is MissileCourseOfActionData mcoad ?
                  mcoad.IsActionable : false;
        }
        else
        {
          Filter = null;
        }
      }
    }
  }

  /// <summary>
  /// Selected sensor to control.
  /// </summary>
  private Sensor? _selectedTargetSensor;
  public Sensor? SelectedTargetSensor
  {
    get => _selectedTargetSensor;
    set => SetProperty(ref _selectedTargetSensor, value);
  }

  /// <summary>
  /// Selected commkit to control.
  /// </summary>
  private CommKit? _selectedTargetCommkit;
  public CommKit? SelectedTargetCommkit
  {
    get => _selectedTargetCommkit;
    set => SetProperty(ref _selectedTargetCommkit, value);
  }

  /// <summary>
  /// Selected (detonated) missile.
  /// </summary>
  private MissileCourseOfActionData? _selectedFiredMissile;
  public MissileCourseOfActionData? SelectedFiredMissile
  {
    get => _selectedFiredMissile;
    set
    {
      if (SetProperty(ref _selectedFiredMissile, value))
      {
        // clear target sensor and commkit selections
        SelectedTargetSensor = null;
        SelectedTargetCommkit = null;
      }
      PublishSelectedEntity();
    }
  }

  #endregion

  // *******************************************************************************************
  // Commands
  // *******************************************************************************************

  #region Commands

  private readonly RelayCommandFactory _damageTargetCommand = new();
  public ICommand DamageTargetCommand => _damageTargetCommand.GetCommand(
    () =>
    {
      object[] args = { SelectedFiredMissile!.TargetEntity!, EntityStatus.ES_CONFIRMED_DEAD };
      _cpxCommandService.SendDataHubMessage(CPXMessageType.ENTITY_STATUS, args);
    },
    () => _simControlsService.IsScenarioRunning && SelectedFiredMissile != null && SelectedFiredMissile.IsActionable &&
          SelectedFiredMissile.TargetEntity != null);

  private readonly RelayCommandFactory _destroyTargetCommand = new();
  public ICommand DestroyTargetCommand => _destroyTargetCommand.GetCommand(
    () =>
    {
      object[] args = { SelectedFiredMissile!.TargetEntity!, EntityStatus.ES_REMOVED };
      _cpxCommandService.SendDataHubMessage(CPXMessageType.ENTITY_STATUS, args);
      SelectedFiredMissile = null;
    },
    () => _simControlsService.IsScenarioRunning && SelectedFiredMissile != null && SelectedFiredMissile.IsActionable &&
          SelectedFiredMissile.TargetEntity != null);

  private readonly RelayCommandFactory _disableSensorCommand = new();
  public ICommand DisableSensorCommand => _disableSensorCommand.GetCommand(
    () =>
    {
      object[] args = { SelectedFiredMissile!.TargetEntity!, SystemControlRequest.ControlType.DISABLE_SENSOR, SelectedTargetSensor!.SensorId };
      _cpxCommandService.SendDataHubMessage(CPXMessageType.SYSTEM_CONTROL, args);

      SelectedTargetSensor = null;
    },
    () => _simControlsService.IsScenarioRunning && SelectedFiredMissile != null && SelectedFiredMissile.IsActionable &&
          SelectedFiredMissile.TargetEntity != null && SelectedTargetSensor != null);

  private readonly RelayCommandFactory _disableCommkitCommand = new();
  public ICommand DisableCommkitCommand => _disableCommkitCommand.GetCommand(
    () =>
    {
      object[] args = { SelectedFiredMissile!.TargetEntity!, SystemControlRequest.ControlType.DISABLE_COMMKIT, SelectedTargetCommkit!.NetworkId };
      _cpxCommandService.SendDataHubMessage(CPXMessageType.SYSTEM_CONTROL, args);

      SelectedTargetCommkit = null;
    },
    () => _simControlsService.IsScenarioRunning && SelectedFiredMissile != null && SelectedFiredMissile.IsActionable &&
          SelectedFiredMissile.TargetEntity != null && SelectedTargetCommkit != null);

  private readonly RelayCommandFactory _clearFiredMissilesCommand = new();
  public ICommand ClearFiredMissilesCommand => _clearFiredMissilesCommand.GetCommand(
    ClearFiredMissilesTable,
    () => FiredMissileList.Any());

  #endregion

  // *******************************************************************************************
  // Public Methods
  // *******************************************************************************************

  #region Public Methods

  public MissileControlViewModel(
    ICPXCommandService cpxCommandService,
    IEntityListService entityListService,
    ICPXModeService cpxModeService,
    IEventAggregator eventAggregator,
    ISimControlsService simControlsService,
    ISimTimeService simTimeService) :
   base(cpxCommandService, entityListService, cpxModeService, eventAggregator, simControlsService)
  {
    _simTimeService = simTimeService;
    var worldState = WorldState.GetInstance(_simControlsService.DataHubClient);

    worldState.MissileLaunched += WorldState_MissileLaunched;
    worldState.MissileDetonated += WorldState_MissileDetonated;

    // Hook into the sort/group descriptions to handle when they change
    HookDescriptions();

    // Hook into the sort/group options (editable list of descriptions)
    HookOptions();

    SortDescriptionOptions.First().Descending = true;
  }

  public override void Activate()
  {
    base.Activate();
    PublishSelectedEntity();
  }

  #endregion

  // *******************************************************************************************
  // Protected Methods
  // *******************************************************************************************

  #region Protected Methods

  protected override void OnScenarioStopped()
  {
    SelectedFiredMissile = null;

    ClearFiredMissilesTable();
  }

  #endregion

  // *******************************************************************************************
  // Private Parts
  // *******************************************************************************************

  #region Privates

  private void WorldState_MissileLaunched(Entity launcherEntity, MissileLaunchData launchData)
  {
    // only log info from the same team if we are in commander role
    if (_cpxModeService.IsCommanderRole && launcherEntity.Team != _cpxModeService.CommanderColour)
    {
      return;
    }

    var missileEntity = EntityList.FirstOrDefault(ent => ent.UID == launchData.MissileId);
    if (missileEntity == null)
    {
      _cpxCommandService.LogError($"Missile was launched with ID '{launchData.MissileId}' but couldn't find it in the scenario");
      return;
    }

    var targetEntity = EntityList.FirstOrDefault(ent => ent.UID == launchData.TargetEntityId);
    if (targetEntity == null)
    {
      if (launchData.TargetEntityId != Entity.DefaultUID)
      {
        _cpxCommandService.LogError($"Missile '{missileEntity.Name}' was launched at the target entity with ID '{launchData.TargetEntityId}' but couldn't find it in the scenario");
        return;
      }
    }

    FiredMissileList.Add(new MissileCourseOfActionData(_simTimeService.SimTime, launcherEntity, missileEntity, targetEntity, launchData.TargetPosition));
  }

  private void WorldState_MissileDetonated(Entity sourceEntity, MissileDetonateData detonateData)
  {
    // only log info from the same team if we are in commander role
    if (_cpxModeService.IsCommanderRole && sourceEntity.Team != _cpxModeService.CommanderColour)
    {
      return;
    }

    var missileCoAData = FiredMissileList.FirstOrDefault(fm => fm.MissileEntity.UID == detonateData.MissileId);
    if (missileCoAData == null)
    {
      _cpxCommandService.LogError($"Missile launched by {sourceEntity.Name} detonated but couldn't find matching launch data!");
      return;
    }

    if (detonateData.TargetEntityId != Entity.DefaultUID) // detonation had a target
    {
      // may or may not have an original target entity
      var originalTarget = EntityList.FirstOrDefault(e => e.UID == missileCoAData.TargetEntity?.UID);

      var detonatedTarget = EntityList.FirstOrDefault(e => e.UID == detonateData.TargetEntityId);

      var prefix = $"Missile '{missileCoAData.MissileEntityName}' launched by '{sourceEntity.Name}'";

      if (originalTarget == null)
      {
        if (detonatedTarget == null)
        {
          _cpxCommandService.LogMessage($"{prefix} with no original target has detonated");
        }
        else
        {
          _cpxCommandService.LogMessage($"{prefix} with no original target has detonated at new target '{detonatedTarget.Name}'");

          // update the view with the new target
          missileCoAData.SetDetonatedTarget(detonatedTarget, detonateData.DetonationLocation, true);

          // clear target sensor and commkit selections
          SelectedTargetSensor = null;
          SelectedTargetCommkit = null;
        }
      }
      else
      {
        if (detonatedTarget == null)
        {
          _cpxCommandService.LogMessage($"{prefix} with original target '{originalTarget.Name}' has detonated");
        }
        else
        {
          _cpxCommandService.LogMessage($"{prefix} with original target '{originalTarget.Name}' has detonated at new target '{detonatedTarget.Name}'");

          // update the view with the new target
          missileCoAData.SetDetonatedTarget(detonatedTarget, detonateData.DetonationLocation,
            detonateData.TargetEntityId == missileCoAData.TargetEntity?.UID);

          // clear target sensor and commkit selections
          SelectedTargetSensor = null;
          SelectedTargetCommkit = null;
        }
      }
    }
    else // detonation had no target
    {
      _cpxCommandService.LogMessage($"Missile '{missileCoAData.MissileEntityName}' launched by '{sourceEntity.Name}' has detonated with no target info");
    }
  }

  private void ClearFiredMissilesTable()
  {
    SelectedFiredMissile = null;

    foreach (var firedMissile in FiredMissileList)
    {
      firedMissile.Dispose();
    }
    FiredMissileList.Clear();
  }

  /// <summary>
  /// Hooks into the sort/group descriptions to handle when they change
  /// </summary>
  private void HookDescriptions()
  {
    GroupDescriptions.CollectionChanged += GroupDescriptions_CollectionChanged;
    SortDescriptions.CollectionChanged += SortDescriptions_CollectionChanged;
  }

  private void GroupDescriptions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
  {
    var oldItem = e.OldItems?.OfType<PropertyGroupDescription>().FirstOrDefault();
    var newItem = e.NewItems?.OfType<PropertyGroupDescription>().FirstOrDefault();

    if (_updateMode == UpdateMode.NotUpdating)
    {
      _updateMode = UpdateMode.UpdatingFromDataGrid;

      switch (e.Action)
      {
        case NotifyCollectionChangedAction.Add:
          if (newItem != null)
          {
            // Turn off GroupDescriptionOption
            var option = GroupDescriptionOptions.FirstOrDefault(o => o.PropertyName == newItem.PropertyName);
            if (option != null)
            {
              option.IsActive = true;
              GroupDescriptionOptions.Remove(option);
              GroupDescriptionOptions.Insert(0, option);
            }
          }
          break;
        case NotifyCollectionChangedAction.Remove:
          if (oldItem != null)
          {
            // Turn off GroupDescriptionOption
            var option = GroupDescriptionOptions.FirstOrDefault(o => o.PropertyName == oldItem.PropertyName);
            if (option != null)
            {
              option.IsActive = false;
            }
          }
          break;
      }

      _updateMode = UpdateMode.NotUpdating;
    }
  }

  private void SortDescriptions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
  {

    var oldItem = e.OldItems?.OfType<SortDescription>().FirstOrDefault();
    var newItem = e.NewItems?.OfType<SortDescription>().FirstOrDefault();

    if (_updateMode == UpdateMode.NotUpdating)
    {
      _updateMode = UpdateMode.UpdatingFromDataGrid;

      switch (e.Action)
      {
        case NotifyCollectionChangedAction.Add:
          if (newItem != null)
          {
            // Turn off SortDescriptionOption
            var option = SortDescriptionOptions.FirstOrDefault(o => o.PropertyName == newItem.Value.PropertyName);
            if (option != null)
            {
              option.IsActive = true;
              option.Ascending = newItem.Value.Direction == ListSortDirection.Ascending;
              SortDescriptionOptions.Remove(option);
              SortDescriptionOptions.Insert(0, option);
            }
          }
          break;
        case NotifyCollectionChangedAction.Remove:
          if (oldItem != null)
          {
            // Turn off GroupDescriptionOption
            var option = SortDescriptionOptions.FirstOrDefault(o => o.PropertyName == oldItem.Value.PropertyName);
            if (option != null)
            {
              option.IsActive = false;
              option.Ascending = oldItem.Value.Direction == ListSortDirection.Ascending;

            }
          }
          break;
      }

      _updateMode = UpdateMode.NotUpdating;
    }
  }

  /// <summary>
  /// Hooks into the sort/group options changes (editable list of sort/group descriptions)
  /// </summary>
  private void HookOptions()
  {
    // Hook GroupDesciptionOptions
    GroupDescriptionOptions.CollectionChanged += (s, e) => GroupDesciptionOptionsChanged();
    foreach (var item in GroupDescriptionOptions)
    {
      item.PropertyChanged += (s, e) => GroupDesciptionOptionsChanged();
    }

    // Hook SortDescriptionOptions
    SortDescriptionOptions.CollectionChanged += (s, e) => SortDesciptionOptionsChanged();
    foreach (var item in SortDescriptionOptions)
    {
      item.PropertyChanged += (s, e) => SortDesciptionOptionsChanged();
    }

    GroupDesciptionOptionsChanged();
    SortDesciptionOptionsChanged();
  }

  /// <summary>
  /// Mirror the changes from the editable group lists to the datagrid groupers
  /// </summary>
  private void GroupDesciptionOptionsChanged()
  {
    if (_updateMode == UpdateMode.NotUpdating)
    {
      _updateMode = UpdateMode.UpdatingFromGroupSortOptions;
      GroupDescriptions.Clear();
      foreach (var item in GroupDescriptionOptions)
      {
        if (item.IsActive)
        {
          GroupDescriptions.Add(item.GroupDescription);
        }
      }
      _updateMode = UpdateMode.NotUpdating;
    }
  }

  /// <summary>
  /// Mirror the changes from the editable sort lists to the datagrid sorters
  /// </summary>
  private void SortDesciptionOptionsChanged()
  {
    if (_updateMode == UpdateMode.NotUpdating)
    {
      _updateMode = UpdateMode.UpdatingFromGroupSortOptions;
      SortDescriptions.Clear();
      foreach (var item in SortDescriptionOptions)
      {
        if (item.IsActive)
        {
          SortDescriptions.Add(item.SortDescription);
        }
      }
      _updateMode = UpdateMode.NotUpdating;
    }
  }

  private void PublishSelectedEntity()
  {
    if (SelectedFiredMissile != null)
    {
      _eventAggregator.GetEvent<SetSelectedEntitiesEvent>()?.Publish(new[] { SelectedFiredMissile.MissileEntity.UID.ToString() });
    }
  }

  #endregion

}
