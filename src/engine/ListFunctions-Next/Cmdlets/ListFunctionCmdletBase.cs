using ListFunctions.Components;
using ListFunctions.Exceptions;
using ListFunctions.Extensions;
using System.Management.Automation.Internal;

#nullable enable

namespace ListFunctions.Cmdlets;

/// <summary>
/// Provides a base class for PowerShell cmdlets that implement list-like functions with custom processing and error
/// handling logic.
/// </summary>
/// <remarks>This abstract class is intended to be inherited by cmdlets that require structured processing
/// phases (begin, process, end) and custom error management. It enforces a processing workflow and provides utility
/// methods for error preference retrieval and type conversion. Derived classes should override the core processing
/// methods to implement specific cmdlet behavior.</remarks>
public abstract class ListFunctionCmdletBase : PSCmdlet
{
	protected const string WITH_CUSTOM_EQUALITY = "WithCustomEquality";
	private const string PREFERENCE = "Preference";
	protected const string ERROR_ACTION = "ErrorAction";
	protected const string ERROR_ACTION_PREFERENCE = ERROR_ACTION + PREFERENCE;
	private const string STOP_UPSTREAM_TYPE = "System.Management.Automation.StopUpstreamCommandsException";

#if NET10_0_OR_GREATER
	/// <summary>
	/// Records that the running PowerShell can't create the exception that stops upstream commands.
	/// </summary>
	private static bool _cannotStopUpstream;
#else
	/// <summary>
	/// Holds the constructor of the exception that stops upstream commands, or <see langword="null"/> when the running
	/// PowerShell doesn't define it.
	/// </summary>
	private static readonly ConstructorInfo? s_stopUpstreamCtor = typeof(PSCmdlet).Assembly
		.GetType(STOP_UPSTREAM_TYPE, throwOnError: false)
		?.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, binder: null, [typeof(InternalCommand)], modifiers: null);
#endif

	private CmdletRunState _state;

	/// <summary>
	/// Gets a value that indicates whether the cmdlet is being requested to stop.
	/// </summary>
	/// <remarks>
	/// This helper property checks the base <c>Stopping</c> flag and, when available,
	/// the pipeline cancellation token to determine whether processing should halt. It centralizes the
	/// stopping logic so callers can check a single property.
	/// </remarks>
	/// <value><see langword="true"/> if the cmdlet should stop; otherwise, <see langword="false"/>.</value>
	[SuppressMessage("Style", "IDE0025", Justification = "Code includes conditional compilation")]
	private bool IsStopping
	{
		get
		{
			return this.Stopping
#if NET10_0_OR_GREATER
						 || this.PipelineStopToken.IsCancellationRequested
#endif
					 ;
		}
	}

	/// <summary>
	/// Begins the cmdlet processing lifecycle. This method is sealed to enforce the
	/// framework-defined execution sequence and delegates work to <see cref="BeginCore"/>.
	/// </summary>
	/// <remarks>
	/// Derived classes should override <see cref="BeginCore"/> to participate in the begin phase.
	/// This method wraps the call and handles failures by recording state, performing cleanup, and
	/// reporting a terminating error.
	/// </remarks>
	protected sealed override void BeginProcessing()
	{
		try
		{
			this.BeginCore();
		}
		catch (Exception e)
		{
			_state = _state.With(CmdletRunFlags.BeginFailed);
			this.CleanupCore();
			this.ThrowTerminatingError(e.ToRecord(ErrorCategory.InvalidArgument));
		}
	}
	/// <summary>
	/// Executes the process-record phase for each input object. This method is sealed and delegates
	/// the actual work to <see cref="ProcessCore"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The method skips the record when the pipeline is stopping, or when an earlier record failed or ended processing.
	/// It converts exceptions into terminating errors after performing cleanup.
	/// </para>
	/// <para>
	/// When <see cref="ProcessCore"/> returns <see langword="false"/>, processing is complete. If the cmdlet receives
	/// pipeline input, the method runs <see cref="EndCore(CmdletRunState)"/> right away and then stops the commands
	/// that send the input, the way <c>Select-Object -First</c> does. Those commands don't run their end blocks. When
	/// the running PowerShell can't stop them, the cmdlet ignores its remaining input and runs
	/// <see cref="EndCore(CmdletRunState)"/> from <see cref="EndProcessing"/> as usual.
	/// </para>
	/// </remarks>
	protected sealed override void ProcessRecord()
	{
		if (this.IsStopping)
		{
			_state = _state.With(CmdletRunFlags.IsStopping);
			return;
		}

		if (_state.ShouldSkipProcess)
		{
			return;
		}

		bool keepGoing;
		try
		{
			keepGoing = this.ProcessCore();
		}
		catch (Exception e)
		{
			_state = _state.With(CmdletRunFlags.ProcessFailed);
			this.CleanupCore();
			this.ThrowTerminatingError(e.ToRecord(ErrorCategory.NotSpecified));
			return;
		}

		if (!keepGoing)
		{
			_state = _state.With(CmdletRunFlags.FoundMatch);
			this.StopUpstreamCommands();
		}
	}
	/// <summary>
	/// Completes the cmdlet processing lifecycle and invokes the end-phase handler.
	/// </summary>
	/// <remarks>
	/// The method calls <see cref="EndCore(CmdletRunState)"/> to allow derived classes to finalize
	/// work and always invokes <see cref="CleanupCore"/> in a finally block to ensure cleanup runs.
	/// It does nothing when the end phase already ran because the cmdlet stopped its upstream commands.
	/// </remarks>
	protected sealed override void EndProcessing()
	{
		if (_state.Ended)
		{
			return;
		}

		_state = _state.With(CmdletRunFlags.Ended);
		try
		{
			this.EndCore(_state);
		}
		finally
		{
			this.CleanupCore();
		}
	}
	/// <summary>
	/// When overridden in a derived class, performs provider-specific logic required to begin cmdlet processing or
	/// operations.
	/// </summary>
	/// <remarks>Override this method in a subclass to implement cmdlet behavior that should occur at
	/// the start of processing. The base implementation does nothing.</remarks>
	protected virtual void BeginCore()
	{
	}
	/// <summary>
	/// When implemented in a derived class, performs the core processing logic for the cmdlet.
	/// </summary>
	/// <returns><see langword="true"/> to continue processing; <see langword="false"/> to stop processing.</returns>
	protected abstract bool ProcessCore();
	/// <param name="wantsToStop">true to request that the operation is requesting to stop; otherwise, false.</param>
	/// <summary>
	/// Performs custom logic when ending cmdlet processing, optionally indicating whether the operation should stop.
	/// </summary>
	/// <remarks>Override this method in a derived class to implement specific behavior that should
	/// occur when the operation ends. The base implementation does nothing.</remarks>
	/// <param name="state">The current state of the cmdlet run, including flags indicating processing outcomes.</param>
	protected virtual void EndCore(CmdletRunState state)
	{
	}

	/// <summary>
	/// Calls <see cref="Cleanup"/> and writes the message of any exception it throws to the debug output.
	/// </summary>
	/// <remarks>
	/// This private helper centralizes the cleanup call so callers can rely on consistent exception
	/// propagation and diagnostic reporting. When <see cref="Cleanup"/> throws, the method writes the
	/// exception's message with <see cref="Debug.WriteLine(string)"/> and rethrows the original exception.
	/// </remarks>
	private void CleanupCore()
	{
		try
		{
			this.Cleanup();
		}
		catch (Exception e)
		{
			Debug.WriteLine(e.Message);
			throw;
		}
	}
	/// <summary>
	/// Runs the end phase early and stops the commands that send pipeline input to this cmdlet.
	/// </summary>
	/// <remarks>
	/// <para>
	/// PowerShell doesn't call <see cref="EndProcessing"/> on a command that stops its upstream commands, so this
	/// method calls <see cref="EndCore(CmdletRunState)"/> and <see cref="CleanupCore"/> itself before it throws. It
	/// sets <see cref="CmdletRunFlags.Ended"/> first, so the end phase can't run twice.
	/// </para>
	/// <para>
	/// The method returns without doing anything when the cmdlet has no pipeline input, or when the running PowerShell
	/// can't create the exception. In both cases, <see cref="EndProcessing"/> runs the end phase as usual.
	/// </para>
	/// </remarks>
	/// <exception cref="FlowControlException">Thrown to stop the upstream commands. PowerShell handles it without reporting an error.</exception>
	private void StopUpstreamCommands()
	{
		if (!this.MyInvocation.ExpectingInput || !TryCreateStopUpstreamException(this, out Exception? stop))
		{
			return;
		}

		_state = _state.With(CmdletRunFlags.Ended);
		try
		{
			this.EndCore(_state);
		}
		finally
		{
			this.CleanupCore();
		}

		throw stop;
	}
	/// <summary>
	/// Creates the exception that PowerShell uses to stop the commands upstream of a command.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the exception that <c>Select-Object -First</c> throws. Its type is internal to PowerShell, so the method
	/// creates it through an unsafe accessor on .NET 10 and through reflection on .NET Framework.
	/// </para>
	/// <para>
	/// On .NET 10, the method remembers when the type or its constructor is missing, and later calls return
	/// <see langword="false"/> without trying again.
	/// </para>
	/// </remarks>
	/// <param name="requestingCommand">The command whose upstream commands are stopped.</param>
	/// <param name="exception">When this method returns <see langword="true"/>, contains the exception to throw; otherwise, <see langword="null"/>.</param>
	/// <returns><see langword="true"/> if the exception was created; otherwise, <see langword="false"/>.</returns>
	private static bool TryCreateStopUpstreamException(InternalCommand requestingCommand, [NotNullWhen(true)] out Exception? exception)
	{
#if NET10_0_OR_GREATER
		if (_cannotStopUpstream)
		{
			exception = null;
			return false;
		}

		try
		{
			exception = CreateStopUpstreamException(requestingCommand) as Exception;
		}
		catch (Exception e) when (e is TypeLoadException or MissingMemberException)
		{
			_cannotStopUpstream = true;
			exception = null;
		}
#else
		exception = s_stopUpstreamCtor?.Invoke([requestingCommand]) as Exception;
#endif

		return exception is not null;
	}
#if NET10_0_OR_GREATER
	/// <summary>
	/// Calls the constructor of the exception that PowerShell uses to stop upstream commands.
	/// </summary>
	/// <remarks>
	/// The type name is assembly-qualified because the runtime resolves an unqualified name only in this assembly.
	/// </remarks>
	/// <param name="requestingCommand">The command whose upstream commands are stopped.</param>
	/// <returns>The new exception.</returns>
	/// <exception cref="TypeLoadException">Thrown when the running PowerShell doesn't define the exception type.</exception>
	/// <exception cref="MissingMethodException">Thrown when the exception type has no constructor that takes an <see cref="InternalCommand"/>.</exception>
	[UnsafeAccessor(UnsafeAccessorKind.Constructor)]
	[return: UnsafeAccessorType(STOP_UPSTREAM_TYPE + ", System.Management.Automation")]
	private static extern object CreateStopUpstreamException(InternalCommand requestingCommand);
#endif
	/// <summary>
	/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
	/// </summary>
	/// <remarks>Override this method in a derived class to implement custom cleanup logic. This
	/// method is called to allow derived types to release resources or perform other cleanup operations before the
	/// object is disposed or finalized.</remarks>
	protected virtual void Cleanup()
	{
		// Override to implement custom cleanup logic
	}

	/// <summary>
	/// Retrieves the current error action preference to determine how errors are handled during command execution.
	/// </summary>
	/// <remarks>This method checks for an explicitly bound error action parameter before falling back
	/// to the session state's error action preference variable. Use this value to control error handling logic in
	/// derived cmdlets.</remarks>
	/// <returns>An <see cref="ActionPreference"/> value that specifies the error handling behavior. Returns the current error action
	/// preference if set; otherwise, returns <see cref="ActionPreference.Continue"/>.</returns>
	protected ActionPreference GetErrorPreference()
	{
		if (!this.MyInvocation.BoundParameters.TryGetValue(ERROR_ACTION, out object? errorObj))
		{
			errorObj = this.SessionState.PSVariable.GetValue(ERROR_ACTION_PREFERENCE);
		}

		return errorObj is ActionPreference actionPref
			? actionPref
			: ActionPreference.Continue;
	}

	/// <summary>
	/// Attempts to convert the specified object to the given type.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The method converts with <see cref="LanguagePrimitives.ConvertTo(object, Type)"/>, so it follows PowerShell's
	/// conversion rules. A successful conversion can produce <see langword="null"/>. For example,
	/// <see langword="null"/> converts to 0 for <see cref="int"/> and to an empty string for <see cref="string"/>, but
	/// it stays <see langword="null"/> for <see cref="Nullable{T}"/> and for most other reference types, such as
	/// <see cref="Version"/>.
	/// </para>
	/// <para>
	/// When the conversion fails, the method writes a non-terminating error instead of throwing.
	/// </para>
	/// </remarks>
	/// <param name="item">The object to convert. This value can be <see langword="null"/>.</param>
	/// <param name="convertTo">The type to convert <paramref name="item"/> to.</param>
	/// <param name="result">When this method returns, contains the converted value, which can be <see langword="null"/>, if the conversion succeeds; otherwise, <see langword="null"/>.</param>
	/// <returns><see langword="true"/> if the conversion succeeds, even when <paramref name="result"/> is <see langword="null"/>; otherwise, <see langword="false"/>.</returns>
	protected bool TryConvertItem(object? item, Type convertTo, out object? result)
	{
		try
		{
			result = LanguagePrimitives.ConvertTo(item, convertTo);
			return true;
		}
		catch (PSInvalidCastException e)
		{
			this.WriteConversionError(e, item, convertTo);
			result = null;
			return false;
		}
	}

	/// <summary>
	/// Writes an error record for a failed type conversion, including details about the original exception, the
	/// target type, and the item that could not be converted.
	/// </summary>
	/// <remarks>
	/// This helper translates a <see cref="PSInvalidCastException"/> into an <see cref="LFInvalidCastException"/>
	/// that captures the attempted target type and the item value. It then writes a terminating/ non-terminating
	/// error record (depending on the caller's error handling) to the pipeline so callers and scripts can react
	/// to the conversion failure.
	/// </remarks>
	/// <param name="thrownException">The exception that was thrown during the type conversion attempt. Must not be null.</param>
	/// <param name="item">The object that failed to convert to the specified type. Can be null if the conversion was attempted on a
	/// null value.</param>
	/// <param name="convertToType">The target type to which the conversion was attempted. Must not be null.</param>
	protected void WriteConversionError(PSInvalidCastException thrownException, object? item, Type convertToType)
	{
		string errorId = thrownException.GetType().GetTypeName();
		ErrorCategory cat = ErrorCategory.InvalidType;

		var castEx = new LFInvalidCastException(thrownException, convertToType, item);

		this.WriteError(new ErrorRecord(
			exception: castEx,
			errorId: errorId,
			errorCategory: cat,
			targetObject: item));
	}
}
