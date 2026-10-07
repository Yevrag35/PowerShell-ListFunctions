using System.Management.Automation.Internal;

namespace ListFunctions.Extensions;

/// <summary>
/// Provides extension methods that unwrap the base object from one or more <see cref="PSObject"/> layers.
/// </summary>
/// <remarks>
/// <para>
/// PowerShell often wraps a value in a <see cref="PSObject"/>, and sometimes wraps that <see cref="PSObject"/> in
/// another. These methods return the value at the center so that callers can work with the original .NET object.
/// </para>
/// <para>
/// Unlike <see cref="PSObject.BaseObject"/>, these methods return a <see cref="PSObject"/> that has no base object, such
/// as one created with <c>[pscustomobject]@{}</c>, as itself.
/// </para>
/// </remarks>
internal static class PSObjectExtensions
{
	/// <summary>
	/// Gets the innermost base object of the specified object by unwrapping every nested <see cref="PSObject"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The method returns <paramref name="obj"/> unchanged when it isn't a <see cref="PSObject"/>, when it is
	/// PowerShell's automation null value, or when it is a <see cref="PSObject"/> that has no base object.
	/// </para>
	/// <para>
	/// Otherwise, the method unwraps one layer at a time. It stops at the first base object that isn't a
	/// <see cref="PSObject"/>, or at a nested <see cref="PSObject"/> that has no base object, and returns that object.
	/// </para>
	/// <para>
	/// A <see cref="PSObject"/> has no base object when its <see cref="PSObject.ImmediateBaseObject"/> is a
	/// <see cref="PSCustomObject"/>, the placeholder that PowerShell gives a <see cref="PSObject"/> it creates without one,
	/// such as a custom object or a deserialized property bag.
	/// </para>
	/// </remarks>
	/// <param name="obj">The object to unwrap. This value can be a <see cref="PSObject"/>, any other object, or <see langword="null"/>.</param>
	/// <returns>
	/// The innermost base object if <paramref name="obj"/> wraps one; otherwise, <paramref name="obj"/> itself.
	/// <see langword="null"/> if <paramref name="obj"/> is <see langword="null"/> or the innermost base object is
	/// <see langword="null"/>.
	/// </returns>
	public static object? GetBaseObject(this object? obj)
	{
		if (!TryGetPSObject(obj, out PSObject? mshObj) || !HasBaseObject(mshObj))
		{
			return obj;
		}

		object? returnValue;
		do
		{
			returnValue = mshObj.ImmediateBaseObject;
			mshObj = returnValue as PSObject;
		} while ((mshObj is not null) && HasBaseObject(mshObj));

		return returnValue;
	}
	/// <summary>
	/// Attempts to get the innermost base object of the specified object.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This method unwraps <paramref name="obj"/> the same way as <see cref="GetBaseObject(object)"/>.
	/// It succeeds whenever the result isn't <see langword="null"/>, including when <paramref name="obj"/> isn't a
	/// <see cref="PSObject"/> and the result is <paramref name="obj"/> itself.
	/// </para>
	/// <para>
	/// The method fails when <paramref name="obj"/> is <see langword="null"/> or the innermost base object is
	/// <see langword="null"/>.
	/// </para>
	/// </remarks>
	/// <param name="obj">The object to unwrap. This value can be <see langword="null"/>.</param>
	/// <param name="result">
	/// When this method returns, contains the innermost base object, or <paramref name="obj"/> itself if it doesn't wrap one.
	/// Contains <see langword="null"/> if the method returns <see langword="false"/>.
	/// </param>
	/// <returns><see langword="true"/> if the unwrapped object isn't <see langword="null"/>; otherwise, <see langword="false"/>.</returns>
	public static bool TryGetBaseObject([NotNullWhen(true)] this object? obj, [NotNullWhen(true)] out object? result)
	{
		result = GetBaseObject(obj);
		return result is not null;
	}

	/// <summary>
	/// Determines whether the specified object is a <see cref="PSObject"/> that can be unwrapped.
	/// </summary>
	/// <remarks>
	/// PowerShell's automation null value is a <see cref="PSObject"/>, but this method treats it as a value that can't be
	/// unwrapped.
	/// </remarks>
	/// <param name="obj">The object to test. This value can be <see langword="null"/>.</param>
	/// <param name="mshObj">
	/// When this method returns, contains <paramref name="obj"/> cast to <see cref="PSObject"/>, or <see langword="null"/>
	/// if <paramref name="obj"/> isn't a <see cref="PSObject"/>. If <paramref name="obj"/> is the automation null value,
	/// contains that value even though the method returns <see langword="false"/>.
	/// </param>
	/// <returns>
	/// <see langword="true"/> if <paramref name="obj"/> is a <see cref="PSObject"/> other than
	/// <see cref="AutomationNull.Value"/>; otherwise, <see langword="false"/>.
	/// </returns>
	private static bool TryGetPSObject(object? obj, [NotNullWhen(true)] out PSObject? mshObj)
	{
		return (mshObj = obj as PSObject) is not null && !mshObj.Equals(AutomationNull.Value);
	}

	/// <summary>
	/// Determines whether the specified <see cref="PSObject"/> has a base object.
	/// </summary>
	/// <remarks>
	/// PowerShell also records that a <see cref="PSObject"/> has no base object in a non-public flag, which it sets when,
	/// and only when, it stores a <see cref="PSCustomObject"/> as the immediate base object. The method tests the public
	/// <see cref="PSObject.ImmediateBaseObject"/> property instead, so it doesn't depend on members that a PowerShell
	/// release can rename or remove.
	/// </remarks>
	/// <param name="mshObj">The <see cref="PSObject"/> to test. This value must not be <see langword="null"/>.</param>
	/// <returns>
	/// <see langword="true"/> if the immediate base object of <paramref name="mshObj"/> isn't a
	/// <see cref="PSCustomObject"/>; otherwise, <see langword="false"/>.
	/// </returns>
	private static bool HasBaseObject(PSObject mshObj)
	{
		return mshObj.ImmediateBaseObject is not PSCustomObject;
	}
}
