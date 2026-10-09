using ListFunctions.Completion;
using ListFunctions.Components;
using ListFunctions.Extensions;
using ListFunctions.Internal;
using ListFunctions.Modern;
using ListFunctions.Modern.Variables;
using ListFunctions.Validation;

#nullable enable

namespace ListFunctions.Cmdlets.Constructs;

/// <summary>
/// Creates a new <see cref="SortedSet{T}"/> and optionally adds elements to it from the pipeline.
/// </summary>
/// <remarks>
/// <para>
/// Without <see cref="ComparingScript"/>, the element type defaults to <see cref="string"/>, and its elements compare
/// with <see cref="StringComparer.OrdinalIgnoreCase"/>, or with <see cref="StringComparer.Ordinal"/> when the dynamic
/// <c>-CaseSensitive</c> switch is set. The cmdlet offers the switch only for <see cref="string"/> elements, and
/// PowerShell rejects it with <see cref="ComparingScript"/>. Other element types use
/// <see cref="Comparer{T}.Default"/>, so they must have a consistent default order: they must implement
/// <see cref="IComparable{T}"/> of themselves, be enums, or be <see cref="Nullable{T}"/> of such a type. Any other
/// element type, <see cref="object"/> and <see cref="PSObject"/> included, is a terminating error before the cmdlet
/// reads any input.
/// </para>
/// <para>
/// With <see cref="ComparingScript"/>, the script block compares the elements, so any element type is accepted, and the
/// element type defaults to <see cref="object"/>.
/// </para>
/// <para>
/// Errors from <see cref="ComparingScript"/> reach PowerShell unchanged, the way they do from a <c>ForEach-Object</c>
/// script block, and the cmdlet then writes no set. When <see cref="ScriptBlockErrorAction"/> is
/// <see cref="ActionPreference.Stop"/>, the default, an error that the script block writes ends the script that runs the
/// cmdlet, as <c>-ErrorAction Stop</c> does. A <c>throw</c> does too unless the errors are suppressed. A failed method
/// call, and output that isn't an <see cref="int"/>, end only the statement, and <c>break</c> leaves the loop around the
/// cmdlet.
/// </para>
/// <para>
/// Elements that compare as equal are stored once. The set is written as a single object and is not enumerated into
/// the pipeline.
/// </para>
/// </remarks>
[Cmdlet(VerbsCommon.New, "SortedSet", DefaultParameterSetName = DEFAULT_ORDER)]
[OutputType(typeof(SortedSet<object>))]
public sealed class NewSortedSetCmdlet : ListFunctionCmdletBase, IDynamicParameters
{
	/// <summary>
	/// The name of the default parameter set, in which the elements sort in their type's default order.
	/// </summary>
	/// <remarks>
	/// The dynamic <c>-CaseSensitive</c> switch belongs only to this set, so it can't be combined with
	/// <see cref="ComparingScript"/>.
	/// </remarks>
	private const string DEFAULT_ORDER = "None";
	/// <summary>
	/// The name of the parameter set in which <see cref="ComparingScript"/> decides the sort order.
	/// </summary>
	private const string WITH_COMPARING_SCRIPT = "WithComparingScript";

	private RuntimeDefinedParameter? _caseSensitive;
	private SetWrapper _set = null!;

	/// <summary>
	/// Gets or sets the element type of the set.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The parameter accepts a <see cref="Type"/>, a type name, or a script block that contains a type literal such
	/// as <c>{ [int] }</c>.
	/// </para>
	/// <para>
	/// Without <see cref="ComparingScript"/>, the type must implement <see cref="IComparable{T}"/> of itself, be an
	/// enum, or be a <see cref="Nullable{T}"/> of such a type. With <see cref="ComparingScript"/>, any type is accepted.
	/// </para>
	/// </remarks>
	/// <value>
	/// The element type. Defaults to <see cref="string"/>, or to <see cref="object"/> when <see cref="ComparingScript"/>
	/// is supplied.
	/// </value>
	[Parameter(Mandatory = false, Position = 0)]
	[ArgumentToTypeTransform]
	[ArgumentCompleter(typeof(TypeNameCompleter))]
	[PSDefaultValue(Help = "[string], or [object] with -ComparingScript")]
	[Alias("Type")]
	public Type GenericType { get; set; } = null!;

	/// <summary>
	/// Gets or sets the script block that compares two elements to determine their sort order.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The script block receives the two elements as <c>$x</c> and <c>$y</c>, as <c>$left</c> and <c>$right</c>, and
	/// as its two arguments, in order: <c>$args[0]</c> and <c>$args[1]</c>, or the parameters of its <c>param()</c>
	/// block. It must reference one variable for each element and return an integer that is less than zero, zero, or
	/// greater than zero, as <see cref="IComparer{T}.Compare(T, T)"/> does. Parameter validation also rejects a script
	/// block that the cmdlet can't run, such as one that has a <c>begin</c> block.
	/// </para>
	/// <para>
	/// Its first output is converted to an <see cref="int"/>. When it returns no value, <see langword="null"/>, or a
	/// value that can't be converted, the error ends the statement that runs the cmdlet, as a failed method call does,
	/// and the cmdlet writes no set.
	/// </para>
	/// </remarks>
	/// <value>The comparison <see cref="ScriptBlock"/>.</value>
	[Parameter(Mandatory = true, ParameterSetName = WITH_COMPARING_SCRIPT)]
	[IsScriptBlock]
	[ValidateScriptVariable(PSComparingVariable.X, PSComparingVariable.LEFT, PSThisVariable.FirstArg)]
	[ValidateScriptVariable(PSComparingVariable.Y, PSComparingVariable.RIGHT, PSThisVariable.SecondArg)]
	public ScriptBlock ComparingScript { get; set; } = null!;

	/// <summary>
	/// Gets or sets the elements to add to the set. The value is accepted from the pipeline.
	/// </summary>
	/// <remarks>
	/// Each pipeline object is one element, even when it's <see langword="null"/> or an array. An array passed to the
	/// parameter supplies its elements, and <see langword="null"/> supplies none. The parameter can't be combined with
	/// pipeline input. Each element is converted to <see cref="GenericType"/>. An element that cannot be converted
	/// produces a non-terminating error and is skipped, and <see langword="null"/> elements are skipped without an error.
	/// </remarks>
	/// <value>The current pipeline object, or the argument of the parameter. The value can be <see langword="null"/>.</value>
	[Parameter(ValueFromPipeline = true)]
	public object? InputObject { get; set; }

	/// <summary>
	/// Gets or sets the error action preference applied while <see cref="ComparingScript"/> runs.
	/// </summary>
	/// <remarks>
	/// The value is assigned to <c>$ErrorActionPreference</c> in the script block's scope. It does not change the
	/// cmdlet's own <c>-ErrorAction</c> behavior.
	/// </remarks>
	/// <value>The error action preference for script block execution. Defaults to <see cref="ActionPreference.Stop"/>.</value>
	[Parameter(ParameterSetName = WITH_COMPARING_SCRIPT), Alias("ScriptErrorAction")]
	[PSDefaultValue(Value = ActionPreference.Stop)]
	public ActionPreference ScriptBlockErrorAction { get; set; } = ActionPreference.Stop;

	/// <summary>
	/// Returns the dynamic <c>-CaseSensitive</c> parameter when the set's elements are strings.
	/// </summary>
	/// <remarks>
	/// <para>
	/// PowerShell calls the method after it binds the other parameters on the command line, so <see cref="GenericType"/>
	/// holds the element type, if one was given. The method offers the switch when that type is <see cref="string"/>,
	/// and when no type was given, because <see cref="string"/> is then the element type unless
	/// <see cref="ComparingScript"/> is supplied.
	/// </para>
	/// <para>
	/// When the switch comes before a positional element type, as in <c>New-SortedSet -CaseSensitive [int]</c>,
	/// PowerShell can't bind the type by position until it knows that the switch takes no argument, so it binds the type
	/// after the method returns. <see cref="BeginCore"/> then rejects the switch for a type other than
	/// <see cref="string"/>.
	/// </para>
	/// <para>
	/// The switch belongs only to the default parameter set, so PowerShell rejects it when <see cref="ComparingScript"/>
	/// is supplied, because the script block decides the order then. The method creates the switch once and returns it
	/// on every later call.
	/// </para>
	/// </remarks>
	/// <returns>
	/// A <see cref="RuntimeDefinedParameterDictionary"/> that contains <c>-CaseSensitive</c>, or <see langword="null"/>
	/// when <see cref="GenericType"/> is a type other than <see cref="string"/>.
	/// </returns>
	public object? GetDynamicParameters()
	{
		if (this.GenericType is not null && !typeof(string).Equals(this.GenericType))
		{
			return null;
		}

		_caseSensitive ??= new RuntimeDefinedParameter(CASE_SENSE, typeof(SwitchParameter), new Collection<Attribute>()
		{
			new ParameterAttribute()
			{
				ParameterSetName = DEFAULT_ORDER,
			},
		});

		return new RuntimeDefinedParameterDictionary()
		{
			{ CASE_SENSE, _caseSensitive },
		};
	}

	/// <summary>
	/// Resolves the element type and creates the sorted set with it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// With <see cref="ComparingScript"/>, the element type defaults to <see cref="object"/>, and the set compares its
	/// elements with the script block. Without it, the element type defaults to <see cref="string"/>, and the set uses
	/// the element type's default order, which the method checks for before it creates the set. The order of
	/// <see cref="string"/> elements considers case when the dynamic <c>-CaseSensitive</c> switch is set.
	/// </para>
	/// <para>
	/// The method also sets the callbacks that write a non-terminating error for each element that can't be converted,
	/// and for each element that the set fails to add. Setting them once here means that pipeline input doesn't allocate
	/// a delegate for each record.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentException">
	/// Thrown when <see cref="ComparingScript"/> isn't supplied and <see cref="GenericType"/> has no consistent default
	/// order, or isn't <see cref="string"/> although the dynamic <c>-CaseSensitive</c> switch is set.
	/// </exception>
	protected override void BeginCore()
	{
		bool caseSensitive = LanguagePrimitives.IsTrue(_caseSensitive?.Value);
		IComparer? comparer = null;
		if (this.MyInvocation.BoundParameters.ContainsKey(nameof(this.ComparingScript)))
		{
			this.GenericType ??= typeof(object);
			comparer = ComparingBlock.Create(this.ComparingScript, this.GenericType, this.GetAction());
		}
		else
		{
			this.GenericType ??= typeof(string);
			if (!HasDefaultOrder(this.GenericType))
			{
				throw new ArgumentException(
					$"Cannot sort elements of type \"{this.GenericType.GetTypeName()}\" without -ComparingScript, because "
					+ "the type has no default sort order. Use -ComparingScript, or an element type that has a default sort "
					+ "order, such as [string], [int], [datetime], or an enum.",
					nameof(this.GenericType));
			}

			if (caseSensitive && !typeof(string).Equals(this.GenericType))
			{
				throw new ArgumentException(
					$"Cannot sort elements of type \"{this.GenericType.GetTypeName()}\" with -CaseSensitive, because the "
					+ "switch applies only to [string] elements.",
					CASE_SENSE);
			}
		}

		_set = SetWrapper.CreateSortedSet(this.GenericType, comparer, caseSensitive);
		_set.ConversionFailed = (item, exception) => this.WriteConversionError(exception, item, this.GenericType);
		_set.AddFailed = (item, exception) => this.WriteError(exception.ToRecord(ErrorCategory.InvalidType, item));
	}
	/// <summary>
	/// Converts the elements of the current <see cref="InputObject"/> and adds them to the set.
	/// </summary>
	/// <remarks>
	/// <para>
	/// An element that cannot be converted to <see cref="GenericType"/> produces a non-terminating error, the same one
	/// that <c>New-List</c> writes, and is skipped. <see langword="null"/> elements are skipped without an error.
	/// </para>
	/// <para>
	/// When adding an element throws a <see cref="RuntimeException"/> or a <see cref="FlowControlException"/>, such as
	/// an error from <see cref="ComparingScript"/>, including one for output that isn't an <see cref="int"/>, the
	/// exception reaches PowerShell unchanged, and the cmdlet writes no set. Any other exception, such as one from the
	/// element type's own comparison, produces a non-terminating error whose target is the element as it was before
	/// conversion, and the method goes on with the next one.
	/// </para>
	/// </remarks>
	/// <returns>Always <see langword="true"/>, so all pipeline input is processed.</returns>
	/// <exception cref="RuntimeException">Thrown when adding an element throws one, for example because <see cref="ComparingScript"/> fails.</exception>
	/// <exception cref="FlowControlException">Thrown when adding an element throws one, for example because <see cref="ComparingScript"/> runs <c>break</c>.</exception>
	protected override bool ProcessCore()
	{
		_set.AddRange(this.GetInputElements(this.InputObject));
		return true;
	}
	/// <summary>
	/// Writes the set to the pipeline as a single object.
	/// </summary>
	/// <param name="state">The run state of the cmdlet. When <see cref="CmdletRunState.FoundMatch"/> is <see langword="true"/>, nothing is written.</param>
	private protected override void EndCore(CmdletRunState state)
	{
		if (!state.FoundMatch)
		{
			this.WriteObject(_set.AsSet());
		}
	}

	/// <summary>
	/// Returns the variables to define in the scope of <see cref="ComparingScript"/>.
	/// </summary>
	/// <returns>A sequence that contains <c>$ErrorActionPreference</c> set to <see cref="ScriptBlockErrorAction"/>.</returns>
	private IEnumerable<PSVariable> GetAction()
	{
		yield return new PSVariable(ERROR_ACTION_PREFERENCE, this.ScriptBlockErrorAction);
	}
	/// <summary>
	/// Determines whether the specified element type has a consistent default sort order.
	/// </summary>
	/// <remarks>
	/// A type has one when it implements <see cref="IComparable{T}"/> of itself, is an enum, or is a
	/// <see cref="Nullable{T}"/> of a type that has one. <see cref="Comparer{T}.Default"/> orders these types by value.
	/// For any other type, it calls <see cref="IComparable.CompareTo(object)"/>, which isn't a consistent order when the
	/// elements' types differ, as they can in a set of <see cref="object"/> or <see cref="PSObject"/>.
	/// </remarks>
	/// <param name="type">The element type to check. This value must not be <see langword="null"/>.</param>
	/// <returns>
	/// <see langword="true"/> if <paramref name="type"/> has a consistent default sort order; otherwise,
	/// <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="type"/> can't be a generic type argument, such as a pointer type.
	/// </exception>
	private static bool HasDefaultOrder(Type type)
	{
		if (type.IsEnum)
		{
			return true;
		}

		if (Nullable.GetUnderlyingType(type) is Type underlyingType)
		{
			return HasDefaultOrder(underlyingType);
		}

		return typeof(IComparable<>).MakeGenericType(type).IsAssignableFrom(type);
	}
}

