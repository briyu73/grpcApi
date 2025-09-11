// Classification: OFFICIAL
//
// Copyright (C) 2025 Commonwealth of Australia.
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
//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Windows.Input;
using DryIoc;
using FlewsApp.Common.SharedEdmLib.Model.Structural;
using FlewsApp.Modules.DataAccess.Contexts;
using FlewsApp.Modules.DBManager.UI.Views;
using FlewsApp.Modules.UI.ViewModels;
using Microsoft.EntityFrameworkCore;
using Prism.Services.Dialogs;
using Swordfish.NET.ViewModel;

namespace FlewsApp.Modules.DBManager.UI.ViewModels;

public class EFDataBaseViewerViewModel : FlewsDialogViewModelBase
{
  // *******************************************************************************************
  // Members
  // *******************************************************************************************

  #region Members

  private const int COLLECTION_LIMIT = 10;

	private Type? _dbContextType = null;

  private Dictionary<object, EntityWrapper?> _entityCollection = [];

  #endregion Members

  // *******************************************************************************************
  // Properties
  // *******************************************************************************************

  #region Properties

  public DatabaseDataTemplateSelector DataTemplateSelector { get; } = new DatabaseDataTemplateSelector();

  private string _statusMessage = "Ready";
	public string StatusMessage
	{
		get => _statusMessage;
		set => SetProperty(ref _statusMessage, value);
	}

	private string _databaseInfo = "No database loaded";
	public string DatabaseInfo
	{
		get => _databaseInfo;
		set => SetProperty(ref _databaseInfo, value);
	}

	private ObservableCollection<EntityTypeInfo> _entityTypes = new();
	public ObservableCollection<EntityTypeInfo> EntityTypes
	{
		get => _entityTypes;
		set => SetProperty(ref _entityTypes, value);
	}

	private ObservableCollection<object> _entities = new();
	public ObservableCollection<object> Entities
	{
		get => _entities;
		set => SetProperty(ref _entities, value);
	}

	private EntityTypeInfo? _selectedEntityType;
	public EntityTypeInfo? SelectedEntityType
	{
		get => _selectedEntityType;
		set
		{
			if (SetProperty(ref _selectedEntityType, value))
			{
				RefreshEntitiesCommand.Execute(null);
			}
		}
	}

	private int _entityCount;
	public int EntityCount
	{
		get => _entityCount;
		set => SetProperty(ref _entityCount, value);
	}

	#endregion

	// *******************************************************************************************
	// Commands
	// *******************************************************************************************

	#region Commands

	private readonly RelayCommandFactory _loadDatabaseCommand = new();
	public ICommand LoadDatabaseCommand => _loadDatabaseCommand.GetCommand
	(	
		p =>
		{
			switch (_dbContextType)
			{
				case Type dbContextType when typeof(FlewsDbContext).IsAssignableFrom(dbContextType):
					{
            // Use lazy loading to make sure all virtual properties are loaded
            var dbContextOptions = new DbContextOptionsBuilder<FlewsDbContext>().UseLazyLoadingProxies().Options;
            var flewsDbContextFactory = _container.Resolve<FlewsDbContextFactory>();

            using var flewsDbContext = flewsDbContextFactory.CreateDbContext(dbContextOptions);
						LoadDatabase(flewsDbContext);
					}
					break;
				case null:
					StatusMessage = "Error: DbContextType not provided";
					return;
				case Type dbContextType when !typeof(DbContext).IsAssignableFrom(dbContextType):
					StatusMessage = "Error: DbContextType is not a DbContext";
					return;
			}
		},
		p => true
	);

	private readonly RelayCommandFactory _refreshEntitiesCommand = new();
	public ICommand RefreshEntitiesCommand => _refreshEntitiesCommand.GetCommand
	(
		p =>
		{
      switch (_dbContextType)
			{
				case Type dbContextType when typeof(FlewsDbContext).IsAssignableFrom(dbContextType):
					{
            // Use lazy loading to make sure all virtual properties are loaded
            var dbContextOptions = new DbContextOptionsBuilder<FlewsDbContext>().UseLazyLoadingProxies().Options;
            var flewsDbContextFactory = _container.Resolve<FlewsDbContextFactory>();

            using var flewsDbContext = flewsDbContextFactory.CreateDbContext(dbContextOptions);
            RefreshEntities(flewsDbContext);
					}
					break;
				case null:
					StatusMessage = "Error: DbContextType not provided";
					return;
				case Type dbContextType when !typeof(DbContext).IsAssignableFrom(dbContextType):
					StatusMessage = "Error: DbContextType is not a DbContext";
					return;
			}
		},
		p => SelectedEntityType != null
	);

  #endregion

  // *******************************************************************************************
  // Public Methods
  // *******************************************************************************************

  #region Public Methods

  public EFDataBaseViewerViewModel(DryIoc.IContainer container) : base(container)
	{
    Title = "Database Viewer";
	}

	public override void OnDialogOpened(IDialogParameters parameters)
	{
		_dbContextType = parameters.GetValue<Type>("DbContextType");
	}

  #endregion

  // *******************************************************************************************
  // Protected Methods
  // *******************************************************************************************

  #region Protected Methods

  #endregion

  // *******************************************************************************************
  // Private Methods
  // *******************************************************************************************

  #region Private Methods

  private object? CreateEntityWrapper(object entity)
	{
		if (entity == null)
		{
			return null;
		}

    var type = entity.GetType();
    EntityWrapper entityWrapper = new EntityWrapper
    {
      TypeName = type.Name.Replace("Proxy",""),
    };

    if (_entityCollection.ContainsKey(entity))
    {
      return _entityCollection[entity];
    }
    else
    {
      _entityCollection.Add(entity, entityWrapper);
    }

		var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where(p => p.CanRead)
				.Select(p => new PropertyWrapper
				{
					Name = p.Name,
					Value = GetPropertyValue(entity, p)
				})
				.ToList();

    entityWrapper.Properties = properties;

    _entityCollection[entity] = entityWrapper;

    return entityWrapper;
	}

	private object? GetPropertyValue(object entity, PropertyInfo property, bool limitCollections = false)
	{
		try
		{
			var value = property.GetValue(entity);

      if (value == null)
      {
        return null;
      }

			// Handle collections
			if (value is System.Collections.IEnumerable enumerable && value is not string)
			{
				var items = new List<object>();
				var count = 0;
				foreach (var item in enumerable)
				{
          count++;

					if (limitCollections && count > COLLECTION_LIMIT)
					{
						break; // Limit collection display
					}

          var entityWrapper = CreateEntityWrapper(item);
					if (entityWrapper != null)
					{
						items.Add(entityWrapper);
					}
				}

				return new CollectionWrapper
				{
					Header = $"{property.Name} ({count} items)",
					Items = items
				};
			}

      // Handle complex objects (not primitives)
      var type = value.GetType();
      if (!IsPrimitiveType(type))
			{
				return CreateEntityWrapper(value);
			}

			return value;
		}
		catch (Exception)
		{
			return "[Error reading property]";
		}
	}

	private bool IsPrimitiveType(Type type)
	{
		return type.IsPrimitive ||
					 type == typeof(string) ||
					 type == typeof(DateTime) ||
					 type == typeof(decimal) ||
					 type == typeof(Guid) ||
					 type.IsEnum ||
					 (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>));
	}

	private void LoadDatabase(DbContext dbContext)
	{
		try
		{
			StatusMessage = "Loading database...";

			var entityTypes = dbContext.Model.GetEntityTypes()
					.Select(et => new EntityTypeInfo
					{
						Name = et.ClrType.Name,
						ClrType = et.ClrType,
						EntityType = et
					})
					.OrderBy(et => et.Name)
					.ToList();

			EntityTypes.Clear();
			foreach (var entityType in entityTypes)
			{
				EntityTypes.Add(entityType);
			}

			DatabaseInfo = $"Database: {dbContext.Database.GetDbConnection().Database} ({EntityTypes.Count} entity types)";
			StatusMessage = "Database loaded successfully";
		}
		catch (Exception ex)
		{
			StatusMessage = $"Error loading database: {ex.Message}";
		}
	}

	private void RefreshEntities(DbContext dbContext)
	{
		if (SelectedEntityType == null)
			return;

    // reset entity wrapper collection
    _entityCollection.Clear();

    try
    {
      StatusMessage = "Loading entities...";
      Entities.Clear();

      var dbSet = dbContext.GetType()
          .GetProperties()
          .FirstOrDefault(p => p.PropertyType.IsGenericType &&
                              p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>) &&
                              p.PropertyType.GetGenericArguments()[0] == SelectedEntityType.ClrType);

      if (dbSet != null)
      {
        var dbSetValue = dbSet.GetValue(dbContext);
        var queryable = (IQueryable<BaseEntity>)dbSetValue!;

        var entities = new List<object>();
        var count = queryable.Count();
        foreach (var entity in queryable.ToList().OrderBy(e => e.ToString()))
        {
          var entityWrapper = CreateEntityWrapper(entity);
          if (entityWrapper != null)
          {
            entities.Add(entityWrapper);
          }
        }

        foreach (var entity in entities)
        {
          Entities.Add(entity);
        }

        EntityCount = entities.Count;
        StatusMessage = $"Loaded {EntityCount} entities";
      }
    }
    catch (Exception ex)
    {
      StatusMessage = $"Error loading entities: {ex.Message}";
    }
	}

  #endregion

  // *******************************************************************************************
  // Supporting Classes
  // *******************************************************************************************

  public class EntityTypeInfo
	{
		public string Name { get; set; } = string.Empty;
		public Type ClrType { get; set; } = null!;
		public Microsoft.EntityFrameworkCore.Metadata.IEntityType EntityType { get; set; } = null!;
	}

	public class EntityWrapper
	{
		public string TypeName { get; set; } = string.Empty;
		public List<PropertyWrapper> Properties { get; set; } = [];
	}

	public class PropertyWrapper
	{
		public string Name { get; set; } = string.Empty;
		public object? Value { get; set; } = null!;
	}

	public class CollectionWrapper
	{
		public string Header { get; set; } = string.Empty;
		public List<object> Items { get; set; } = [];
	}
}
