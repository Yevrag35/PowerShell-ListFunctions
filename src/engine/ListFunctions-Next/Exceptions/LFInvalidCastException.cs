using ListFunctions.Extensions;

#nullable enable

namespace ListFunctions.Exceptions;

/// <summary>
/// Represents the error that occurs when a ListFunctions cmdlet cannot convert an item to the type that its collection
/// or comparer requires.
/// </summary>
/// <remarks>
/// <para>
/// This exception wraps a <see cref="PSInvalidCastException"/> from a failed PowerShell conversion. Its message names
/// the item, the item's type, the target type, and the reason that the conversion failed, and its
/// <see cref="RuntimeException.ErrorRecord"/> carries the same details as category information and a recommended action.
/// </para>
/// <para>
/// The wrapped exception does not become the <see cref="Exception.InnerException"/> of this exception. Instead, this
/// exception takes over the wrapped exception's inner exception, stack trace, <see cref="Exception.HResult"/>, and
/// <see cref="Exception.HelpLink"/>.
/// </para>
/// </remarks>
public sealed class LFInvalidCastException : PSInvalidCastException
{
	private const string MSG_FORMAT = "Cannot convert value \"{0}\" of type \"{1}\" to type \"{2}\".";
	private const string ADD_FORMAT = MSG_FORMAT + " Error: {3}";
	private static readonly string s_namespace = typeof(LFInvalidCastException).Namespace ?? "";

	private readonly string? _itemAsStr;
	private readonly string _message;
	private readonly string? _stackTrace;

	/// <summary>
	/// Gets the message that describes the failed conversion.
	/// </summary>
	/// <value>
	/// A message in the form <c>Cannot convert value "item" of type "itemType" to type "targetType". Error: reason</c>,
	/// where <c>reason</c> is the message of the innermost exception of the wrapped exception. When the item is
	/// <see langword="null"/>, the value is empty and the type is <c>null</c>.
	/// </value>
	public override string Message => _message;

	/// <summary>
	/// Gets the stack trace of the wrapped exception.
	/// </summary>
	/// <value>
	/// The stack trace of the <see cref="PSInvalidCastException"/> that this exception wraps, or, when that stack trace is
	/// <see langword="null"/>, the stack trace of this exception.
	/// </value>
	public override string? StackTrace => _stackTrace ?? base.StackTrace;

	/// <summary>
	/// Gets or sets the name of the application or object that causes the error.
	/// </summary>
	/// <value>
	/// The value that the base implementation returns, or, when that value is <see langword="null"/>, the namespace of
	/// <see cref="LFInvalidCastException"/>.
	/// </value>
	public override string? Source
	{
		get => base.Source ??= s_namespace;
		set => base.Source = value;
	}

	/// <summary>
	/// Initializes a new <see cref="LFInvalidCastException"/> instance from the specified cast exception, the type that
	/// the conversion targets, and the item that fails to convert.
	/// </summary>
	/// <remarks>
	/// The constructor fills in the <see cref="ErrorCategoryInfo"/> of <see cref="RuntimeException.ErrorRecord"/>:
	/// <see cref="ErrorCategoryInfo.Reason"/> gets the <see cref="ErrorRecord.FullyQualifiedErrorId"/> of
	/// <paramref name="inner"/>, <see cref="ErrorCategoryInfo.TargetName"/> gets the string form of
	/// <paramref name="item"/>, and <see cref="ErrorCategoryInfo.TargetType"/> gets the full name of its type. It also
	/// sets <see cref="ErrorRecord.ErrorDetails"/> to the message, with a recommended action that names
	/// <paramref name="convertingTo"/>. It does not change <paramref name="inner"/>.
	/// </remarks>
	/// <param name="inner">The exception that PowerShell throws when the conversion fails. This value must not be <see langword="null"/>.</param>
	/// <param name="convertingTo">The type that the conversion targets. This value must not be <see langword="null"/>.</param>
	/// <param name="item">The item that fails to convert, or <see langword="null"/> when the conversion starts from a <see langword="null"/> value.</param>
	/// <exception cref="NullReferenceException">Thrown when <paramref name="inner"/> or <paramref name="convertingTo"/> is null.</exception>
	public LFInvalidCastException(PSInvalidCastException inner, Type convertingTo, object? item)
		: base(string.Empty, inner.InnerException)
	{
		_message = FormatMessage(inner, item, convertingTo, out _itemAsStr, out string? itemType);
		_stackTrace = inner.StackTrace;

		this.ErrorRecord.CategoryInfo.Reason = inner.ErrorRecord.FullyQualifiedErrorId;
		this.ErrorRecord.CategoryInfo.TargetName = _itemAsStr ?? string.Empty;
		this.ErrorRecord.CategoryInfo.TargetType = itemType ?? string.Empty;
		this.ErrorRecord.ErrorDetails = new ErrorDetails(_message)
		{
			RecommendedAction = ConstructRecommendedAction(convertingTo),
		};

		this.HResult = inner.HResult;
		this.HelpLink = inner.HelpLink;
	}

	/// <summary>
	/// Creates the recommended action for the error record of this exception.
	/// </summary>
	/// <param name="convertingTo">The type that the conversion targets. This value must not be <see langword="null"/>.</param>
	/// <returns>A sentence that tells the user to check that the input can be converted to <paramref name="convertingTo"/>, which it names by its full name.</returns>
	/// <exception cref="NullReferenceException">Thrown when <paramref name="convertingTo"/> is null.</exception>
	private static string ConstructRecommendedAction(Type convertingTo)
	{
		const string recom_act = "Validate that the object being passed can be converted to \"{0}\".";

		return string.Format(recom_act, convertingTo.GetTypeName());
	}

	/// <summary>
	/// Creates the message for this exception and gets the string form and type name of the item that fails to convert.
	/// </summary>
	/// <param name="inner">The exception that PowerShell throws when the conversion fails. Its innermost exception supplies the reason in the message. This value must not be <see langword="null"/>.</param>
	/// <param name="item">The item that fails to convert, or <see langword="null"/>.</param>
	/// <param name="convertingTo">The type that the conversion targets. This value must not be <see langword="null"/>.</param>
	/// <param name="itemAsStr">When this method returns, contains the result of calling <see cref="object.ToString"/> on <paramref name="item"/>, or <see langword="null"/> when <paramref name="item"/> is <see langword="null"/>.</param>
	/// <param name="itemType">When this method returns, contains the full name of the type of <paramref name="item"/>, or <see langword="null"/> when <paramref name="item"/> is <see langword="null"/>.</param>
	/// <returns>
	/// A message in the form <c>Cannot convert value "item" of type "itemType" to type "targetType". Error: reason</c>.
	/// When <paramref name="item"/> is <see langword="null"/>, the value is empty and the type is <c>null</c>.
	/// </returns>
	/// <exception cref="NullReferenceException">Thrown when <paramref name="inner"/> or <paramref name="convertingTo"/> is null.</exception>
	private static string FormatMessage(Exception inner, object? item, Type convertingTo, out string? itemAsStr, out string? itemType)
	{
		itemType = item?.GetType().GetTypeName();
		string type = itemType ?? "null";
		itemAsStr = item?.ToString();

		Exception baseEx = inner.GetBaseException();

		return string.Format(ADD_FORMAT, itemAsStr, type, convertingTo.GetTypeName(), baseEx.Message);
	}
}
