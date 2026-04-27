using BAM.Libraries.SharedEDMLib.Extensions;
using BAM.Libraries.SharedEDMLib.Model.Structural;
using BAM.Libraries.WpfLib;
using BAM.Libraries.WpfLib.Commands;
using BAM.Modules.DataAccess.Models.Contexts;
using BAM.Modules.DataAccess.UI.Views;
using BAM.Modules.DataSource.Interface;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Input;

namespace BAM.Modules.DataAccess.UI.ViewModels;

public class DatabaseViewerViewModel : ViewModelBase, IDialogAware
{
	// *******************************************************************************************
	// Fields
	// *******************************************************************************************

	#region Fields

	private const int COLLECTION_LIMIT = 10;

	private readonly DryIoc.IContainer _container;

	private Type? _dbContextType = null;

	private Dictionary<object, EntityWrapper?> _entityCollection = [];

	#endregion Fields

	// *******************************************************************************************
	// Properties
	// *******************************************************************************************

	#region Properties

	public string Title => "Database Viewer";

	public DialogCloseListener RequestClose { get; } = new DialogCloseListener();

	public DatabaseDataTemplateSelector DataTemplateSelector { get; }

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
				case Type dbContextType when typeof(FinanceDbContext).IsAssignableFrom(dbContextType):
					{
						// Use lazy loading to make sure all virtual properties are loaded
						var dbContextOptions = new DbContextOptionsBuilder<FinanceDbContext>().UseLazyLoadingProxies().Options;
						var financeDbContextFactory = new FinanceDBContextFactory(_container.Resolve<IDataSourceManager>());
						using var financeDbContext = financeDbContextFactory.CreateDbContext(dbContextOptions);
						LoadDatabase(financeDbContext);
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
				case Type dbContextType when typeof(FinanceDbContext).IsAssignableFrom(dbContextType):
					{
						var dbContextOptions = new DbContextOptionsBuilder<FinanceDbContext>().UseLazyLoadingProxies().Options;
						var financeDbContextFactory = new FinanceDBContextFactory(_container.Resolve<IDataSourceManager>());
						using var financeDbContext = financeDbContextFactory.CreateDbContext(dbContextOptions);
						RefreshEntities(financeDbContext);
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

	public DatabaseViewerViewModel(DryIoc.IContainer container)
	{
		_container = container;

		DataTemplateSelector = new DatabaseDataTemplateSelector();
	}

	public bool CanCloseDialog() => true;

	public void OnDialogClosed()
	{
	}
	
	public void OnDialogOpened(IDialogParameters parameters)
	{
		_dbContextType = parameters.GetValue<Type>("DbContextType");

		if (LoadDatabaseCommand.CanExecute(null))
		{
			LoadDatabaseCommand.Execute(null);
		}
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
			TypeName = type.Name.Replace("Proxy", ""),
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
					.Where(et => !et.IsOwned() && et.FindPrimaryKey() != null && !et.ClrType.IsAbstract)
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
				queryable.IncludeAllNavigationPropertiesRecursive(dbContext);

				var entities = Task.Run(() =>
				{
					var list = new List<object>();
					var count = queryable.Count();
					foreach (var entity in queryable.ToList().OrderBy(e => e.GetName()))
					{
						var entityWrapper = CreateEntityWrapper(entity);
						if (entityWrapper != null)
						{
							list.Add(entityWrapper);
						}
					}
					return list;
				}).Result;

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

	#region IDisposalSupport

	public class EntityTypeInfo
	{
		public string Name { get; set; } = string.Empty;
		public Type ClrType { get; set; } = null!;
		public Microsoft.EntityFrameworkCore.Metadata.IEntityType EntityType { get; set; } = null!;
	}

	public class EntityWrapper
	{
		public string TypeName { get; set; } = string.Empty;
		public List<PropertyWrapper> Properties { get; set; } = new();
	}

	public class PropertyWrapper
	{
		public string Name { get; set; } = string.Empty;
		public object? Value { get; set; } = null!;
	}

	public class CollectionWrapper
	{
		public string Header { get; set; } = string.Empty;
		public List<object> Items { get; set; } = new();
	}

	#endregion
}