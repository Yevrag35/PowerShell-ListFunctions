using ListFunctions.Components;
using ListFunctions.Exceptions;
using ListFunctions.Extensions;
using ListFunctions.Modern;
using System.Management.Automation.Internal;
using System.Runtime.ExceptionServices;

#nullable enable

namespace ListFunctions.Cmdlets;

/// <summary>
/// Provides the base class for the ListFunctions cmdlets and runs their begin, process, and end phases in a fixed order.
/// </summary>
/// <remarks>
/// <para>
/// The class seals <see cref="BeginProcessing"/>, <see cref="ProcessRecord"/>, and <see cref="EndProcessing"/>. A
/// derived class overrides <see cref="BeginCore"/>, <see cref="ProcessCore"/>, and <see cref="Cleanup"/> instead. Only
/// classes in this assembly can override the end phase.
/// </para>
/// <para>
/// When <see cref="BeginCore"/> or <see cref="ProcessCore"/> throws, <see cref="Cleanup"/> runs, and the cmdlet ends.
/// PowerShell's own exceptions, such as an error from a script block that the cmdlet runs, reach PowerShell unchanged
/// from every phase, the way they do from <c>ForEach-Object</c>. Any other exception becomes a terminating error. When
/// <see cref="ProcessCore"/> returns <see langword="false"/>, the cmdlet processes no more pipeline input.
/// </para>
/// <para>
/// A derived cmdlet takes its input from the pipeline or from its <c>-InputObject</c> parameter, not both, and the
/// class rejects both together before <see cref="BeginCore"/> runs.
/// </para>
/// <para>
/// The class also provides helpers that create the filter for a condition script block, get the error action
/// preference, and convert items with PowerShell's conversion rules. Like other cmdlets, an instance isn't thread-safe.
/// </para>
/// </remarks>
public abstract class ListFunctionCmdletBase : PSCmdlet
{
	/// <summary>
	/// The name of the parameter set in which a cmdlet takes script blocks that decide whether two elements or keys are
	/// equal.
	/// </summary>
	protected const string WITH_CUSTOM_EQUALITY = "WithCustomEquality";
	/// <summary>
	/// The name of the dynamic <c>-CaseSensitive</c> parameter.
	/// </summary>
	private protected const string CASE_SENSE = "CaseSensitive";
	/// <summary>
	/// The name of the parameter that takes pipeline input, which every cmdlet in the module calls <c>InputObject</c>.
	/// </summary>
	private const string INPUT_OBJECT = "InputObject";
	/// <summary>
	/// The suffix that turns the name of a common parameter into the name of its preference variable.
	/// </summary>
	private const string PREFERENCE = "Preference";
	/// <summary>
	/// The name of the <c>-ErrorAction</c> common parameter.
	/// </summary>
	protected const string ERROR_ACTION = "ErrorAction";
	/// <summary>
	/// The name of the <c>$ErrorActionPreference</c> preference variable.
	/// </summary>
	/// <remarks>
	/// Derived cmdlets use it to set the error action preference in the scope where their script blocks run.
	/// </remarks>
	protected const string ERROR_ACTION_PREFERENCE = ERROR_ACTION + PREFERENCE;
	/// <summary>
	/// The full name of the PowerShell exception type that stops the commands upstream of a command.
	/// </summary>
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

	/// <summary>
	/// Holds the outcomes that this run has recorded so far.
	/// </summary>
	private CmdletRunState _state;
	/// <summary>
	/// Holds the single-element array that <see cref="GetInputElements(object)"/> returns for each pipeline object.
	/// </summary>
	private object?[]? _pipelineElement;

	/// <summary>
	/// Gets a value that indicates whether PowerShell is stopping the pipeline.
	/// </summary>
	/// <remarks>
	/// The property checks <see cref="Cmdlet.Stopping"/> and, on .NET 10, the cancellation token in
	/// <c>PipelineStopToken</c>, so callers can check a single property.
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
	/// Runs the begin phase by calling <see cref="BeginCore"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Before it calls <see cref="BeginCore"/>, the method makes sure that the cmdlet's input comes from one place. When
	/// the cmdlet receives pipeline input and its <c>-InputObject</c> parameter is bound on the command line too, the
	/// method throws an <see cref="ArgumentException"/>. PowerShell can't bind a pipeline object to a parameter that the
	/// command line already bound, so it would skip every pipeline object with an error, and the cmdlet would write its
	/// result for no input. The error ends the statement in the begin phase, before PowerShell binds any pipeline object,
	/// so it's the only error. The check applies even when the pipeline sends no objects.
	/// </para>
	/// <para>
	/// When the check or <see cref="BeginCore"/> throws, the method records that the begin phase failed and calls
	/// <see cref="Cleanup"/>. It then passes a <see cref="RuntimeException"/> or a <see cref="FlowControlException"/>
	/// to PowerShell unchanged, and reports any other exception as a terminating error in the
	/// <see cref="ErrorCategory.InvalidArgument"/> category, whose error ID is the full name of the exception's type.
	/// </para>
	/// </remarks>
	protected sealed override void BeginProcessing()
	{
		try
		{
			this.ThrowIfInputHasTwoSources();
			this.BeginCore();
		}
		catch (Exception e)
		{
			_state = _state.With(CmdletRunFlags.BeginFailed);
			this.CleanupCore();
			if (PassesThrough(e))
			{
				throw;
			}

			this.ThrowTerminatingError(e.ToRecord(ErrorCategory.InvalidArgument));
		}
	}
	/// <summary>
	/// Runs the process phase for the current input object by calling <see cref="ProcessCore"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The method skips the input object when PowerShell is stopping the pipeline, or when an earlier call failed or
	/// ended processing. When <see cref="ProcessCore"/> throws, the method records that the process phase failed and
	/// calls <see cref="Cleanup"/>. It then passes a <see cref="RuntimeException"/> or a <see cref="FlowControlException"/>
	/// to PowerShell unchanged, and reports any other exception as a terminating error in the
	/// <see cref="ErrorCategory.NotSpecified"/> category.
	/// </para>
	/// <para>
	/// When <see cref="ProcessCore"/> returns <see langword="false"/>, processing is complete. If the cmdlet receives
	/// pipeline input, the method runs the end phase and <see cref="Cleanup"/> right away and then stops the commands
	/// that send the input, the way <c>Select-Object -First</c> does. Those commands don't run their end blocks. When the
	/// running PowerShell can't stop them, the cmdlet ignores its remaining input and runs the end phase from
	/// <see cref="EndProcessing"/> as usual.
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
			if (PassesThrough(e))
			{
				throw;
			}

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
	/// Runs the end phase, and then calls <see cref="Cleanup"/>.
	/// </summary>
	/// <remarks>
	/// <see cref="Cleanup"/> runs even when the end phase throws. The method does nothing when the end phase already ran
	/// because the cmdlet stopped the commands that send it pipeline input.
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
	/// When overridden in a derived class, prepares the cmdlet before it receives pipeline input.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The base class calls this method from <see cref="BeginProcessing"/>. Parameters that take pipeline input aren't
	/// bound yet.
	/// </para>
	/// <para>
	/// When this method throws, <see cref="Cleanup"/> runs, and the cmdlet processes no input. A
	/// <see cref="RuntimeException"/> or a <see cref="FlowControlException"/> reaches PowerShell unchanged, and any other
	/// exception becomes a terminating error. The base implementation does nothing.
	/// </para>
	/// </remarks>
	protected virtual void BeginCore()
	{
	}
	/// <summary>
	/// When implemented in a derived class, processes the current pipeline input object.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The base class calls this method once for each pipeline input object, or once when the cmdlet receives no
	/// pipeline input. It stops calling the method after the method returns <see langword="false"/> or throws, and
	/// while PowerShell is stopping the pipeline.
	/// </para>
	/// <para>
	/// When this method throws, <see cref="Cleanup"/> runs. A <see cref="RuntimeException"/> or a
	/// <see cref="FlowControlException"/> reaches PowerShell unchanged, and any other exception becomes a terminating
	/// error.
	/// </para>
	/// </remarks>
	/// <returns><see langword="true"/> to keep processing input; <see langword="false"/> to stop.</returns>
	protected abstract bool ProcessCore();
	/// <summary>
	/// When overridden in a derived class, finishes the cmdlet's work, for example by writing the collection it built.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The base class calls this method at most once. It usually runs from <see cref="EndProcessing"/>. When
	/// <see cref="ProcessCore"/> returns <see langword="false"/> and the cmdlet receives pipeline input, it runs right
	/// away instead, before the cmdlet stops the commands that send the input.
	/// </para>
	/// <para>
	/// <see cref="Cleanup"/> runs after this method, even when it throws. The base class passes an exception from this
	/// method to PowerShell unchanged, which turns any exception other than a <see cref="RuntimeException"/> or a
	/// <see cref="FlowControlException"/> into a terminating error. The base implementation does nothing.
	/// </para>
	/// </remarks>
	/// <param name="state">
	/// The outcomes that the run has recorded. <see cref="CmdletRunState.Ended"/> is always <see langword="true"/>, and
	/// <see cref="CmdletRunState.FoundMatch"/> is <see langword="true"/> when <see cref="ProcessCore"/> returned
	/// <see langword="false"/>.
	/// </param>
	private protected virtual void EndCore(CmdletRunState state)
	{
	}

	/// <summary>
	/// Calls <see cref="Cleanup"/> and, in Debug builds, writes the message of any exception it throws to the debug output.
	/// </summary>
	/// <remarks>
	/// The method centralizes the cleanup call. When <see cref="Cleanup"/> throws, the method writes the exception's
	/// message with <see cref="Debug.WriteLine(string)"/>, which only Debug builds compile, and rethrows the original
	/// exception.
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
	/// Determines whether the cmdlet passes the specified exception to PowerShell unchanged.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A <see cref="RuntimeException"/> carries a PowerShell error, such as an error from a script block that the cmdlet
	/// runs, and a <see cref="FlowControlException"/> carries a statement such as <c>break</c> out of the script block.
	/// PowerShell then handles them the way it does when they come from a <c>ForEach-Object</c> script block. For
	/// example, an error that a script block writes under <c>$ErrorActionPreference = 'Stop'</c> ends the whole script,
	/// a failed method call ends only the statement, and <c>break</c> leaves the enclosing loop.
	/// </para>
	/// <para>
	/// A <see cref="RuntimeException"/> also includes the <see cref="PipelineStoppedException"/> that
	/// <see cref="Cmdlet.ThrowTerminatingError(ErrorRecord)"/> throws, so an error that the cmdlet already reported isn't
	/// reported again.
	/// </para>
	/// <para>
	/// The base class applies this rule to exceptions from <see cref="BeginCore"/> and <see cref="ProcessCore"/>. A
	/// derived class that catches exceptions around the script blocks it runs can use the method to apply the same rule.
	/// </para>
	/// </remarks>
	/// <param name="exception">The exception to check. This value must not be <see langword="null"/>.</param>
	/// <returns>
	/// <see langword="true"/> if <paramref name="exception"/> is a <see cref="RuntimeException"/> or a
	/// <see cref="FlowControlException"/>; otherwise, <see langword="false"/>.
	/// </returns>
	protected static bool PassesThrough(Exception exception)
	{
		return exception is RuntimeException or FlowControlException;
	}
	/// <summary>
	/// Throws the specified exception again, with its original stack trace, when <see cref="PassesThrough(Exception)"/>
	/// accepts it.
	/// </summary>
	/// <remarks>
	/// A derived class that receives an exception as a value, such as the exception that a collection's <c>Add</c> method
	/// threw when the class called it through reflection, uses this method instead of a <see langword="throw"/> statement,
	/// which would replace the exception's stack trace. When the method returns, the exception doesn't pass through, and
	/// the caller can report it as a non-terminating error.
	/// </remarks>
	/// <param name="exception">The exception to check. This value must not be <see langword="null"/>.</param>
	/// <exception cref="RuntimeException">Thrown when <paramref name="exception"/> is a <see cref="RuntimeException"/>. The exception thrown is <paramref name="exception"/> itself.</exception>
	/// <exception cref="FlowControlException">Thrown when <paramref name="exception"/> is a <see cref="FlowControlException"/>. The exception thrown is <paramref name="exception"/> itself.</exception>
	private protected static void RethrowIfPassesThrough(Exception exception)
	{
		if (PassesThrough(exception))
		{
			ExceptionDispatchInfo.Capture(exception).Throw();
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
	/// When overridden in a derived class, releases the resources that the cmdlet holds for its run.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The base class calls this method once, after the end phase or after <see cref="BeginCore"/> or
	/// <see cref="ProcessCore"/> throws. It doesn't run when PowerShell stops the pipeline before the end phase, for
	/// example when the user presses Ctrl+C.
	/// </para>
	/// <para>
	/// An exception from this method propagates to PowerShell. When <see cref="BeginCore"/> or
	/// <see cref="ProcessCore"/> threw, it replaces the terminating error for that exception. The base implementation
	/// does nothing.
	/// </para>
	/// </remarks>
	protected virtual void Cleanup()
	{
		// Override to implement custom cleanup logic
	}

	/// <summary>
	/// Gets the error action preference that applies to this cmdlet.
	/// </summary>
	/// <remarks>
	/// The method returns the value of the <c>-ErrorAction</c> common parameter when it is bound. Otherwise, it returns
	/// the value of <c>$ErrorActionPreference</c> in the cmdlet's session state.
	/// </remarks>
	/// <returns>
	/// The error action preference, or <see cref="ActionPreference.Continue"/> when the value found isn't an
	/// <see cref="ActionPreference"/>.
	/// </returns>
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
	/// Creates the filter that tests elements with the specified condition script block and error action preference.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <see cref="ActionPreference.SilentlyContinue"/> and <see cref="ActionPreference.Ignore"/> would hide the errors of
	/// the condition, so for them the filter runs the condition with <c>$ErrorActionPreference</c> set to
	/// <see cref="ActionPreference.Stop"/> instead. The first error that the condition doesn't handle itself then ends the
	/// test of that element: the cmdlet writes the error's message as a warning, and the element doesn't satisfy the
	/// condition. An error that the condition handles, in a <c>try</c> block or with a command's own
	/// <c>-ErrorAction SilentlyContinue</c> or <c>-ErrorAction Ignore</c>, isn't a warning.
	/// </para>
	/// <para>
	/// For any other value, the filter runs the condition with <c>$ErrorActionPreference</c> set to that value, and the
	/// errors of the condition reach PowerShell unchanged.
	/// </para>
	/// </remarks>
	/// <param name="condition">The condition script block. This value must not be <see langword="null"/>.</param>
	/// <param name="errorAction">The value of the cmdlet's <c>-ScriptBlockErrorAction</c> parameter.</param>
	/// <returns>The new filter.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="condition"/> is null.</exception>
	private protected ScriptBlockFilter CreateConditionFilter(ScriptBlock condition, ActionPreference errorAction)
	{
		if (errorAction is ActionPreference.SilentlyContinue or ActionPreference.Ignore)
		{
			return new ScriptBlockFilter(condition, this.WriteConditionWarning, new PSVariable(ERROR_ACTION_PREFERENCE, ActionPreference.Stop));
		}

		return new ScriptBlockFilter(condition, new PSVariable(ERROR_ACTION_PREFERENCE, errorAction));
	}
	/// <summary>
	/// Writes the message of an error from a condition script block as a warning.
	/// </summary>
	/// <remarks>
	/// The message is the one that PowerShell shows for the error record, and the warning follows the cmdlet's
	/// <c>-WarningAction</c>. So <c>-WarningAction SilentlyContinue</c> hides it, and <c>-WarningAction Stop</c> turns it
	/// into an error that ends the script.
	/// </remarks>
	/// <param name="error">The error record of the error. This value must not be <see langword="null"/>.</param>
	private void WriteConditionWarning(ErrorRecord error)
	{
		this.WriteWarning(error.ToString());
	}

	/// <summary>
	/// Throws when the cmdlet receives pipeline input and its <c>-InputObject</c> parameter is bound on the command line
	/// too.
	/// </summary>
	/// <remarks>
	/// Both are known in the begin phase. <see cref="InvocationInfo.ExpectingInput"/> is <see langword="true"/> whenever
	/// the cmdlet isn't first in its pipeline, and <see cref="InvocationInfo.BoundParameters"/> already holds the
	/// parameters that the command line bound.
	/// </remarks>
	/// <exception cref="ArgumentException">Thrown when the cmdlet receives pipeline input and <c>-InputObject</c> is bound.</exception>
	private void ThrowIfInputHasTwoSources()
	{
		if (this.MyInvocation.ExpectingInput && this.MyInvocation.BoundParameters.ContainsKey(INPUT_OBJECT))
		{
			throw new ArgumentException(
				$"Cannot use -{INPUT_OBJECT} and pipeline input together, because both supply the command's input. Pipe "
				+ $"the input, or pass it to -{INPUT_OBJECT}, but not both.");
		}
	}

	/// <summary>
	/// Returns the elements of the specified input object, which is the value of a derived cmdlet's pipeline input
	/// parameter.
	/// </summary>
	/// <remarks>
	/// <para>
	/// When the cmdlet receives pipeline input, each pipeline object is one element, even when it's
	/// <see langword="null"/> or a collection, the same way <c>ForEach-Object</c> sees it. The method removes the
	/// <see cref="PSObject"/> that PowerShell wraps around the object, unless the object is a custom object such as one
	/// that <c>[pscustomobject]@{}</c> creates.
	/// </para>
	/// <para>
	/// Otherwise, the input object is the argument of the parameter. A list, such as an array, supplies its elements,
	/// and <see langword="null"/> supplies none. Any other argument is one element, as it is, including a string, a
	/// dictionary, and a collection that isn't a list. These are the elements that PowerShell binds to a parameter of
	/// type <see cref="object"/>[].
	/// </para>
	/// <para>
	/// For pipeline input, the method returns the same array on every call. Don't keep the array or change it.
	/// </para>
	/// </remarks>
	/// <param name="inputObject">The value of the cmdlet's pipeline input parameter, or <see langword="null"/>.</param>
	/// <returns>The elements of <paramref name="inputObject"/>. The array can be empty.</returns>
	protected object?[] GetInputElements(object? inputObject)
	{
		if (this.MyInvocation.ExpectingInput)
		{
			object?[] element = _pipelineElement ??= new object?[1];
			element[0] = inputObject.GetBaseObject();
			return element;
		}

		return inputObject.GetBaseObject() switch
		{
			null => [],
			object[] array => array,
			IList list => CopyToArray(list),
			_ => [inputObject],
		};
	}
	/// <summary>
	/// Copies the elements of the specified list into a new array.
	/// </summary>
	/// <param name="list">The list to copy. This value must not be <see langword="null"/>.</param>
	/// <returns>A new array that holds the elements of <paramref name="list"/> in order.</returns>
	private static object?[] CopyToArray(IList list)
	{
		object?[] array = new object?[list.Count];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = list[i];
		}

		return array;
	}
	/// <summary>
	/// Returns the elements to search in the specified input object, which is the value of the pipeline input parameter
	/// of Find-Index or Find-LastIndex.
	/// </summary>
	/// <remarks>
	/// <para>
	/// When the cmdlet receives pipeline input, the method returns what <see cref="GetInputElements(object)"/> returns,
	/// so each pipeline object is one element.
	/// </para>
	/// <para>
	/// Otherwise, the input object is the argument of the parameter, and unlike in
	/// <see cref="GetInputElements(object)"/>, it supplies the elements that piping it would send. The method enumerates
	/// it with <see cref="LanguagePrimitives.GetEnumerator(object)"/>, so a collection, such as an array, a list, or a set,
	/// supplies its elements, and so does an enumerator. <see langword="null"/> supplies none. Any other argument is one
	/// element, as it is in the pipeline, and the method writes a warning that says so. A dictionary's warning suggests the
	/// dictionary's <c>GetEnumerator()</c> method. A string is one element without a warning, because PowerShell never
	/// treats a string as a collection.
	/// </para>
	/// <para>
	/// The method removes the <see cref="PSObject"/> that PowerShell wraps around each element, unless the element is a
	/// custom object such as one that <c>[pscustomobject]@{}</c> creates. For the argument of the parameter, it returns a
	/// new array.
	/// </para>
	/// </remarks>
	/// <param name="inputObject">The value of the cmdlet's pipeline input parameter, or <see langword="null"/>.</param>
	/// <returns>The elements of <paramref name="inputObject"/>. The array can be empty.</returns>
	private protected object?[] GetSearchElements(object? inputObject)
	{
		if (this.MyInvocation.ExpectingInput)
		{
			return this.GetInputElements(inputObject);
		}

		object? baseObject = inputObject.GetBaseObject();
		if (baseObject is null)
		{
			return [];
		}

		IEnumerator? enumerator = LanguagePrimitives.GetEnumerator(baseObject);
		if (enumerator is null)
		{
			if (baseObject is not string)
			{
				this.WriteOneElementWarning(baseObject);
			}

			return [baseObject];
		}

		try
		{
			return CopyElements(enumerator, (baseObject as ICollection)?.Count ?? 0);
		}
		finally
		{
			// An enumerator passed as the argument belongs to the caller, so only one created for a collection is disposed.
			if (!ReferenceEquals(enumerator, baseObject))
			{
				(enumerator as IDisposable)?.Dispose();
			}
		}
	}
	/// <summary>
	/// Copies the elements that the specified enumerator supplies into a new array, without the <see cref="PSObject"/>
	/// that PowerShell wraps around them.
	/// </summary>
	/// <remarks>
	/// A custom object, such as one that <c>[pscustomobject]@{}</c> creates, keeps its <see cref="PSObject"/>.
	/// </remarks>
	/// <param name="enumerator">The enumerator to read to its end. This value must not be <see langword="null"/>.</param>
	/// <param name="capacity">The number of elements to expect, or 0 when it isn't known.</param>
	/// <returns>A new array that holds the elements in the order that <paramref name="enumerator"/> supplies them.</returns>
	private static object?[] CopyElements(IEnumerator enumerator, int capacity)
	{
		List<object?> elements = new(capacity);
		while (enumerator.MoveNext())
		{
			elements.Add(enumerator.Current.GetBaseObject());
		}

		return elements.ToArray();
	}
	/// <summary>
	/// Writes a warning that the argument of <c>-InputObject</c> is one element, because it isn't a collection.
	/// </summary>
	/// <remarks>
	/// For a dictionary, the warning suggests the dictionary's <c>GetEnumerator()</c> method, whose enumerator supplies the
	/// entries. For any other value, it names the value's type, or for a custom object, the type of its base object.
	/// </remarks>
	/// <param name="argument">The argument that <c>-InputObject</c> received. This value must not be <see langword="null"/>.</param>
	private void WriteOneElementWarning(object argument)
	{
		if (argument is IDictionary)
		{
			this.WriteWarning($"The dictionary passed to -{INPUT_OBJECT} is one element, the same as when you pipe it. To pass "
				+ "its entries as elements, use its GetEnumerator() method, or pass its Keys or Values property.");
			return;
		}

		Type type = (argument is PSObject wrapper ? wrapper.BaseObject : argument).GetType();
		this.WriteWarning($"The value passed to -{INPUT_OBJECT} is one element, because a value of type '{type.GetTypeName()}' "
			+ "isn't a collection.");
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
	/// When the conversion fails, the method writes an error with
	/// <see cref="WriteConversionError(PSInvalidCastException, object, Type)"/> instead of throwing.
	/// </para>
	/// </remarks>
	/// <param name="item">The object to convert. This value can be <see langword="null"/>.</param>
	/// <param name="convertTo">The type to convert <paramref name="item"/> to. This value must not be <see langword="null"/>.</param>
	/// <param name="result">When this method returns, contains the converted value, which can be <see langword="null"/>, if the conversion succeeds; otherwise, <see langword="null"/>.</param>
	/// <returns><see langword="true"/> if the conversion succeeds, even when <paramref name="result"/> is <see langword="null"/>; otherwise, <see langword="false"/>.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="convertTo"/> is null.</exception>
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
	/// Writes an error for an item that can't be converted to the specified type.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The error record wraps an <see cref="LFInvalidCastException"/>, whose message names the item, its type, the
	/// target type, and the reason that the conversion failed. The record's error ID is the full name of the run-time
	/// type of <paramref name="thrownException"/>, its category is <see cref="ErrorCategory.InvalidType"/>, and its
	/// target object is <paramref name="item"/>.
	/// </para>
	/// <para>
	/// The error is non-terminating unless the error action preference makes PowerShell stop on it.
	/// </para>
	/// </remarks>
	/// <param name="thrownException">The exception that PowerShell threw when the conversion failed. This value must not be <see langword="null"/>.</param>
	/// <param name="item">The object that failed to convert, or <see langword="null"/> when the conversion started from a <see langword="null"/> value.</param>
	/// <param name="convertToType">The type that the conversion targeted. This value must not be <see langword="null"/>.</param>
	/// <exception cref="NullReferenceException">Thrown when <paramref name="thrownException"/> or <paramref name="convertToType"/> is null.</exception>
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
