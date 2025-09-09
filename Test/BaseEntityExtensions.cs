
using BAM.Libraries.SharedEDMLib.Model.Structural;

namespace BAM.Libraries.SharedEDMLib.Extensions;

public static class BaseEntityExtensions
{
	// *******************************************************************************************
	// Members
	// *******************************************************************************************

	#region Members

	#endregion Members

	// *******************************************************************************************
	// Properties
	// *******************************************************************************************

	#region Properties

	#endregion

	// *******************************************************************************************
	// Commands
	// *******************************************************************************************

	#region Commands

	#endregion

	// *******************************************************************************************
	// Public Methods
	// *******************************************************************************************

	#region Public Methods

	// Flattens and returns all child BaseEntity objects within the passed node
	public static IEnumerable<BaseEntity> Flatten(this BaseEntity node)
	{
		var list = new List<BaseEntity>();
		list.Add(node);

		var properties = node.GetType().GetProperties();
		foreach (var property in properties) 
		{
			var value = property.GetValue(node) as BaseEntity;
			if (value != null)
			{
				list.AddRange(value.Flatten());
			}
		}

		return list;
	}

	// returns a name property if found on the object, otherwise it returns it's id
	public static string GetName(this BaseEntity node)
	{
		var nameProperty = node.GetType().GetProperty("Name");
		if (nameProperty != null)
		{
			return nameProperty.GetValue(node) as string ?? string.Empty;
		}

		return node.Id.ToString();
	}

	public static bool TryGetObjectName(this BaseEntity baseEntity, out string name)
	{
		var nameProperty = baseEntity.GetType().GetProperties()
			.Where(p => p.CanRead)
			.FirstOrDefault(p => p.Name == "Name");

		if (nameProperty != null)
		{
			name = nameProperty.GetValue(baseEntity)?.ToString() ?? string.Empty;
			return true;
		}

		name = string.Empty;
		return false;
	}

	public static bool TryGetObjectName(this BaseEntity baseEntity, string name)
	{
		// if object has a writeable name proeprty just use that
		var nameProperty = baseEntity.GetType().GetProperties()
			.Where(p => p.CanWrite)
			.FirstOrDefault(p => p.Name == "Name");

		if (nameProperty != null)
		{
			// check for an invalid empty or white spaced name
			if (string.IsNullOrWhiteSpace(name))
			{
				name = HelperFns.DEFAULT_UNKNOWN_NAME;
			}

			nameProperty.SetValue(baseEntity, name);
			return true;
		}

		return false;
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

	#endregion

	// *******************************************************************************************
	// Disposal Support
	// *******************************************************************************************

	#region IDisposalSupport

	#endregion
}
