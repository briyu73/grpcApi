using Microsoft.EntityFrameworkCore;

namespace BAM.Libraries.SharedEDMLib.Extensions;

public static class EagerLoadExtensions
{
	/// <summary>
	/// Eagerly loads all navigation properties and collections for the specified entity type.
	/// </summary>
	/// <typeparam name="TEntity">The entity type to load.</typeparam>
	/// <param name="query">The IQueryable to apply the includes to.</param>
	/// <param name="dbContext">The DbContext instance to get metadata from.</param>
	/// <returns>An IQueryable with all navigation properties included.</returns>
	public static IQueryable<TEntity> IncludeAllNavigationProperties<TEntity>(
			this IQueryable<TEntity> query,
			DbContext dbContext)
			where TEntity : class
	{
		if (query == null)
			throw new ArgumentNullException(nameof(query));
		if (dbContext == null)
			throw new ArgumentNullException(nameof(dbContext));

		var entityType = dbContext.Model.FindEntityType(typeof(TEntity));
		if (entityType == null)
			throw new InvalidOperationException($"Entity type {typeof(TEntity).Name} not found in the model.");

		foreach (var navigation in entityType.GetNavigations())
		{
			query = query.Include(navigation.Name);
		}

		return query;
	}

	/// <summary>
	/// Eagerly loads all navigation properties and collections recursively for the specified entity type.
	/// </summary>
	/// <typeparam name="TEntity">The entity type to load.</typeparam>
	/// <param name="query">The IQueryable to apply the includes to.</param>
	/// <param name="dbContext">The DbContext instance to get metadata from.</param>
	/// <param name="maxDepth">The maximum depth for recursive loading (default is 3).</param>
	/// <returns>An IQueryable with all navigation properties included recursively.</returns>
	public static IQueryable<TEntity> IncludeAllNavigationPropertiesRecursive<TEntity>(
			this IQueryable<TEntity> query,
			DbContext dbContext,
			int maxDepth = 3)
			where TEntity : class
	{
		if (query == null)
			throw new ArgumentNullException(nameof(query));
		if (dbContext == null)
			throw new ArgumentNullException(nameof(dbContext));
		if (maxDepth < 0)
			throw new ArgumentException("Max depth cannot be negative.", nameof(maxDepth));

		return IncludeNavigationPropertiesRecursiveInternal(query, dbContext, typeof(TEntity), maxDepth);
	}

	private static IQueryable<TEntity> IncludeNavigationPropertiesRecursiveInternal<TEntity>(
			IQueryable<TEntity> query,
			DbContext dbContext,
			Type entityType,
			int maxDepth,
			string currentPath = "")
			where TEntity : class
	{
		if (maxDepth <= 0)
			return query;

		var entityMetadata = dbContext.Model.FindEntityType(entityType);
		if (entityMetadata == null)
			return query;

		foreach (var navigation in entityMetadata.GetNavigations())
		{
			var navigationPath = string.IsNullOrEmpty(currentPath) ? navigation.Name : $"{currentPath}.{navigation.Name}";
			query = query.Include(navigationPath);

			// Recursively include navigation properties for the target type
			var targetType = navigation.TargetEntityType.ClrType;
			query = IncludeNavigationPropertiesRecursiveInternal(query, dbContext, targetType, maxDepth - 1, navigationPath);
		}

		return query;
	}
}