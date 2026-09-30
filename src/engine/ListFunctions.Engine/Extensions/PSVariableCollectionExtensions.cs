using System.Collections.ObjectModel;

namespace ListFunctions.Extensions;

public static class PSVariableCollectionExtensions
{
	[return: NotNullIfNotNull(nameof(defaultIfNull))]
	public static T GetFirstValue<T>(this Collection<PSObject>? collection, Func<object, T> convert, T defaultIfNull = default!)
	{
		if (collection is null || collection.Count == 0 || collection[0] is not PSObject pso || !pso.TryGetBaseObject(out object? o))
		{
			return defaultIfNull;
		}

		try
		{
			return convert(o);
		}
		catch (Exception e)
		{
			Debug.WriteLine(e.Message);
			return defaultIfNull;
		}
	}

	[return: MaybeNull]
	public static T GetLastValue<T>(this Collection<PSObject>? collection, Func<object, T> convert)
	{
		if (collection is null || collection.Count == 0)
			return default;

		object? last = collection[collection.Count - 1]?.ImmediateBaseObject;
		if (last is null)
			return default;

		try
		{
			return convert(PSObject.AsPSObject(last).ImmediateBaseObject);
		}
		catch
		{
			return default;
		}
	}
}
