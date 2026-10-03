using ListFunctions.Cmdlets.Constructs;
using ListFunctions.Extensions;
using ListFunctions.Internal;

#nullable enable

namespace ListFunctions.Validation;

/// <summary>
/// Converts a cmdlet parameter's argument to a list of the items that it holds.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="PSObject"/> argument is unwrapped first. A <see cref="List{T}"/> of any element type is returned
/// unchanged. Any other <see cref="IEnumerable"/>, including an <see cref="object"/> array, is copied into a new
/// <see cref="List{T}"/> of <see cref="object"/>. Any other argument, including <see langword="null"/>, is wrapped in a
/// single-item <see cref="PipelineItem"/>.
/// </para>
/// <para>
/// A <see cref="string"/> is an <see cref="IEnumerable"/> of its characters, so it becomes a list of
/// <see cref="char"/> values. A dictionary becomes a list of its entries. An instance of a type that
/// derives from <see cref="List{T}"/> is copied, not returned unchanged.
/// </para>
/// <para>
/// The attribute keeps no state, so it is thread-safe.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class ListTransformAttribute : ArgumentTransformationAttribute
{
	/// <summary>
	/// The <see cref="object"/> array type, whose instances are copied instead of returned unchanged.
	/// </summary>
	private static readonly Type _objArr = typeof(object[]);

	/// <summary>
	/// Converts the specified argument to a list of the items that it holds.
	/// </summary>
	/// <remarks>
	/// The method copies only when <paramref name="inputData"/> is an <see cref="IEnumerable"/> other than a
	/// <see cref="List{T}"/>. It enumerates that argument once, and the new list doesn't change when the argument
	/// changes later.
	/// </remarks>
	/// <param name="engineIntrinsics">The engine intrinsics of the session that binds the parameter. The method doesn't use it.</param>
	/// <param name="inputData">The argument to convert. This value can be <see langword="null"/>, and it can be wrapped in a <see cref="PSObject"/>.</param>
	/// <returns>
	/// The unwrapped <paramref name="inputData"/> when it is a <see cref="List{T}"/>; a new <see cref="List{T}"/> of
	/// <see cref="object"/> that holds its elements when it is any other <see cref="IEnumerable"/>; otherwise, a
	/// <see cref="PipelineItem"/> that holds it.
	/// </returns>
	public override object? Transform(EngineIntrinsics engineIntrinsics, object? inputData)
	{
		object? target = inputData.GetBaseObject();

		if (target is not null
			&&
			!IsObjectArrayType(target, out Type actualType)
			&&
			IsGenericList(actualType))
		{
			return target;
		}
		else if (target is IEnumerable enumerable)
		{
			var list = new List<object?>(2);

			foreach (object? item in enumerable)
			{
				list.Add(item);
			}

			return list;
		}
		else
		{
			return new PipelineItem(target);
		}
	}

	/// <summary>
	/// Determines whether the specified type is a constructed <see cref="List{T}"/>.
	/// </summary>
	/// <remarks>
	/// The check compares generic type definitions, so it returns <see langword="false"/> for a type that derives from
	/// <see cref="List{T}"/>.
	/// </remarks>
	/// <param name="actualType">The type to check. This value must not be <see langword="null"/>.</param>
	/// <returns><see langword="true"/> if <paramref name="actualType"/> is <see cref="List{T}"/> for some element type; otherwise, <see langword="false"/>.</returns>
	private static bool IsGenericList(Type actualType)
	{
		return actualType.IsGenericType
			   &&
			   NewListCmdlet.ListTypeNoT.Equals(actualType.GetGenericTypeDefinition());
	}

	/// <summary>
	/// Determines whether the specified object is an <see cref="object"/> array, and returns its runtime type.
	/// </summary>
	/// <param name="target">The object to check. This value must not be <see langword="null"/>.</param>
	/// <param name="actualType">When this method returns, contains the runtime type of <paramref name="target"/>.</param>
	/// <returns><see langword="true"/> if the runtime type of <paramref name="target"/> is exactly <see cref="object"/>[]; otherwise, <see langword="false"/>.</returns>
	private static bool IsObjectArrayType(object target, out Type actualType)
	{
		actualType = target.GetType();
		return _objArr.Equals(actualType);
	}
}

