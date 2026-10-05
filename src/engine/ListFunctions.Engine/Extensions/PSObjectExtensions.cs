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
public static class PSObjectExtensions
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
	/// On .NET Framework and .NET Standard targets, the method reads <see cref="PSObject"/>'s non-public fields through
	/// reflection. If the loaded PowerShell version doesn't have those fields, the first call throws a
	/// <see cref="TypeInitializationException"/>.
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
		if (!TryGetPSObject(obj, out PSObject? mshObj) || Marshal.IsImmediateBaseObjectIsEmpty(mshObj))
			return obj;

		object? returnValue;
		do
		{
			returnValue = Marshal.GetRawImmediateBaseObject(mshObj);
			mshObj = returnValue as PSObject;
		} while ((mshObj is not null) && !Marshal.IsImmediateBaseObjectIsEmpty(mshObj));

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
	/// Reads the non-public state of a <see cref="PSObject"/> that holds its immediate base object.
	/// </summary>
	/// <remarks>
	/// <para>
	/// PowerShell exposes whether a <see cref="PSObject"/> has a base object only through non-public members, so this
	/// class reads them directly.
	/// </para>
	/// <para>
	/// On .NET 9 and later, the class uses <c>UnsafeAccessorAttribute</c>, which binds to the members when it first
	/// calls them. On earlier targets, it looks up the fields once through reflection in its static constructor.
	/// </para>
	/// </remarks>
	private static class Marshal
	{
#if NET9_0_OR_GREATER
		/// <summary>
		/// Gets the raw value of the <see cref="PSObject"/>'s immediate base object field.
		/// </summary>
		/// <param name="psObject">The <see cref="PSObject"/> to read. This value must not be <see langword="null"/>.</param>
		/// <returns>The value of the immediate base object field, which can be <see langword="null"/>.</returns>
		/// <exception cref="MissingFieldException">Thrown when the loaded PowerShell version has no field named <c>_immediateBaseObject</c> on PSObject.</exception>
		internal static object? GetRawImmediateBaseObject(PSObject psObject)
		{
			return GetImmediateBaseObject(psObject);
		}
		/// <summary>
		/// Determines whether the <see cref="PSObject"/> has no immediate base object.
		/// </summary>
		/// <param name="psObject">The <see cref="PSObject"/> to read. This value must not be <see langword="null"/>.</param>
		/// <returns><see langword="true"/> if <paramref name="psObject"/> has no immediate base object; otherwise, <see langword="false"/>.</returns>
		/// <exception cref="MissingMethodException">Thrown when the loaded PowerShell version has no ImmediateBaseObjectIsEmpty property getter on PSObject.</exception>
		internal static bool IsImmediateBaseObjectIsEmpty(PSObject psObject)
		{
			return ImmediateBaseObjectIsEmpty(psObject);
		}

		/// <summary>
		/// Gets a reference to the <see cref="PSObject"/>'s non-public <c>_immediateBaseObject</c> field.
		/// </summary>
		/// <remarks>
		/// The runtime supplies this method's body. The returned reference points into <paramref name="psObject"/>, so
		/// writing through it changes that object's base object.
		/// </remarks>
		/// <param name="psObject">The <see cref="PSObject"/> whose field to access.</param>
		/// <returns>A reference to the field.</returns>
		/// <exception cref="MissingFieldException">Thrown when the field doesn't exist on PSObject.</exception>
		[UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_immediateBaseObject")]
		private static extern ref object? GetImmediateBaseObject(PSObject psObject);

		/// <summary>
		/// Calls the getter of the <see cref="PSObject"/>'s non-public <c>ImmediateBaseObjectIsEmpty</c> property.
		/// </summary>
		/// <remarks>
		/// The runtime supplies this method's body.
		/// </remarks>
		/// <param name="psObject">The <see cref="PSObject"/> whose property to read.</param>
		/// <returns>The value of the property.</returns>
		/// <exception cref="MissingMethodException">Thrown when the property getter doesn't exist on PSObject.</exception>
		[UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_ImmediateBaseObjectIsEmpty")]
		private static extern bool ImmediateBaseObjectIsEmpty(PSObject psObject);

#else
		/// <summary>
		/// The <see cref="PSObject"/>'s non-public <c>immediateBaseObject</c> field.
		/// </summary>
		private static readonly FieldInfo s_immediateBaseObjectField;
		/// <summary>
		/// The <see cref="PSObject"/>'s non-public <c>immediateBaseObjectIsEmpty</c> field.
		/// </summary>
		private static readonly FieldInfo s_immediateBaseObjectIsEmptyField;
		/// <summary>
		/// Initializes the <see cref="Marshal"/> class by looking up the <see cref="PSObject"/> fields it reads.
		/// </summary>
		/// <exception cref="InvalidOperationException">Thrown when either field doesn't exist on PSObject. The runtime wraps it in a TypeInitializationException.</exception>
		static Marshal()
		{
			s_immediateBaseObjectIsEmptyField = typeof(PSObject).GetField("immediateBaseObjectIsEmpty", BindingFlags.NonPublic | BindingFlags.Instance)
				?? throw new InvalidOperationException("Could not find field 'immediateBaseObjectIsEmpty' on type 'PSObject'.");

			s_immediateBaseObjectField = typeof(PSObject).GetField("immediateBaseObject", BindingFlags.NonPublic | BindingFlags.Instance)
				?? throw new InvalidOperationException("Could not find field 'immediateBaseObject' on type 'PSObject'.");
		}

		/// <summary>
		/// Gets the raw value of the <see cref="PSObject"/>'s immediate base object field.
		/// </summary>
		/// <param name="psObject">The <see cref="PSObject"/> to read. This value must not be <see langword="null"/>.</param>
		/// <returns>The value of the immediate base object field, which can be <see langword="null"/>.</returns>
		/// <exception cref="TypeInitializationException">Thrown when the class's static constructor can't find the PSObject fields it reads.</exception>
		internal static object? GetRawImmediateBaseObject(PSObject psObject)
		{
			return s_immediateBaseObjectField.GetValue(psObject);
		}

		/// <summary>
		/// Determines whether the <see cref="PSObject"/> has no immediate base object.
		/// </summary>
		/// <remarks>
		/// If the field's value isn't a <see cref="bool"/>, the method returns <see langword="false"/>.
		/// </remarks>
		/// <param name="psObject">The <see cref="PSObject"/> to read. This value must not be <see langword="null"/>.</param>
		/// <returns><see langword="true"/> if <paramref name="psObject"/> has no immediate base object; otherwise, <see langword="false"/>.</returns>
		/// <exception cref="TypeInitializationException">Thrown when the class's static constructor can't find the PSObject fields it reads.</exception>
		internal static bool IsImmediateBaseObjectIsEmpty(PSObject psObject)
		{
			return s_immediateBaseObjectIsEmptyField.GetValue(psObject) as bool? ?? false;
		}
#endif
	}
}
