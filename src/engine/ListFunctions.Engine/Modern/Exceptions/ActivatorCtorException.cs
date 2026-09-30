using ListFunctions.Extensions;

namespace ListFunctions.Modern.Exceptions;

/// <summary>
/// The exception that is thrown when creating an instance of a collection type through reflection fails.
/// </summary>
/// <remarks>
/// <see cref="Exception.InnerException"/> holds the exception that the type's constructor or
/// <see cref="Activator.CreateInstance(Type, object[])"/> threw. When the instance was <see langword="null"/>
/// instead, it holds a <see cref="NullReferenceException"/> that says so.
/// </remarks>
public sealed class ActivatorCtorException : Exception
{
	const string DEF_FORMAT = "An exception occurred attempting to construct an object of type \"{0}\".";

	/// <summary>
	/// Gets the type that couldn't be created.
	/// </summary>
	/// <value>The type whose instance creation failed.</value>
	public Type OffendingType { get; }

	/// <summary>
	/// Initializes a new <see cref="ActivatorCtorException"/> instance for the specified type when creating it returned
	/// <see langword="null"/>.
	/// </summary>
	/// <remarks>
	/// <see cref="Exception.InnerException"/> is set to a <see cref="NullReferenceException"/> that describes the
	/// <see langword="null"/> result.
	/// </remarks>
	/// <param name="offendingType">The type that couldn't be created. This value must not be <see langword="null"/>.</param>
	public ActivatorCtorException(Type offendingType)
		: this(offendingType, GetNullException())
	{
	}
	/// <summary>
	/// Initializes a new <see cref="ActivatorCtorException"/> instance for the specified type and the exception that
	/// creating it threw.
	/// </summary>
	/// <param name="offendingType">The type that couldn't be created. This value must not be <see langword="null"/>.</param>
	/// <param name="innerException">The exception that creating the instance threw, or <see langword="null"/>.</param>
	public ActivatorCtorException(Type offendingType, Exception? innerException)
		: base(string.Format(DEF_FORMAT, offendingType.GetTypeName()), innerException)
	{
		this.OffendingType = offendingType;
	}

	/// <summary>
	/// Creates the inner exception for an instance creation that returned <see langword="null"/>.
	/// </summary>
	/// <returns>A new <see cref="NullReferenceException"/> that describes the <see langword="null"/> result.</returns>
	private static NullReferenceException GetNullException()
	{
		return new NullReferenceException($"The constructed object returned from \"{nameof(Activator)}.{nameof(Activator.CreateInstance)}()\" was null.");
	}
}

