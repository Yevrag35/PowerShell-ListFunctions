namespace ListFunctions.Modern.Exceptions;

/// <summary>
/// The exception that is thrown when a hash code script block fails or doesn't return a usable hash code.
/// </summary>
/// <remarks>
/// Use one of the <c>FromBlockException</c> methods to create an instance from the exception that the script block
/// threw. They keep the PowerShell error record and the failing statement when the exception carries them.
/// </remarks>
//#if !NET8_0_OR_GREATER
[Serializable]
//#endif
public sealed class HashCodeScriptException : ScriptBlockInvocationException
{
	const string DEF_MSG = "An exception occurred trying to calculate the hash code of a specific object";

	/// <summary>
	/// Gets the type of the object whose hash code the script block was calculating.
	/// </summary>
	/// <value>
	/// The type argument of the <c>FromBlockException</c> call that created the exception, or <see cref="object"/> when
	/// no type was given.
	/// </value>
	public Type HashCodeBlockType { get; }
	/// <summary>
	/// Gets the type that is recorded for <see cref="ScriptBlockInvocationException.Offender"/>.
	/// </summary>
	/// <value>The value of <see cref="HashCodeBlockType"/>.</value>
	protected override Type OffenderType => this.HashCodeBlockType;

	/// <summary>
	/// Initializes a new <see cref="HashCodeScriptException"/> instance with the specified offending object, script
	/// statement, block type, inner exception, and injected variables.
	/// </summary>
	/// <param name="offender">The object whose hash code the script block was calculating, or <see langword="null"/>.</param>
	/// <param name="scriptStatement">The script statement that failed, or <see langword="null"/> if it isn't known.</param>
	/// <param name="blockType">
	/// The type of the object whose hash code the script block was calculating, or <see langword="null"/> for
	/// <see cref="object"/>.
	/// </param>
	/// <param name="innerException">The exception that caused this one, or <see langword="null"/>.</param>
	/// <param name="injectedVariables">
	/// The variables that were injected into the script block's scope, or <see langword="null"/> for none.
	/// </param>
	public HashCodeScriptException(object? offender, string? scriptStatement, Type? blockType, Exception? innerException, IReadOnlyList<PSVariable>? injectedVariables)
		: base(DEF_MSG, offender, scriptStatement, innerException, injectedVariables)
	{
		this.HashCodeBlockType = blockType ?? typeof(object);
	}
	/// <summary>
	/// Initializes a new <see cref="HashCodeScriptException"/> instance with the specified offending object, script
	/// statement, block type, inner PowerShell runtime exception, and injected variables.
	/// </summary>
	/// <remarks>
	/// The new exception reuses the error record of <paramref name="runtimeException"/>.
	/// </remarks>
	/// <param name="offender">The object whose hash code the script block was calculating, or <see langword="null"/>.</param>
	/// <param name="scriptStatement">The script statement that failed, or <see langword="null"/> if it isn't known.</param>
	/// <param name="blockType">
	/// The type of the object whose hash code the script block was calculating, or <see langword="null"/> for
	/// <see cref="object"/>.
	/// </param>
	/// <param name="runtimeException">The PowerShell runtime exception that caused this one.</param>
	/// <param name="injectedVariables">
	/// The variables that were injected into the script block's scope, or <see langword="null"/> for none.
	/// </param>
	private HashCodeScriptException(object? offender, string? scriptStatement, Type? blockType, RuntimeException runtimeException, IReadOnlyList<PSVariable>? injectedVariables)
		: base(DEF_MSG, offender, scriptStatement, runtimeException, injectedVariables)
	{
		this.HashCodeBlockType = blockType ?? typeof(object);
	}

#if !NET8_0_OR_GREATER
	/// <summary>
	/// Initializes a new <see cref="HashCodeScriptException"/> instance with serialized data.
	/// </summary>
	/// <remarks>
	/// This constructor exists only in the .NET Framework and .NET Standard builds. When the data has no block type,
	/// <see cref="HashCodeBlockType"/> is <see cref="object"/>.
	/// </remarks>
	/// <param name="info">The object that holds the serialized exception data.</param>
	/// <param name="context">The contextual information about the source or destination.</param>
	private HashCodeScriptException(SerializationInfo info, StreamingContext context)
		: base(info, context)
	{
		this.HashCodeBlockType = (Type?)info.GetValue(nameof(this.HashCodeBlockType), typeof(Type))
			?? typeof(object);
	}

	/// <summary>
	/// Adds <see cref="HashCodeBlockType"/> and the base class's data to the serialized exception data.
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
		info.AddValue(nameof(this.HashCodeBlockType), this.HashCodeBlockType);

		base.GetObjectData(info, context);
	}
#endif

	/// <summary>
	/// Creates a <see cref="HashCodeScriptException"/> from the exception that a hash code script block threw, without
	/// injected variables.
	/// </summary>
	/// <remarks>
	/// This method behaves like
	/// <see cref="FromBlockException{T}(Exception, in T, IReadOnlyList{PSVariable})"/> with no injected variables.
	/// </remarks>
	/// <typeparam name="T">The type of the object whose hash code the script block was calculating.</typeparam>
	/// <param name="exception">The exception that the script block threw, or <see langword="null"/>.</param>
	/// <param name="obj">The object whose hash code the script block was calculating. It can be <see langword="null"/>.</param>
	/// <returns>The new exception, with <see cref="HashCodeBlockType"/> set to <typeparamref name="T"/>.</returns>
	public static HashCodeScriptException FromBlockException<T>(Exception? exception, [MaybeNull] in T obj)
	{
		return FromBlockException(exception, in obj, null);
	}
	/// <summary>
	/// Creates a <see cref="HashCodeScriptException"/> from the exception that a hash code script block threw.
	/// </summary>
	/// <remarks>
	/// When <paramref name="exception"/> is a <see cref="RuntimeException"/>, this method behaves like
	/// <see cref="FromBlockException{T}(RuntimeException, in T, IReadOnlyList{PSVariable})"/>.
	/// </remarks>
	/// <typeparam name="T">The type of the object whose hash code the script block was calculating.</typeparam>
	/// <param name="exception">The exception that the script block threw, or <see langword="null"/>.</param>
	/// <param name="obj">The object whose hash code the script block was calculating. It can be <see langword="null"/>.</param>
	/// <param name="injectedVariables">
	/// The variables that were injected into the script block's scope, or <see langword="null"/> for none.
	/// </param>
	/// <returns>The new exception, with <see cref="HashCodeBlockType"/> set to <typeparamref name="T"/>.</returns>
	public static HashCodeScriptException FromBlockException<T>(Exception? exception, [MaybeNull] in T obj, IReadOnlyList<PSVariable>? injectedVariables)
	{
		return FromBlockException(treatAsNonRuntime: false, exception, in obj, injectedVariables);
	}
	/// <summary>
	/// Creates a <see cref="HashCodeScriptException"/> from an exception, and treats it as a PowerShell runtime
	/// exception only when <paramref name="treatAsNonRuntime"/> is <see langword="false"/>.
	/// </summary>
	/// <typeparam name="T">The type of the object whose hash code the script block was calculating.</typeparam>
	/// <param name="treatAsNonRuntime">
	/// <see langword="true"/> to wrap <paramref name="exception"/> without reading an error record from it;
	/// otherwise, <see langword="false"/>.
	/// </param>
	/// <param name="exception">The exception that the script block threw, or <see langword="null"/>.</param>
	/// <param name="obj">The object whose hash code the script block was calculating.</param>
	/// <param name="injectedVariables">
	/// The variables that were injected into the script block's scope, or <see langword="null"/> for none.
	/// </param>
	/// <returns>The new exception.</returns>
	private static HashCodeScriptException FromBlockException<T>(bool treatAsNonRuntime, Exception? exception, [MaybeNull] in T obj, IReadOnlyList<PSVariable>? injectedVariables)
	{
		if (!treatAsNonRuntime && exception is RuntimeException runtime)
		{
			return FromBlockException(runtimeException: runtime, in obj, injectedVariables);
		}

		return new HashCodeScriptException(obj, null, typeof(T), innerException: exception, injectedVariables);
	}
	/// <summary>
	/// Creates a <see cref="HashCodeScriptException"/> from the PowerShell runtime exception that a hash code script
	/// block threw.
	/// </summary>
	/// <remarks>
	/// When <paramref name="runtimeException"/> has an error record with invocation information, the new exception
	/// reuses that error record and records the failing statement. Otherwise, it wraps
	/// <paramref name="runtimeException"/> like any other exception, without a statement.
	/// </remarks>
	/// <typeparam name="T">The type of the object whose hash code the script block was calculating.</typeparam>
	/// <param name="runtimeException">The PowerShell runtime exception that the script block threw.</param>
	/// <param name="obj">The object whose hash code the script block was calculating. It can be <see langword="null"/>.</param>
	/// <param name="injectedVariables">
	/// The variables that were injected into the script block's scope, or <see langword="null"/> for none.
	/// </param>
	/// <returns>The new exception, with <see cref="HashCodeBlockType"/> set to <typeparamref name="T"/>.</returns>
	public static HashCodeScriptException FromBlockException<T>(RuntimeException runtimeException, [MaybeNull] in T obj, IReadOnlyList<PSVariable>? injectedVariables)
	{
		if (runtimeException.ErrorRecord is null || runtimeException.ErrorRecord.InvocationInfo is null)
		{
			return FromBlockException(treatAsNonRuntime: true, exception: runtimeException, in obj, injectedVariables);
		}

		string statement = GetScriptStatement(runtimeException.ErrorRecord.InvocationInfo);

		return new HashCodeScriptException(obj, statement, typeof(T), runtimeException: runtimeException, injectedVariables);
	}
}
