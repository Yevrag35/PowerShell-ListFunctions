using ListFunctions.Extensions;
using ListFunctions.Internal;

namespace ListFunctions.Modern.Exceptions;

/// <summary>
/// Provides the base class for exceptions that occur while a user-supplied script block runs on behalf of a
/// comparer or hash function.
/// </summary>
/// <remarks>
/// <para>
/// The exception records the object that the script block was processing, the script statement that failed, and a
/// snapshot of the variables that were injected into the script block's scope, such as <c>$_</c> or <c>$x</c> and
/// <c>$y</c>.
/// </para>
/// <para>
/// When the inner exception is a <see cref="RuntimeException"/>, the exception reuses its
/// <see cref="RuntimeException.ErrorRecord"/>, so PowerShell reports the error at the script block's position
/// instead of at the cmdlet.
/// </para>
/// </remarks>
#if !NET8_0_OR_GREATER
[Serializable]
#endif
public abstract class ScriptBlockInvocationException : RuntimeException
{
	const string DEF_MSG_ONLY_FORMAT = "{0}.";
	const string DEF_MSG_POINT_INNER_FORMAT = "{0} --> {1}";
	const string INV_MSG_FORMAT = "Invoking \"{0}\" threw '{1}'.";
	static readonly Lazy<PropertyInfo?> _statementProp = new Lazy<PropertyInfo?>(GetStatementProperty);

	/// <summary>
	/// Gets the object that the script block was processing when the error occurred.
	/// </summary>
	/// <value>The offending object, or <see langword="null"/> if the script block was processing <see langword="null"/>.</value>
	public object? Offender { get; }
	/// <summary>
	/// Gets the type that a derived class records for <see cref="Offender"/>.
	/// </summary>
	/// <remarks>
	/// The .NET Framework build uses this type when it serializes <see cref="Offender"/>.
	/// </remarks>
	/// <value>The type associated with the offending object.</value>
	protected abstract Type OffenderType { get; }
	/// <summary>
	/// Gets the script statement that failed.
	/// </summary>
	/// <value>The failing statement, or an empty string when it isn't known.</value>
	public string Script { get; }
	/// <summary>
	/// Gets the variables that were injected into the script block's scope when the error occurred.
	/// </summary>
	/// <remarks>
	/// The dictionary is a read-only snapshot. Each value is a copy when it can be cloned, so later changes to the
	/// original objects don't show up here. Variable names compare without regard to case, and when two variables share
	/// a name, the first one wins.
	/// </remarks>
	/// <value>A read-only dictionary of variable names and values. It is empty when no variables were injected.</value>
	public IReadOnlyDictionary<string, object?> Variables { get; }

	/// <summary>
	/// Initializes a new <see cref="ScriptBlockInvocationException"/> instance with the specified message, offending
	/// object, script statement, inner exception, and injected variables.
	/// </summary>
	/// <remarks>
	/// The final message combines <paramref name="message"/> with the message of <paramref name="innerException"/> and,
	/// when it isn't blank, <paramref name="statement"/>.
	/// </remarks>
	/// <param name="message">The base message that describes the error, without a trailing period.</param>
	/// <param name="offender">The object that the script block was processing, or <see langword="null"/>.</param>
	/// <param name="statement">The script statement that failed, or <see langword="null"/> if it isn't known.</param>
	/// <param name="innerException">The exception that caused this one, or <see langword="null"/>.</param>
	/// <param name="injectedVariables">
	/// The variables that were injected into the script block's scope, or <see langword="null"/> for none.
	/// </param>
	protected ScriptBlockInvocationException(string message, object? offender, string? statement, Exception? innerException, IReadOnlyList<PSVariable>? injectedVariables)
		: base(FormatMessage(message, innerException, statement), innerException)
	{
		this.Offender = offender;
		this.Script = statement ?? string.Empty;
		this.Variables = ToDictionary(injectedVariables);
	}
	/// <summary>
	/// Initializes a new <see cref="ScriptBlockInvocationException"/> instance with the specified message, offending
	/// object, script statement, inner PowerShell runtime exception, and injected variables.
	/// </summary>
	/// <remarks>
	/// The new exception takes its <see cref="RuntimeException.ErrorRecord"/>, <see cref="Exception.HelpLink"/>,
	/// <see cref="Exception.HResult"/>, <see cref="Exception.Source"/>, and
	/// <see cref="RuntimeException.WasThrownFromThrowStatement"/> values from <paramref name="innerRuntime"/>.
	/// </remarks>
	/// <param name="message">The base message that describes the error, without a trailing period.</param>
	/// <param name="offender">The object that the script block was processing, or <see langword="null"/>.</param>
	/// <param name="statement">The script statement that failed, or <see langword="null"/> if it isn't known.</param>
	/// <param name="innerRuntime">
	/// The PowerShell runtime exception that caused this one. This value must not be <see langword="null"/>.
	/// </param>
	/// <param name="injectedVariables">
	/// The variables that were injected into the script block's scope, or <see langword="null"/> for none.
	/// </param>
	protected ScriptBlockInvocationException(string message, object? offender, string? statement, RuntimeException innerRuntime, IReadOnlyList<PSVariable>? injectedVariables)
		: base(FormatMessage(message, innerRuntime, statement), innerRuntime, innerRuntime.ErrorRecord)
	{
		this.HelpLink = innerRuntime.HelpLink;
		this.HResult = innerRuntime.HResult;
		this.Offender = offender;
		this.Script = statement ?? string.Empty;
		this.Source = innerRuntime.Source;
		this.Variables = ToDictionary(injectedVariables);
		this.WasThrownFromThrowStatement = innerRuntime.WasThrownFromThrowStatement;
	}
#if !NET8_0_OR_GREATER
	/// <summary>
	/// Initializes a new <see cref="ScriptBlockInvocationException"/> instance with serialized data.
	/// </summary>
	/// <remarks>
	/// This constructor exists only in the .NET Framework and .NET Standard builds.
	/// </remarks>
	/// <param name="info">The object that holds the serialized exception data. This value must not be <see langword="null"/>.</param>
	/// <param name="context">The contextual information about the source or destination.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="info"/> is null.</exception>
	protected ScriptBlockInvocationException(SerializationInfo info, StreamingContext context)
		: base(info, context)
	{
		Guard.NotNull(info, nameof(info));
		this.Offender = info.GetValue(nameof(this.Offender), typeof(object));
		this.Script = info.GetString(nameof(this.Script)) ?? string.Empty;
		this.Variables = (IReadOnlyDictionary<string, object?>)info.GetValue(nameof(this.Variables), typeof(ReadOnlyDictionary<string, object?>));
	}

	/// <summary>
	/// Adds <see cref="Offender"/>, <see cref="Variables"/>, and <see cref="Script"/> to the serialized exception data.
	/// </summary>
	/// <remarks>
	/// This method exists only in the .NET Framework and .NET Standard builds.
	/// </remarks>
	/// <param name="info">The object that holds the serialized exception data. This value must not be <see langword="null"/>.</param>
	/// <param name="context">The contextual information about the source or destination.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="info"/> is null.</exception>
	public override void GetObjectData(SerializationInfo info, StreamingContext context)
	{
		Guard.NotNull(info, nameof(info));
		info.AddValue(nameof(this.Offender), this.Offender, this.OffenderType);
		info.AddValue(nameof(this.Variables), this.Variables, typeof(ReadOnlyDictionary<string, object?>));
		info.AddValue(nameof(this.Script), this.Script);

		base.GetObjectData(info, context);
	}

#endif
	/// <summary>
	/// Builds the exception message from the base message, the inner exception, and the failing statement.
	/// </summary>
	/// <param name="message">The base message, without a trailing period.</param>
	/// <param name="inner">The inner exception, or <see langword="null"/>.</param>
	/// <param name="statement">The failing statement, or <see langword="null"/>.</param>
	/// <returns>
	/// <paramref name="message"/> followed by a period when <paramref name="inner"/> is <see langword="null"/>;
	/// otherwise, <paramref name="message"/>, an arrow, and the inner message, which names
	/// <paramref name="statement"/> when it isn't blank.
	/// </returns>
	private static string FormatMessage(string message, Exception? inner, string? statement)
	{
		if (string.IsNullOrWhiteSpace(statement))
		{
			return inner is null
				? string.Format(DEF_MSG_ONLY_FORMAT, message)
				: string.Format(DEF_MSG_POINT_INNER_FORMAT, message, inner.Message);
		}
		else if (inner is null)
		{
			return string.Format(DEF_MSG_ONLY_FORMAT, message);
		}

		return string.Format(DEF_MSG_POINT_INNER_FORMAT, message,
			string.Format(INV_MSG_FORMAT, statement, inner.Message));
	}
	/// <summary>
	/// Returns the script statement that the specified invocation information points to.
	/// </summary>
	/// <remarks>
	/// The method reads the <c>Statement</c> property of <see cref="InvocationInfo"/> through reflection when the
	/// running version of PowerShell defines one, public or not. Otherwise, or when reading it fails or gives
	/// <see langword="null"/>, it returns the whole line from <see cref="InvocationInfo.Line"/>.
	/// </remarks>
	/// <param name="info">The invocation information of the error. This value must not be <see langword="null"/>.</param>
	/// <returns>The failing statement, or the line that contains it.</returns>
	public static string GetScriptStatement(InvocationInfo info)
	{
		if (_statementProp.Value is null)
		{
			return info.Line;
		}

		try
		{
			return _statementProp.Value.GetValue(info) as string ?? info.Line;
		}
		catch (Exception e)
		{
			Debug.WriteLine(e.Message);
			return info.Line;
		}
	}
	/// <summary>
	/// Looks up the <c>Statement</c> property of <see cref="InvocationInfo"/>.
	/// </summary>
	/// <returns>The property, or <see langword="null"/> when it doesn't exist or the lookup fails.</returns>
	private static PropertyInfo? GetStatementProperty()
	{
		try
		{
			return typeof(InvocationInfo).GetProperty("Statement",
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		}
		catch (Exception e)
		{
			Debug.WriteLine(e.Message);
			return null;
		}
	}

	/// <summary>
	/// Adds a copy of the specified variable's value to the dictionary unless a variable with the same name is already
	/// there.
	/// </summary>
	/// <param name="dict">The dictionary to add to.</param>
	/// <param name="variable">The variable to add, or <see langword="null"/> to add nothing.</param>
	private static void AddToDict(ref Dictionary<string, object?> dict, PSVariable? variable)
	{
		if (variable is null)
		{
			return;
		}

		object? val = variable.Value.CloneIf();

#if NET5_0_OR_GREATER
		_ = dict.TryAdd(variable.Name, val);
#else
		if (!dict.ContainsKey(variable.Name))
		{
			dict.Add(variable.Name, val);
		}
#endif
	}
	/// <summary>
	/// Copies the specified variables into a read-only dictionary keyed by variable name.
	/// </summary>
	/// <param name="variables">The variables to copy, or <see langword="null"/> for none.</param>
	/// <returns>
	/// A read-only dictionary whose names compare without regard to case, or a shared empty dictionary when
	/// <paramref name="variables"/> is <see langword="null"/> or empty.
	/// </returns>
	private static IReadOnlyDictionary<string, object?> ToDictionary(IReadOnlyList<PSVariable>? variables)
	{
		if (variables is null || variables.Count <= 0)
		{
			return Empty.Dictionary<string, object?>();
		}

		var dict = new Dictionary<string, object?>(variables.Count, StringComparer.InvariantCultureIgnoreCase);
		foreach (PSVariable v in variables)
		{
			AddToDict(ref dict, v);
		}

		return new ReadOnlyDictionary<string, object?>(dict);
	}
}
