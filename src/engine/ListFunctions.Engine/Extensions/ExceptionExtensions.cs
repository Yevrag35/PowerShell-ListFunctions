namespace ListFunctions.Extensions;

/// <summary>
/// Provides extension methods that wrap an <see cref="Exception"/> in a PowerShell <see cref="ErrorRecord"/>.
/// </summary>
public static class ExceptionExtensions
{
	extension(ArgumentNullException e)
	{
		/// <summary>
		/// Throws an <see cref="ArgumentNullException"/> if the specified object is <see langword="null"/>.
		/// </summary>
		/// <param name="o">The object to check for <see langword="null"/>.</param>
		/// <param name="paramName">The name of the parameter to include in the exception.</param>
		public static void ThrowIfNull([NotNull] object? o, string? paramName)
		{
			Guard.NotNull(o, paramName);
		}
	}

	/// <summary>
	/// Creates an <see cref="ErrorRecord"/> for the exception, with the specified category and no target object.
	/// </summary>
	/// <remarks>
	/// This overload calls <see cref="ToRecord(Exception, ErrorCategory, object)"/> with a target object of
	/// <see langword="null"/>.
	/// </remarks>
	/// <param name="exception">The exception to wrap. This value can be <see langword="null"/>.</param>
	/// <param name="category">The error category to assign to the record.</param>
	/// <returns>
	/// A new <see cref="ErrorRecord"/> that wraps <paramref name="exception"/>, or <see langword="null"/> if
	/// <paramref name="exception"/> is <see langword="null"/>.
	/// </returns>
	[DebuggerStepThrough]
	[return: NotNullIfNotNull(nameof(exception))]
	public static ErrorRecord? ToRecord(this Exception? exception, ErrorCategory category)
	{
		return ToRecord(exception, category, null);
	}
	/// <summary>
	/// Creates an <see cref="ErrorRecord"/> for the exception, with the specified category and target object.
	/// </summary>
	/// <remarks>
	/// The record's error ID is the full name of the exception's run-time type, such as
	/// <c>System.ArgumentException</c>. If the type has no full name, the error ID is its simple name.
	/// </remarks>
	/// <param name="exception">The exception to wrap. This value can be <see langword="null"/>.</param>
	/// <param name="category">The error category to assign to the record.</param>
	/// <param name="targetObj">The object that was being processed when the error occurred. This value can be <see langword="null"/>.</param>
	/// <returns>
	/// A new <see cref="ErrorRecord"/> that wraps <paramref name="exception"/>, or <see langword="null"/> if
	/// <paramref name="exception"/> is <see langword="null"/>.
	/// </returns>
	[return: NotNullIfNotNull(nameof(exception))]
	public static ErrorRecord? ToRecord(this Exception? exception, ErrorCategory category, object? targetObj)
	{
		if (exception is null)
		{
			return null;
		}

		return new ErrorRecord(exception, exception.GetType().GetTypeName(), category, targetObj);
	}
}
