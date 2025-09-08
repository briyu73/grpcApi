
using BAM.Modules.DataAccess.UI.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using static BAM.Modules.DataAccess.UI.ViewModels.DatabaseViewerViewModel;

namespace BAM.Modules.DataAccess.UI.Views
{
	public class DatabaseDataTemplateSelector : DataTemplateSelector
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

		public override DataTemplate? SelectTemplate(object item, DependencyObject container)
		{
			if (container is FrameworkElement element)
			{
				if (item == null)
					return element.FindResource("NullTemplate") as DataTemplate;

				if (item is EntityWrapper)
					return element.FindResource("ComplexObjectTemplate") as DataTemplate;

				if (item is CollectionWrapper)
					return element.FindResource("CollectionTemplate") as DataTemplate;

				var type = item.GetType();

				if (type == typeof(DateTime) || type == typeof(DateTime?))
					return element.FindResource("DateTimeTemplate") as DataTemplate;

				if (type == typeof(Guid) || type == typeof(Guid?))
					return element.FindResource("GuidTemplate") as DataTemplate;

				if (IsPrimitiveType(type))
					return element.FindResource("PrimitiveTemplate") as DataTemplate;

				return element.FindResource("ComplexObjectTemplate") as DataTemplate;
			}

			return null;
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

		#endregion

		// *******************************************************************************************
		// Disposal Support
		// *******************************************************************************************

		#region IDisposalSupport

		#endregion
	}
}
