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

using FlewsApp.Common.DataModels.UI.Models;
using FlewsApp.Modules.Config.Services;
using FlewsApp.Modules.DataLibraryEditor.Data.Settings;
using FlewsApp.Modules.DataLibraryEditor.Interface;
using FlewsApp.Modules.DataLibraryEditor.Models;
using FlewsApp.Modules.DataStore.Data;
using FlewsApp.Modules.DataStore.Data.BaseObjects;
using FlewsApp.Modules.DataStore.Interface;
using FlewsApp.Modules.DataStore.Messaging;
using FlewsApp.Modules.Shared.Messaging;
using FlewsApp.Modules.Standards;
using FlewsApp.Modules.Standards.Interface;
using FlewsApp.Modules.Standards.Models;
using FlewsApp.Modules.Standards.ViewModels;
using FlewsApp.Modules.UI;
using FlewsApp.Modules.UI.Interface;
using FlewsApp.Modules.UI.ViewModels;
using FlewseDotNetLib.Settings;
using Mad.Libraries.Core.Events;
using Mad.Libraries.Settings.Interface;
using Microsoft.Extensions.Logging;
using Prism.Commands;
using Prism.Events;
using Prism.Services.Dialogs;
using Swordfish.NET.Collections;
using Swordfish.NET.Converters;
using Swordfish.NET.General;
using Swordfish.NET.ViewModel;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Xml.Linq;

namespace FlewsApp.Modules.DataLibraryEditor.ViewModels;

// NOTE about this function.
// Some further improvements can be made to make the reference searching more efficient
// such as removing the need to search certain folders which may not have a relevant
// search string

// BM: Leaving this here as I will be using it to sort groupings in a custom manner
//public class GroupDescriptionComparer : System.Collections.IComparer
//{
//  public int Compare(object? x, object? y)
//  {
//    var xo = (GroupDescription) x!;
//    var yo = (GroupDescription) y!;

//    if (xo.Order == yo.Order)
//    {
//      return 0;
//    }

//    return (x?.Order < y?.Order ? -1 : 1);
//  }
//}

public class NamingToolViewModel : FlewsDialogViewModelBase
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

  private const int MAX_SUGGESTIONS = 20;
  private static ParallelOptions ParallelOptions => new ParallelOptions()
  {
    MaxDegreeOfParallelism = new int[] { 2, Environment.ProcessorCount - 2 }.Min()
  };

  private readonly IEventAggregator _eventAggregator;
  private readonly IDialogService _dialogService;
  private readonly IAppSettings _appSettings;
  private readonly IDataManager _dataManager;
  private readonly ISisoRef10Service _sisoRef10Service;
  private readonly ILogger _logger;
  private readonly IDataLibraryEditorController _dataLibraryEditorController;

  /// <summary>
  /// Tracks the current update mode, whether from the group/sort options, or from
  /// clicking on the DataGrid headers.
  /// </summary>
  private UpdateMode _updateMode = UpdateMode.NotUpdating;

  private Dictionary<EReferencePathType, List<string>> _locationsDictionary = new();

  private TaskFactory _taskFactory;

  private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

  private EventHandler? _globalsLoadedEventHandler;

  private readonly ConcurrentDictionary<string, ReaderWriterLockSlim> _fileLockCache;

  // Wait handle that blocks the thread until the globals have been loaded
  private ManualResetEvent _dataLibItemsInitialised = new ManualResetEvent(false);

  // Wait handle that blocks the thread until the globals have been loaded
  private ManualResetEvent _globalsLoaded = new ManualResetEvent(false);

  // Used to track platform, missile and sensor collection changes
  private TreeChangeWatcher? _globalsWatcher;

  #endregion Fields

  // *******************************************************************************************
  // Properties
  // *******************************************************************************************

  #region Properties

  public ObservableCollection<SortDescription> SortDescriptions { get; } = new();

  public ObservableCollection<GroupDescription> GroupDescriptions { get; } = new();

  public ObservableCollection<SortDescriptionOption> SortDescriptionOptions { get; } =
  new ObservableCollection<SortDescriptionOption>
  (
    new[]
    {
      new SortDescriptionOption(true, nameof(IItemNamingRecord.Name)),
      new SortDescriptionOption(false, nameof(IItemNamingRecord.DisValue)),
      new SortDescriptionOption(false, nameof(IItemNamingRecord.IsValid)),
    }
  );


  public ObservableCollection<GroupDescriptionOption> GroupDescriptionOptions { get; } =
  new ObservableCollection<GroupDescriptionOption>
  (
    new[]
    {
      new GroupDescriptionOption(true, nameof(IItemNamingRecord.RecordType)),
      new GroupDescriptionOption(false, nameof(IItemNamingRecord.IsValid)),
    }
  );

  private Predicate<object>? _filter;
  public Predicate<object>? Filter
  {
    get => _filter;
    set => SetProperty(ref _filter, value);
  }

  private bool _hasInValidNameFiltering;
  public bool HasInValidNameFiltering
  {
    get => _hasInValidNameFiltering;
    set
    {
      if (SetProperty(ref _hasInValidNameFiltering, value))
      {
        Filter = _hasInValidNameFiltering ? (p => p is not IItemNamingRecord record || !record.UsesSisoName) : null;
      }
    }
  }

  /// Selected record
  /// </summary>
  private BaseItemNamingRecord? _selectedItemNamingRecord;
  public BaseItemNamingRecord? SelectedItemNamingRecord
  {
    get => _selectedItemNamingRecord;
    set
    {
      SetProperty(ref _selectedItemNamingRecord, value);

      if (_selectedItemNamingRecord != null && !_selectedItemNamingRecord.IsLoadingReferences && !_selectedItemNamingRecord.HaveLoadedReferences)
      {
        LoadItemReferences(_selectedItemNamingRecord);
      }
    }
  }

  private bool _showReferenceDetailsColumn;
  public bool ShowReferenceDetailsColumn
  {
    get => _showReferenceDetailsColumn;
    set => SetProperty(ref _showReferenceDetailsColumn, value);
  }

  private bool _showReferencePathTypeColumn = true;
  public bool ShowReferencePathTypeColumn
  {
    get => _showReferencePathTypeColumn;
    set => SetProperty(ref _showReferencePathTypeColumn, value);
  }

  private bool _showReferenceSearchTypeColumn = true;
  public bool ShowReferenceSearchTypeColumn
  {
    get => _showReferenceSearchTypeColumn;
    set => SetProperty(ref _showReferenceSearchTypeColumn, value);
  }

  private bool _showReferencePathCollections;
  public bool ShowReferencePathCollections
  {
    get => _showReferencePathCollections;
    set => SetProperty(ref _showReferencePathCollections, value);
  }

  public IDataLibraryEditorService DataLibraryEditorService { get; }

  public IFlewsUIService FlewsUiService { get; }

  public DataLibraryEditorSettings DataLibraryEditorSettings { get; }

  private UserReferenceLocation? _selectedUserLocation;
  public UserReferenceLocation? SelectedUserLocation
  {
    get => _selectedUserLocation;
    set => SetProperty(ref _selectedUserLocation, value);
  }

  public ObservableCollection<UserReferenceLocation> UserReferenceLocations { get; } = new();

  public ConcurrentObservableCollection<IItemNamingRecord> ItemNamingRecords { get; } = new();

  public ConcurrentObservableSortedCollection<string> ReferencePathCollection { get; } = new();

  private bool _allReferencesLoaded;
  public bool AllReferencesLoaded
  {
    get => _allReferencesLoaded;
    set => SetProperty(ref _allReferencesLoaded, value);
  }

  private bool _isLoadingReferences;
  public bool IsLoadingReferences
  {
    get => _isLoadingReferences;
    set => SetProperty(ref _isLoadingReferences, value);
  }

  public ICommand CloseCommand { get; }

  #endregion

  // *******************************************************************************************
  // Commands
  // *******************************************************************************************

  #region Commands

  private readonly RelayCommandFactory _clearSelectionsCommand = new();
  public ICommand ClearSelectionsCommand => _clearSelectionsCommand.GetCommand(
    () => ItemNamingRecords.ForEach(p => p.IsSelected = false),
    () => ItemNamingRecords.Any(r => r.IsSelected));

  private readonly RelayCommandFactory _openSelectedItemCommand = new();
  public ICommand OpenSelectedItemCommand => _openSelectedItemCommand.GetCommand(
    p => _eventAggregator.GetEvent<SelectDataLibraryItemEvent>().Publish(SelectedItemNamingRecord!.DbObject),
    p => SelectedItemNamingRecord != null);

  private readonly RelayCommandFactory _loadItemReferencesCommand = new();
  public ICommand LoadItemReferencesCommand => _loadItemReferencesCommand.GetCommand(
    p => LoadItemReferences(SelectedItemNamingRecord!),
    p => SelectedItemNamingRecord != null && !IsLoadingReferences);

  private readonly RelayCommandFactory _disSisoMappingsCommand = new();
  public virtual ICommand ShowDisSisoMappingsDLECommand => _disSisoMappingsCommand.GetCommand(
    p =>
    {
      var record = (IItemNamingRecord)p!;

      Action<IDialogResult> callBack = new Action<IDialogResult>((dr) =>
      {
        if (dr.Result == ButtonResult.OK)
        {
          // extract and the set the new name/DIS ID for the platform
          var newIdentity = new IdentityModelWithReferences()
          {
            Name = dr.Parameters.GetValue<string>(DisSisoToolViewModel.DIS_SISO_TOOL_VIEW_PARAM_NAME),
            DisValue = dr.Parameters.GetValue<string>(DisSisoToolViewModel.DIS_SISO_TOOL_VIEW_PARAM_DISVALUE),
            DataLibReferences = record.DataLibReferences,
            ValidatedDisDetails = dr.Parameters.GetValue<DisDbQueryDetails>(DisSisoToolViewModel.DIS_SISO_TOOL_VIEW_PARAM_DISDBQUERYDETAILS)
          };

          var (success, numReferenceChanges, cancelRequested) = DataLibraryEditorService.PreviewChange(record.DbObject, newIdentity, true, false);
          if (cancelRequested)
          {
            return;
          }

          var txt = (success ? "Changes applied successfully." : "Failed to apply changes.");
          if (success)
          {
            if (numReferenceChanges > 0)
            {
              txt += $" Total of {numReferenceChanges} file references have been modified.";

              // update view references
              PopulateSuggestionsForItem(record);
              PopulateDataLibReferences([record], null, false, _cancellationTokenSource.Token);

              _eventAggregator.GetEvent<SelectDataLibraryItemEvent>().Publish(record.DbObject);
            }
          }
          record.IsSelected = true;
          _eventAggregator.StatusUpdate(false, 0, 0, false, txt, LogLevel.None);
        }
      });

      var parameters = new DialogParameters
      {
        { DisSisoToolViewModel.DIS_SISO_TOOL_VIEW_PARAM_DISVALUE, record.DisValue },
        { DisSisoToolViewModel.DIS_SISO_TOOL_VIEW_PARAM_KINDFILTER, record.Kind }
      };

      _dialogService.ShowDialog(nameof(Standards.Views.DisSisoToolView), record.DbObject == null ? null : parameters, callBack);
    },
    p => p is IItemNamingRecord record && record.HaveLoadedReferences
  );

  private readonly RelayCommandFactory _applySuggestedNameCommand = new();
  public ICommand ApplySuggestedNameCommand => _applySuggestedNameCommand.GetCommand(
    p =>
    {
      var record = (IItemNamingRecord)p!;

      // In this case we only start with the new name and DIS ID. The rest of the identity properties are obtained by the PreviewChange fn.
      var newIdentity = new IdentityModelWithReferences()
      {
        Name = record.SelectedSuggestion!.Description,
        DisValue = record.SelectedSuggestion!.FullDisValue,
        DataLibReferences = record!.DataLibReferences
      };

      var (success, numReferenceChanges, cancelRequested) = DataLibraryEditorService.PreviewChange(record.DbObject, newIdentity, true, false);
      if (cancelRequested)
      {
        return;
      }

      var txt = (success ? "Changes applied successfully." : "Failed to apply changes.");
      if (success)
      {
        _eventAggregator.GetEvent<SelectDataLibraryItemEvent>().Publish(null);

        if (numReferenceChanges > 0)
        {
          txt += $" Total of {numReferenceChanges} file references have been modified.";

          // update view references
          PopulateSuggestionsForItem(record);
          PopulateDataLibReferences([record], null, false, _cancellationTokenSource.Token);
        }
      }
      _eventAggregator.StatusUpdate(false, 0, 0, false, txt, LogLevel.None);
      record.IsSelected = true;
    },
    p => p is IItemNamingRecord record && record.SelectedSuggestion != null && record.HaveLoadedReferences
  );

  private readonly RelayCommandFactory _revertIdentityCommand = new();
  public ICommand RevertIdentityCommand => _revertIdentityCommand.GetCommand(
    p =>
    {
      // Already have all the info we need that needs to be applied.
      var legacyIdentity = new IdentityModelWithReferences()
      {
        Name = SelectedItemNamingRecord!.LegacyIdentity!.Name,
        DisValue = SelectedItemNamingRecord.LegacyIdentity.DisID,
        EntityType = SelectedItemNamingRecord.LegacyIdentity.EntityType,
        MilSpecSymbol = SelectedItemNamingRecord.LegacyIdentity.MilSymbol,
        DataLibReferences = SelectedItemNamingRecord!.DataLibReferences
      };

      var (success, numReferenceChanges, cancelRequested) = DataLibraryEditorService.PreviewChange(SelectedItemNamingRecord.DbObject, legacyIdentity, false, true);
      if (cancelRequested)
      {
        return;
      }

      var txt = (success ? "Changes reverted successfully." : "Failed to revert changes.");
      if (success)
      {
        _eventAggregator.GetEvent<SelectDataLibraryItemEvent>().Publish(null);

        if (numReferenceChanges > 0)
        {
          txt += $" Total of {numReferenceChanges} file references have been modified.";

          // update view references
          PopulateSuggestionsForItem(SelectedItemNamingRecord);
          PopulateDataLibReferences([SelectedItemNamingRecord], null, false, _cancellationTokenSource.Token);
        }
      }
      _eventAggregator.StatusUpdate(false, 0, 0, false, txt, LogLevel.None);
      SelectedItemNamingRecord.IsSelected = true;
    },
    p => SelectedItemNamingRecord?.LegacyIdentity != null
  );

  private readonly RelayCommandFactory _applyAllSelectedItemsCommand = new();
  public ICommand ApplyAllSelectedItemsCommand => _applyAllSelectedItemsCommand.GetCommand(
    p =>
    {
      bool doDataLibraryRefresh = false;
      int totalNumRefChanges = 0;
      bool haveErrors = false;
      var cancellationToken = _cancellationTokenSource.Token;

      foreach (var record in ItemNamingRecords.Where(r => r.IsSelected))
      {
        var newIdentity = new IdentityModelWithReferences()
        {
          Name = record.SelectedSuggestion!.Description,
          DisValue = record.SelectedSuggestion!.FullDisValue,
          DataLibReferences = record.DataLibReferences
        };

        var result = DataLibraryEditorService.PreviewChange(record.DbObject, newIdentity, true, false);
        if (result.cancelRequested)
        {
          break;
        }

        if (result.success)
        {
          doDataLibraryRefresh |= result.success;
          if (result.numReferenceChanges > 0)
          {
            totalNumRefChanges += result.numReferenceChanges;

            // update view references
            PopulateSuggestionsForItem(record);
            if (record == SelectedItemNamingRecord)
            {
              PopulateDataLibReferences([record], null, false, cancellationToken);
            }
          }
        }
        else
        {
          haveErrors = true;
        }
      }

      if (totalNumRefChanges > 0)
      {
        if (doDataLibraryRefresh)
        {
          _eventAggregator.GetEvent<SelectDataLibraryItemEvent>().Publish(null);
        }
        var txt = $"Changes applied {(haveErrors ? "with errors" : "successfully")}.";
        txt += $" Total of {totalNumRefChanges} file references have been modified.";
        _eventAggregator.StatusUpdate(false, 0, 0, false, txt, LogLevel.None);
      }
    },
    p => ItemNamingRecords.Any(p => p.IsSelected && !p.IsLoadingReferences) && !IsLoadingReferences
  );

  private readonly RelayCommandFactory _revertAllSelectedItemsCommand = new();
  public ICommand RevertAllSelectedItemsCommand => _revertAllSelectedItemsCommand.GetCommand(
    p =>
    {
      bool doDataLibraryRefresh = false;
      int totalNumRefChanges = 0;
      bool haveErrors = false;
      var cancellationToken = _cancellationTokenSource.Token;

      foreach (var record in ItemNamingRecords.Where(r => r.IsSelected))
      {
        var legacyIdentity = new IdentityModelWithReferences()
        {
          Name = record.LegacyIdentity!.Name,
          DisValue = record.LegacyIdentity.DisID,
          EntityType = record.LegacyIdentity.EntityType,
          MilSpecSymbol = record.LegacyIdentity!.MilSymbol,
          DataLibReferences = record!.DataLibReferences
        };

        var result = DataLibraryEditorService.PreviewChange(record.DbObject, legacyIdentity, false, true);
        if (result.cancelRequested)
        {
          break;
        }

        if (result.success)
        {
          doDataLibraryRefresh |= result.success;
          if (result.numReferenceChanges > 0)
          {
            totalNumRefChanges += result.numReferenceChanges;

            // update suggestions
            PopulateSuggestionsForItem(record);
            if (record == SelectedItemNamingRecord)
            {
              PopulateDataLibReferences([record], null, false, cancellationToken);
            }
          }
        }
        else
        {
          haveErrors = true;
        }
      }

      if (totalNumRefChanges > 0)
      {
        if (doDataLibraryRefresh)
        {
          _eventAggregator.GetEvent<SelectDataLibraryItemEvent>().Publish(null);
        }
        var txt = $"Changes reverted {(haveErrors ? "with errors" : "successfully")}.";
        txt += $" Total of {totalNumRefChanges} file references have been modified.";
        _eventAggregator.StatusUpdate(false, 0, 0, false, txt, LogLevel.None);
      }
    },
    p => ItemNamingRecords.Any(p => p.IsSelected && !p.IsLoadingReferences) && !IsLoadingReferences
  );

  private readonly RelayCommandFactory _addUserLocationCommand = new();
  public ICommand AddUserLocationCommand => _addUserLocationCommand.GetCommand(
    () =>
    {
      var newUserLocation = new UserReferenceLocation();
      UserReferenceLocations.Add(newUserLocation);
      SelectedUserLocation = newUserLocation;
    },
    () => true
  );

  private readonly RelayCommandFactory _removeUserLocationCommand = new();
  public ICommand RemoveUserLocationCommand => _removeUserLocationCommand.GetCommand(
    () =>
    {
      UserReferenceLocations.Remove(SelectedUserLocation!);
      SelectedUserLocation = null;
    },
    () => SelectedUserLocation != null
  );

  private readonly RelayCommandFactory _setUserLocationCommand = new();
  public ICommand SetUserLocationCommand => _setUserLocationCommand.GetCommand(
    p =>
    {
      var result = FlewsUiService.OpenFolderDialog(FlewsSettings.Instance.FlewsScenarioData, out string? selectedFolder, "Select folder..");
      if (result == true && selectedFolder != null)
      {
        var dir = new DirectoryInfo(selectedFolder);
        if (dir.Exists)
        {
          var userLocation = (UserReferenceLocation)p!;
          userLocation.Location = selectedFolder;
        }
        else
        {
          FlewsUiService.ShowError("Folder selected for export is not valid!");
        }
      }
    },
    p => p != null
  );

  private readonly RelayCommandFactory _loadAllReferencesCommand = new();
  public ICommand LoadAllReferencesCommand => _loadAllReferencesCommand.GetCommand(
    p =>
    {
      if (!IsLoadingReferences)
      {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _cancellationTokenSource.Token;
        LoadAllReferences(cancellationToken);
      }
      else
      {
        var question = "Are you sure you want to cancel?";
        if (FlewsUiService.ShowQuestion(question, DialogButtons.YesNo, $"Cancel Loading All References?") == ButtonResult.Yes)
        {
          _eventAggregator.StatusUpdate(true, -1, 0, true, "Cancelling..", LogLevel.Information);
          _cancellationTokenSource.Cancel();
        }
      }
    }
  );

  #endregion

  // *******************************************************************************************
  // Public Methods
  // *******************************************************************************************

  #region Public Methods

  public NamingToolViewModel(DryIoc.IContainer container, IEventAggregator eventAggregator, IDialogService dialogService,
    IDataLibraryEditorService dataLibraryEditorService, IFlewsUIService flewsUIService,
    ISettingsProvider settingsProvider, IAppSettings appSettings, IDataManager dataManager,
    ISisoRef10Service sisoRef10Service, ILogger logger, IDataLibraryEditorController dataLibraryEditorController)
    : base(container)
  {
    _eventAggregator = eventAggregator;
    _dialogService = dialogService;
    DataLibraryEditorService = dataLibraryEditorService;
    FlewsUiService = flewsUIService;
    DataLibraryEditorSettings = settingsProvider.GetSettings<DataLibraryEditorSettings>();
    _appSettings = appSettings;
    _dataManager = dataManager;
    _sisoRef10Service = sisoRef10Service;
    _logger = logger;
    _fileLockCache = DataLibraryEditorService.FileLockCache;
    _dataLibraryEditorController = dataLibraryEditorController;

    // initialise user locations from the settings
    foreach (var location in DataLibraryEditorSettings.NamingToolOptions.UserReferenceLocations)
    {
      WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>
        .AddHandler(location, nameof(INotifyPropertyChanged.PropertyChanged), UserReferenceLocation_PropertyChanged);
      UserReferenceLocations.Add(location);
    }
    UserReferenceLocations.CollectionChanged += UserReferenceLocations_CollectionChanged;

    // Hook into the sort/group descriptions to handle when they change
    HookDescriptions();

    // Hook into the sort/group options (editable list of descriptions)
    HookOptions();

    // SortDescriptionOptions.First().Descending = true;

    var scheduler = new LimitedConcurrencyLevelTaskScheduler(Environment.ProcessorCount - 2);
    _taskFactory = new TaskFactory(scheduler);

    // Initialise all search locations
    InitialiseSearchLocations();

    if (_dataManager.Globals != null && _dataManager.Globals.IsLoaded)
    {
      SetGlobalData();
    }
    else
    {
      AddGlobalsLoadedEventHandler();
    }

    // The following event gets triggered when the data sources changes.
    _eventAggregator.GetEvent<GlobalsChangedEvent>().Subscribe(OnGlobalsChangedEventWrapper);

    DataLibraryEditorSettings.NamingToolOptions.PropertyChanged += DataLibraryEditorSettings_PropertyChanged;

    CloseCommand = new DelegateCommand(() => CloseDialog(ButtonResult.Cancel));
  }

  #endregion

  // *******************************************************************************************
  // Protected Methods
  // *******************************************************************************************

  #region Protected Methods

  protected override void OnDetachedFromView()
  {
    _cancellationTokenSource.Cancel();

    base.OnDetachedFromView();
  }

  #endregion

  // *******************************************************************************************
  // Private Parts
  // *******************************************************************************************

  #region Privates

  private string GetDisplayLocation(string location) => DataLibraryEditorSettings.NamingToolOptions.TrimUserSearchLocations ? HelperFns.TrimProjectHomePath(location) : location;

  private void InitialiseSearchLocations()
  {
    // initilise static locations
    _locationsDictionary[EReferencePathType.XML_DataLibrary] = [
      Path.Combine(_appSettings.FlewsSettings.FlewsDesign, "DataLibrary"),
        Path.Combine(_appSettings.FlewsSettings.ProjectHome, "TestOverrideData", "DataLibrary"),
        Path.Combine(_appSettings.FlewsSettings.ProjectHome, "TestData", "Design", "DataLibrary")
    ];

    _locationsDictionary[EReferencePathType.XML_Scenarios] = [
      Path.Combine(_appSettings.FlewsSettings.FlewsDesign, "Scenarios"),
        Path.Combine(_appSettings.FlewsSettings.ProjectHome, "TestOverrideData", "Scenarios"),
        Path.Combine(_appSettings.FlewsSettings.ProjectHome, "TestData", "Design", "Scenarios")
    ];

    _locationsDictionary[EReferencePathType.FLEWS_DefaultDataObjects] = [
      Path.Combine(_appSettings.FlewsSettings.ProjectData, Config.HelperFns.DEFAULT_DATASTORE_OBJECTS),
        Path.Combine(_appSettings.FlewsSettings.ProjectHome, "TestOverrideData", Config.HelperFns.DEFAULT_DATASTORE_OBJECTS),
        Path.Combine(_appSettings.FlewsSettings.ProjectData, "DataStoreDefaultObjectsPhoebe"),
        Path.Combine(_appSettings.FlewsSettings.ProjectHome, "TestData", "DataStore")
    ];

    _locationsDictionary[EReferencePathType.FLEWZ_Scenarios] = [
      Path.Combine(_appSettings.FlewsSettings.ProjectHome, "UserData", "Scenarios"),
        Path.Combine(_appSettings.FlewsSettings.ProjectHome, "TestData", "Scenarios")
    ];

    // initialise user locations
    foreach (var userRefLocation in DataLibraryEditorSettings.NamingToolOptions.UserReferenceLocations)
    {
      if (Path.Exists(userRefLocation.Location))
      {
        _locationsDictionary[userRefLocation.ReferencePathType].Add(userRefLocation.Location);
      }
    }

    // set the reference path collection
    ReferencePathCollection.Clear();
    foreach (var refPathType in Enum.GetValues<EReferencePathType>())
    {
      foreach (var location in _locationsDictionary[refPathType])
      {
        var txt = $"{EnumToDescriptionConverter.GetString(refPathType)}: {GetDisplayLocation(location)}";
        if (!Path.Exists(location))
        {
          txt += " (Invalid Path! Please fix or remove.)";
        }
        ReferencePathCollection.Add(txt);
      }
    }
  }

  // This is called after the globals has finished loading all the items
  private void InitialiseDataLibItems()
  {
    _globalsLoaded.WaitOne();

    if (_globalsWatcher != null)
    {
      _globalsWatcher.CollectionChanged -= GlobalsWatcher_CollectionChanged;
    }

    _cancellationTokenSource.Cancel();
    _cancellationTokenSource = new CancellationTokenSource();

    // always start with a clean slate at initialisation
    if (ItemNamingRecords.Any())
    {
      ItemNamingRecords.Clear();
    }

    if (_dataManager.Globals!.Platforms.Any() || _dataManager.Globals!.Weapons.Missiles.Any() || _dataManager.Globals.Systems.AllSystems.Any())
    {
      // get all platforms, missiles and sensors as stored objects
      ItemNamingRecords.AddRange(_dataManager.Globals!.Platforms.Select(s => new PlatformItemNamingRecord(s)));
      ItemNamingRecords.AddRange(_dataManager.Globals.Weapons.Missiles.Select(s => new MissileItemNamingRecord(s)));
      ItemNamingRecords.AddRange(_dataManager.Globals.Systems.AllSystems.Select(s => new SensorItemNamingRecord(s)));

      // BM &&& Leave this - USE FOR TESTING ONLY
      //ItemNamingRecords.AddRange(_dataManager.Globals!.Weapons.Missiles.Where(p => p.Name.Contains("AA-12-Adder")).Select(s => new MissileItemNamingRecord(s)));
      //ItemNamingRecords.AddRange(_dataManager.Globals!.Platforms.Where(p => p.Name.Contains("Poseidon")).Select(s => new PlatformItemNamingRecord(s)));
      //ItemNamingRecords.AddRange(_dataManager.Globals!.Platforms.Where(p => p.Name.Contains("TestAEA")).Select(s => new PlatformItemNamingRecord(s)));
      //ItemNamingRecords.AddRange(_dataManager.Globals!.Systems.AllSystems.Where(p => p.LegacyFileId.Contains("Demo_IADS_jammer")).Select(s => new SensorItemNamingRecord(s)));

      if (ItemNamingRecords.Any())
      {
        var cancellationToken = _cancellationTokenSource.Token;
        // populate the best match lists on background threads
        // NOTE: The parallel commands needs to running within another task so that it doesn't block the UI
        Task.Run(() =>
        {
          Parallel.ForEach(ItemNamingRecords, ParallelOptions, r =>
          {
            SetNameSuggestionsForItem(r, cancellationToken);
          });

          // This helps the button refreshing properly. Withot it the Preview/Apply button will look like they are
          // unavailable until the user focuses on the view with a mouse button press
          RelayCommandManager.InvalidateRequerySuggested();
        });

        if (!DataLibraryEditorSettings.NamingToolOptions.LazyLoadReferences)
        {
          LoadAllReferences(cancellationToken);
        }
      }
    }

    _globalsWatcher = new TreeChangeWatcher(_dataManager.Globals);
    _globalsWatcher.CollectionChanged += GlobalsWatcher_CollectionChanged;

    _dataLibItemsInitialised.Set();
  }

  private void LoadAllReferences(CancellationToken cancellationToken)
  {
    if (ItemNamingRecords.Count >= 25)
    {
      var question = "Warning: This operation will take a long time. Are you sure you want to continue?\n(This can be cancelled at anytime)";
      if (FlewsUiService.ShowQuestion(question, DialogButtons.YesNo, $"Load All References?") == ButtonResult.No)
      {
        return;
      }
    }

    Task.Run(() =>
    {
      AllReferencesLoaded = false;
      IsLoadingReferences = true;
      _eventAggregator.StatusUpdate(true, -1, 0, true, "Collecting file references..", LogLevel.Information);
      PopulateDataLibReferences(ItemNamingRecords, ReferencePathCollection, true, cancellationToken);
      IsLoadingReferences = false;
      if (!cancellationToken.IsCancellationRequested)
      {
        AllReferencesLoaded = true;
      }
      _eventAggregator.StatusUpdate(false, 0, 0, false, "Ready", LogLevel.None);
      RelayCommandManager.InvalidateRequerySuggested();
    });
  }

  private void GlobalsWatcher_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
  {
    // Do not make changes while we are initialising the data library items
    _dataLibItemsInitialised.WaitOne();

    if (e.NewItems != null)
    {
      foreach (var item in e.NewItems.OfType<StoredObject>())
      {
        IItemNamingRecord? record = item switch
        {
          PlatformDef platformDef => new PlatformItemNamingRecord(platformDef),
          MissileDetails missileDetails => new MissileItemNamingRecord(missileDetails),
          ISensorSystem sensorSystem => new SensorItemNamingRecord(sensorSystem),
          _ => null,
        };

        if (record != null)
        {
          ItemNamingRecords.Add(record);
          PopulateSuggestionsForItem(record);
        }
      }
    }
    if (e.OldItems != null)
    {
      foreach (var item in e.OldItems.OfType<StoredObject>())
      {
        var record = ItemNamingRecords.FirstOrDefault(r => r.DbObject == item);
        if (record != null)
        {
          ItemNamingRecords.Remove(record);
        }
      }
    }
  }

  private void PopulateSuggestionsForItem(IItemNamingRecord record)
  {
    _taskFactory.StartNew(() => SetNameSuggestionsForItem(record, _cancellationTokenSource.Token));
  }

  private void OnGlobalsChangedEventWrapper() => SetGlobalData();

  private void SetGlobalData()
  {
    _globalsLoaded.Set();

    try
    {
      RemoveGlobalsLoadedEventHandler();

      InitialiseDataLibItems();
    }
    finally
    {
      AddGlobalsLoadedEventHandler();
    }
  }

  private void DataManager_GlobalsLoaded(object? sender, EventArgs e)
  {
    // Only do this when the globals are already loaded as this fn also gets triggered on the
    // GlobalsChangedEvent event which gets sent before the Globals has finished loading in the
    // background
    if (_dataManager.Globals?.IsLoaded ?? false)
    {
      _logger.LogDebug("DataLibrary received DataStore Globals update event.");

      SetGlobalData();
    }
  }

  private void AddGlobalsLoadedEventHandler()
  {
    if (_globalsLoadedEventHandler == null)
    {
      _globalsLoadedEventHandler = new EventHandler(DataManager_GlobalsLoaded);
      _dataManager.GlobalsLoaded += _globalsLoadedEventHandler;
    }
  }

  private void RemoveGlobalsLoadedEventHandler()
  {
    if (_globalsLoadedEventHandler != null)
    {
      _dataManager.GlobalsLoaded -= _globalsLoadedEventHandler;
      _globalsLoadedEventHandler = null;
    }
  }

  private void PopulateDataLibReferences(ICollection<IItemNamingRecord> itemNamingRecords, ConcurrentObservableCollection<string>? pathCollection, bool doWait, CancellationToken cancellationToken)
  {
    if (itemNamingRecords.Count == 0)
    {
      return;
    }

    itemNamingRecords.ForEach(r => r.StartReferenceLoading());
    pathCollection?.Clear();

    if (doWait)
    {
      List<Task> tasks = [];
      tasks.AddRange(Enum.GetValues<EReferencePathType>().SelectMany(type => PopulateDataLibReferencesByType(itemNamingRecords, type, pathCollection, cancellationToken)));
      Task.WaitAll([.. tasks]);
      if (!cancellationToken.IsCancellationRequested)
      {
        itemNamingRecords.ForEach(r => r.StopReferenceLoading(cancellationToken));
      }
    }
    else
    {
      List<Task> tasks = [];
      tasks.AddRange(Enum.GetValues<EReferencePathType>().SelectMany(type => PopulateDataLibReferencesByType(itemNamingRecords, type, pathCollection, cancellationToken)));
      Task.WhenAll(tasks).ContinueWith(_ =>
      {
        if (!cancellationToken.IsCancellationRequested)
        {
          itemNamingRecords.ForEach(r => r.StopReferenceLoading(cancellationToken));
        }
      });
    }
  }

  private IEnumerable<Task> PopulateDataLibReferencesByType(ICollection<IItemNamingRecord> itemNamingRecords, EReferencePathType pathType,
          ConcurrentObservableCollection<string>? pathCollection, CancellationToken cancellationToken)
  {
    List<Task> tasks = [];

    switch (pathType)
    {
      case EReferencePathType.XML_DataLibrary:
      case EReferencePathType.XML_Scenarios:
        {
          foreach (var location in _locationsDictionary[pathType])
          {
            if (Directory.Exists(location))
            {
              pathCollection?.Add($"{EnumToDescriptionConverter.GetString(pathType)}: {GetDisplayLocation(location)}");
              tasks.Add(_taskFactory.StartNew(() => PopulateXmlReferences([location], itemNamingRecords, pathType, cancellationToken)));
            }
            else
            {
              _logger.LogError("Reference path '{location}' does not exist!", location);
            }
          }
          break;
        }
      case EReferencePathType.FLEWS_DefaultDataObjects:
        {
          foreach (var location in _locationsDictionary[pathType])
          {
            if (Directory.Exists(location))
            {
              pathCollection?.Add($"{EnumToDescriptionConverter.GetString(pathType)}: {GetDisplayLocation(location)}");
              tasks.Add(_taskFactory.StartNew(() => PopulateFlewsDefaultDataObjectReferences(location, itemNamingRecords, cancellationToken)));
            }
            else
            {
              _logger.LogError("Reference path '{location}' does not exist!", location);
            }
          }
          break;
        }
      case EReferencePathType.FLEWZ_Scenarios:
        {
          foreach (var location in _locationsDictionary[pathType])
          {
            if (Directory.Exists(location))
            {
              pathCollection?.Add($"{EnumToDescriptionConverter.GetString(pathType)}: {GetDisplayLocation(location)}");
              tasks.Add(_taskFactory.StartNew(() => PopulateFlewzScenarioReferences(location, itemNamingRecords, cancellationToken)));
            }
            else
            {
              _logger.LogError("Reference path '{location}' does not exist!", location);
            }
          }
          break;
        }
    }

    return tasks;
  }

  // Does not search for name field
  private void PopulateXmlReferences(IEnumerable<string> locations, ICollection<IItemNamingRecord> records, EReferencePathType pathType, CancellationToken cancellationToken)
  {
    // NOTE: Legacy XML-based Systems do not have any search fields, so can ignore

    IEnumerable<EReferenceSearchType> refSearchTypes = pathType == EReferencePathType.XML_DataLibrary
                          ? ([EReferenceSearchType.DIS_ID, EReferenceSearchType.MilSpecSymbol, EReferenceSearchType.EntityType])
                          : ([EReferenceSearchType.DIS_ID]);

    var allfiles = GetDataFileNames(locations);
    foreach (var filename in allfiles)
    {
      if (!_fileLockCache.TryGetValue(filename, out ReaderWriterLockSlim? fileLock))
      {
        fileLock = new ReaderWriterLockSlim();
        _fileLockCache[filename] = fileLock;
      }

      XDocument? doc = null;
      try
      {
        fileLock.EnterReadLock();
        doc = DataLibraryEditorService.LoadXDoc(filename);
      }
      finally
      {
        fileLock.ExitReadLock();
      }

      if (doc != null)
      {
        AddAllReferences(records, pathType, refSearchTypes, filename, doc!.Root, cancellationToken);
      }
    }
  }

  // Does not search for name field
  private void PopulateFlewsDefaultDataObjectReferences(string dataPath, ICollection<IItemNamingRecord> records, CancellationToken cancellationToken)
  {
    // NOTE that name search type is always processed and should not be added in this list
    IEnumerable<EReferenceSearchType> refSearchTypes = [EReferenceSearchType.DIS_ID, EReferenceSearchType.MilSpecSymbol, EReferenceSearchType.EntityType];

    AddFlewsDefaultDataObjectFileReferences(records, Path.Combine(dataPath, "Platforms"), refSearchTypes, cancellationToken);
    AddFlewsDefaultDataObjectFileReferences(records, Path.Combine(dataPath, "Weapons", "Missiles"), refSearchTypes, cancellationToken);

    // Systems do not have EntityType
    refSearchTypes = [EReferenceSearchType.DIS_ID, EReferenceSearchType.MilSpecSymbol];
    AddFlewsDefaultDataObjectFileReferences(records, Path.Combine(dataPath, "Systems"), refSearchTypes, cancellationToken);
  }

  private void AddFlewsDefaultDataObjectFileReferences(ICollection<IItemNamingRecord> records, string path, IEnumerable<EReferenceSearchType> refSearchTypes, CancellationToken cancellationToken)
  {
    if (!Directory.Exists(path))
    {
      // Note that this is not an error as these directories do not have to exist
      //_logger.LogInformation("Directory for default data objects doesn't exist: '{path}'", path);
      return;
    }

    var directoryFiles = Directory.EnumerateFiles(path, $"*{DesignPathProvider.FLEWS_FILE_EXT}", SearchOption.AllDirectories);
    foreach (var filename in directoryFiles)
    {
      if (!_fileLockCache.TryGetValue(filename, out ReaderWriterLockSlim? fileLock))
      {
        fileLock = new ReaderWriterLockSlim();
        _fileLockCache[filename] = fileLock;
      }

      XDocument? doc = null;
      fileLock.EnterReadLock();
      try
      {
        using FileStream fileStream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
        doc = XDocument.Load(filename);
      }
      finally
      {
        fileLock.ExitReadLock();
      }

      if (doc != null)
      {
        AddAllReferences(records, EReferencePathType.FLEWS_DefaultDataObjects, refSearchTypes, filename, doc.Root, cancellationToken);
      }
    }
  }

  private void PopulateFlewzScenarioReferences(string dataPath, ICollection<IItemNamingRecord> records, CancellationToken cancellationToken)
  {
    // get all default data store object files  
    var scenarioFiles = Directory.EnumerateFiles(dataPath, $"*{DesignPathProvider.FLEWS_COMPRESSED_FILE_EXT}", SearchOption.AllDirectories);

    IEnumerable<EReferenceSearchType> refSearchTypes = [EReferenceSearchType.DIS_ID];

    foreach (var filename in scenarioFiles)
    {
      if (!_fileLockCache.TryGetValue(filename, out ReaderWriterLockSlim? fileLock))
      {
        fileLock = new ReaderWriterLockSlim();
        _fileLockCache[filename] = fileLock;
      }

      XDocument? doc = null;
      fileLock.EnterReadLock();
      try
      {
        using FileStream compressedFileStream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var decompressor = new BrotliStream(compressedFileStream, CompressionMode.Decompress);
        doc = XDocument.Load(decompressor);
      }
      finally
      {
        fileLock.ExitReadLock();
      }

      if (doc != null)
      {
        AddAllReferences(records, EReferencePathType.FLEWZ_Scenarios, refSearchTypes, filename, doc.Root, cancellationToken);
      }
    }
  }

  private void AddAllReferences(ICollection<IItemNamingRecord> records, EReferencePathType pathType, IEnumerable<EReferenceSearchType> refSearchTypes,
    string file, XElement? rootNode, CancellationToken cancellationToken)
  {
    if (rootNode != null)
    {
      foreach (var record in records)
      {
        // only add the rest of the search types if we have found matches for the name
        // IMPORTANT: This has to assume that getting a name match means we are updating a file describing that one item.
        // This is a big assumption! We may need to add more conditional checking here prior to updating the rest of the search type fields
        // but for now at least it reduces the risk of making reference change errors
        if (AddReferencesForSearchType(pathType, file, rootNode, record, EReferenceSearchType.Name, cancellationToken))
        {
          foreach (var refSearchType in refSearchTypes)
          {
            AddReferencesForSearchType(pathType, file, rootNode, record, refSearchType, cancellationToken);
          }
        }
      }
    }
    else
    {
      _logger.LogError("Unable to load file '{file}' for referencing. Root node not found!", file);
    }
  }

  private bool AddReferencesForSearchType(EReferencePathType pathType, string file, XElement rootNode, IItemNamingRecord record,
    EReferenceSearchType refSearchType, CancellationToken cancellationToken)
  {
    if (cancellationToken.IsCancellationRequested)
    {
      record.StopReferenceLoading(cancellationToken);
      return false;
    }

    // only do name searches for scenario folders
    if ((pathType == EReferencePathType.XML_Scenarios || pathType == EReferencePathType.FLEWZ_Scenarios) &&
        (refSearchType != EReferenceSearchType.Name))
    {
      return false;
    }

    var searchStr = record.SearchStrForType(refSearchType);
    if (string.IsNullOrEmpty(searchStr))
    {
      return false;
    }

    var refNodes = rootNode.Descendants().Where(el => el.Attributes().Any(a => a.Value.Equals(searchStr, StringComparison.Ordinal)));
    foreach (var node in refNodes)
    {
      // systems can have references such as '<system_name>.xml' within files - we can assume this is only for name
      var attributes = (refSearchType == EReferenceSearchType.Name && record.RecordType == IItemNamingRecord.EDisRecordType.System) ?
        node.Attributes().Where(a => a.Value.Equals(searchStr, StringComparison.Ordinal) || a.Value.Equals($"{searchStr}.xml", StringComparison.Ordinal)) :
        node.Attributes().Where(a => a.Value.Equals(searchStr, StringComparison.Ordinal));
      foreach (var attribute in attributes)
      {
        record.DataLibReferences.Add(new DataLibReferenceModel(pathType, refSearchType, searchStr, file, HelperFns.TrimNodeDetails(node)));
      }
    }

    if (refNodes.Any())
    {
      return true;
    }
    else if (refSearchType == EReferenceSearchType.Name)
    {
      // look for matching file names
      if (Path.GetFileNameWithoutExtension(file) == searchStr)
      {
        record.DataLibReferences.Add(new DataLibReferenceModel(pathType, EReferenceSearchType.Path, searchStr, file, file));
      }

      // Lookk for matching foler names
      // There could be cases where the name of the platform for example, doesn't match the name of the directory it lives in
      var directories = Path.GetRelativePath(_appSettings.FlewsSettings.ProjectHome, Path.GetDirectoryName(file) ?? file).Split(Path.DirectorySeparatorChar);
      if (directories.Any(d => d == searchStr))
      {
        var path = Path.GetDirectoryName(file);
        if (path != null && !record.DataLibReferences.Any(r => r.ReferenceSearchType == EReferenceSearchType.Path && r.Details == path))
        {
          record.DataLibReferences.Add(new DataLibReferenceModel(pathType, EReferenceSearchType.Path, searchStr, file, path));
        }
      }
    }

    return false;
  }

  /// <summary>
  /// Recursively searches the folders listed in the DataLocations list for
  /// .xml and .fbr (frisbi run config) files.
  /// </summary>
  /// <returns>The list of all relevant file names</returns>
  private IEnumerable<string> GetDataFileNames(IEnumerable<string> locations)
  {
    var ret = new List<string>();

    var excludedFileList = new List<string>() { "AgentDump.xml", "Frisbi-App-Settings.xml" };

    ret.AddRange(locations
      .Where(d => !string.IsNullOrWhiteSpace(d))
      .SelectMany(l => Directory.EnumerateFiles(l, "*.xml", SearchOption.AllDirectories))
      .Where(file => !excludedFileList.Contains(Path.GetFileName(file))));
    //exclude any agentdump file it is a generated file

    ret.AddRange(locations
      .Where(d => !string.IsNullOrWhiteSpace(d))
      .SelectMany(l => Directory.EnumerateFiles(l, "*.fbr", SearchOption.AllDirectories)));

    return ret;
  }

  private void SetNameSuggestionsForItem(IItemNamingRecord record, CancellationToken cancellationToken)
  {
    if (cancellationToken.IsCancellationRequested)
    {
      return;
    }

    record.HaveLoadedSuggestions = false;
    record.SortedBestMatchList = _sisoRef10Service.GetSortedBestMatchList(record.Kind, record.Name, out double score).Take(MAX_SUGGESTIONS).ToList();
    record.SelectedSuggestion = record.SortedBestMatchList.FirstOrDefault();
    record.PerfectMatchSuggestion = float.Equals(score, 1.0);
    record.HaveLoadedSuggestions = true;

    // sets the Siso validation flag automatically conditional on wether we have a perfect match or not
    record.CheckSisoValidation();
  }

  private void UserReferenceLocations_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
  {
    switch (e.Action)
    {
      case NotifyCollectionChangedAction.Add:
        {
          if (e.NewItems != null)
          {
            foreach (var reference in e.NewItems.OfType<UserReferenceLocation>())
            {
              WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>
                .AddHandler(reference, nameof(INotifyPropertyChanged.PropertyChanged), UserReferenceLocation_PropertyChanged);
              DataLibraryEditorSettings.NamingToolOptions.UserReferenceLocations.Add(reference);
            }
          }
        }
        break;
      case NotifyCollectionChangedAction.Remove:
        {
          if (e.OldItems != null)
          {
            foreach (var reference in e.OldItems.OfType<UserReferenceLocation>())
            {
              WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>
                .RemoveHandler(reference, nameof(INotifyPropertyChanged.PropertyChanged), UserReferenceLocation_PropertyChanged);
              DataLibraryEditorSettings.NamingToolOptions.UserReferenceLocations.Remove(reference);
            }
          }
          break;
        }
    }

    DataLibraryEditorSettings.Save();
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
          //item.GroupDescription.SortDescriptions.First().Direction = ListSortDirection.Descending;
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

  private void UserReferenceLocation_PropertyChanged(object? sender, PropertyChangedEventArgs e)
  {
    DataLibraryEditorSettings.Save();
  }

  private void DataLibraryEditorSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
  {
    switch (e.PropertyName)
    {
      case nameof(DataLibraryEditorSettings.NamingToolOptions.TrimUserSearchLocations):
        InitialiseSearchLocations();
        break;
      case nameof(DataLibraryEditorSettings.NamingToolOptions.LazyLoadReferences):
        if (DataLibraryEditorSettings.NamingToolOptions.LazyLoadReferences == false)
        {
          var question = "Would you like to load all references now?";
          if (FlewsUiService.ShowQuestion(question, DialogButtons.YesNo, $"Load All References?") == ButtonResult.Yes)
          {
            LoadAllReferences(_cancellationTokenSource.Token);
          }
        }
        else if (IsLoadingReferences)
        {
          var question = "Would you like to cancel loading all references?";
          if (FlewsUiService.ShowQuestion(question, DialogButtons.YesNo, $"Cancel Loading references?") == ButtonResult.Yes)
          {
            _cancellationTokenSource.Cancel();
          }
        }
        break;
    }
  }

  private void LoadItemReferences(IItemNamingRecord record)
  {
    _cancellationTokenSource.Cancel();
    _cancellationTokenSource = new CancellationTokenSource();

    Task.Run(() =>
    {
      PopulateDataLibReferences([record], null, false, _cancellationTokenSource.Token);

      // This helps the button refreshing properly. Withot it the Preview/Apply button will look like they are
      // unavailable until the user focuses on the view with a mouse button press
      RelayCommandManager.InvalidateRequerySuggested();
    });
  }

  private void CloseDialog(ButtonResult result)
  {
    RaiseRequestClose(new DialogResult(result));
  }

  #endregion
}
