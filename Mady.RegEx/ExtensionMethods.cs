﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Mady.RegEx
{
	public static class ExtensionMethods
	{
		/// <summary>
		/// <para>Maps a regular expression <c>Match</c> to an object, matching named capture groups to object properties.</para>
		/// <para>Map nested properties by using <c>__</c> as a separator in the capture group (eg: <c>Order__Customer__FirstName</c>)</para>
		/// </summary>
		/// <typeparam name="T">The type of the object to map to.</typeparam>
		/// <param name="match">The regular expression <c>Match</c> object to map.</param>
		/// <param name="processors">A dictionary of processing functions keyed by match names, to process the data during mapping.</param>
		/// <param name="obj">An existing object to map on to.</param>
		/// <returns>An object of type T with the <c>Match</c> mapped on to it.</returns>
		public static T MapTo<T>(this Match match, Dictionary<string, Func<string, object?>>? processors = null, T obj = null) where T : class?, new()
		{
			var result = obj ?? new T();

			// Skip 0 since it's the full result and we only want the individual items.
			for (int i = 1; i < match.Groups.Count; i++)
			{
				var group = match.Groups[i];
				if (group.Captures.Count == 1)
				{
					result.SetProperty(processors, group.Name, group.Name, group.Value);
				}
				else
				{
					foreach (Capture? capture in group.Captures)
					{
						if (capture != null)
						{
							result.SetProperty(processors, group.Name, group.Name, capture.Value);
						}
					}
				}
			}

			return result;
		}

		private static void SetProperty(this object obj, Dictionary<string, Func<string, object?>>? processors, string fullPropertyName, string propertyName, string value)
		{
			int index = propertyName.IndexOf("__");
			if (index > -1)
			{
				string currPropName = propertyName[..index];
				string remainingPropName = propertyName[(index + 2)..];

				var prop = obj.GetType().GetProperty(currPropName);
				if (prop != null && prop.CanWrite)
				{
					var propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
					var propObj = prop.GetValue(obj);
					if (propObj == null)
					{
						propObj = Activator.CreateInstance(propType);
						prop.SetValue(obj, propObj);
					}
					propObj?.SetProperty(processors, fullPropertyName, remainingPropName, value);
				}
			}
			else
			{
				var prop = obj.GetType().GetProperty(propertyName);
				if (prop != null && prop.CanWrite)
				{
					var propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
					if (propType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>)))
					{
						var collTypes = propType.GetGenericArguments();
						if (collTypes.Length == 1)
						{
							var collType = Nullable.GetUnderlyingType(collTypes[0]) ?? collTypes[0];

							var addMethod = propType.GetMethod("Add");
							if (addMethod != null)
							{
								var collection = prop.GetValue(obj);
								if (collection == null)
								{
									collection = Activator.CreateInstance(propType);
									prop.SetValue(obj, collection);
								}

								var collValue = GetProcessValue(processors, fullPropertyName, value, collType);
								addMethod.Invoke(collection, new[] { collValue });
							}
						}
					}
					else
					{
						var propValue = GetProcessValue(processors, fullPropertyName, value, propType);
						prop.SetValue(obj, propValue);
					}
				}
			}
		}

		private static object? GetProcessValue(Dictionary<string, Func<string, object?>>? processors, string fullPropertyName, string value, Type type)
		{
			var processedValue = (processors != null && processors.ContainsKey(fullPropertyName)) ? processors[fullPropertyName](value) : value;
			return processedValue == null ? null : Convert.ChangeType(processedValue, type);
		}
	}
}
