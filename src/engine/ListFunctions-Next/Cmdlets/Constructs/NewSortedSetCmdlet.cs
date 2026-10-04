using ListFunctions.Components;
using ListFunctions.Extensions;
using ListFunctions.Modern;
using ListFunctions.Modern.Constructors;
using ListFunctions.Modern.Variables;
using ListFunctions.Validation;

#nullable enable

namespace ListFunctions.Cmdlets.Constructs;

/// <summary>
/// Creates a new <see cref="SortedSet{T}"/> and optionally adds elements to it from the pipeline.
/// </summary>
/// <remarks>
/// <para>
/// Without <see cref="ComparingScript"/>, a set of <see cref="object"/> orders its elements by using PowerShell's
/// comparison rules, and a set of <see cref="string"/> uses <see cref="StringComparer.InvariantCultureIgnoreCase"/>.
/// In both cases, strings compare case-insensitively. Other element types use <see cref="Comparer{T}.Default"/>.
/// </para>
/// <para>
/// Elements that compare as equal are stored once. The set is written as a single object and is not enumerated into
/// the pipeline.
/// </para>
/// </remarks>
[Cmdlet(VerbsCommon.New, "SortedSet", DefaultParameterSetName = "None")]
[OutputType(typeof(SortedSet<>))]
public sealed class NewSortedSetCmdlet : ListFunctionCmdletBase
{
	private AddMethodInvoker _addMethod = null!;
	private object?[] _arr = null!;
	private SortingCollectorCtor _ctor = null!;
	private object _set = null!;

	/// <summary>
	/// Gets or sets the element type of the set.
	/// </summary>
	/// <remarks>
	/// The parameter accepts a <see cref="Type"/>, a type name, or a script block that contains a type literal such
	/// as <c>{ [int] }</c>.
	/// </remarks>
	/// <value>The element type. Defaults to <see cref="object"/>.</value>
	[Parameter(Mandatory = false, Position = 0)]
	[ArgumentToTypeTransform]
	[PSDefaultValue(Value = typeof(object))]
	[Alias("Type")]
	public Type GenericType { get => field ??= typeof(object); set; }

	/// <summary>
	/// Gets or sets the script block that compares two elements to determine their sort order.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The script block receives the two elements as <c>$x</c> and <c>$y</c>, as <c>$left</c> and <c>$right</c>, or
	/// as <c>$args[0]</c> and <c>$args[1]</c>. It must reference one variable for each element and return an integer
	/// that is less than zero, zero, or greater than zero, as <see cref="IComparer{T}.Compare(T, T)"/> does.
	/// </para>
	/// <para>
	/// Its first output is converted to an <see cref="int"/>. When it returns no value, <see langword="null"/>, or a
	/// value that can't be converted, the element being added isn't added, and the cmdlet writes a non-terminating
	/// error.
	/// </para>
	/// </remarks>
	/// <value>The comparison <see cref="ScriptBlock"/>.</value>
	[Parameter(Mandatory = true, ParameterSetName = WITH_CUSTOM_EQUALITY)]
	[ValidateScriptVariable(PSComparingVariable.X, PSComparingVariable.LEFT, PSThisVariable.FirstArg)]
	[ValidateScriptVariable(PSComparingVariable.Y, PSComparingVariable.RIGHT, PSThisVariable.SecondArg)]
	public ScriptBlock ComparingScript { get; set; } = null!;

	/// <summary>
	/// Gets or sets the elements to add to the set. The value is accepted from the pipeline.
	/// </summary>
	/// <remarks>
	/// Each pipeline object is one element, even when it's <see langword="null"/> or an array. An array passed to the
	/// parameter supplies its elements, and <see langword="null"/> supplies none. Each element is converted to
	/// <see cref="GenericType"/>. An element that cannot be converted produces a non-terminating error and is skipped,
	/// and <see langword="null"/> elements are skipped without an error.
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
	[Parameter(ParameterSetName = WITH_CUSTOM_EQUALITY)]
	[PSDefaultValue(Value = ActionPreference.Stop)]
	public ActionPreference ScriptBlockErrorAction { get; set; } = ActionPreference.Stop;

	/// <summary>
	/// Creates the sorted set with the configured element type and comparer.
	/// </summary>
	protected override void BeginCore()
	{
		IComparer? comparer = this.GetCustomComparer(this.GenericType);

		_ctor = new SortingCollectorCtor(this.GenericType, comparer);
		_set = _ctor.Construct();

	}
	/// <summary>
	/// Converts the elements of the current <see cref="InputObject"/> and adds them to the set.
	/// </summary>
	/// <remarks>
	/// An element that cannot be converted to <see cref="GenericType"/> produces a non-terminating error, the same one
	/// that <c>New-List</c> writes, and is skipped. <see langword="null"/> elements are skipped without an error. When
	/// adding an element throws, for example because <see cref="ComparingScript"/> fails, the method writes a
	/// non-terminating error for the exception that the set threw, and continues.
	/// </remarks>
	/// <returns>Always <see langword="true"/>, so all pipeline input is processed.</returns>
	protected override bool ProcessCore()
	{
		bool flag = true;
		object?[] elements = this.GetInputElements(this.InputObject);
		if (elements.Length == 0)
		{
			return flag;
		}

		_addMethod ??= new AddMethodInvoker(_ctor);
		_arr ??= new object?[1];

		foreach (object? item in elements)
		{
			if (item is null || !this.TryConvertItem(item, this.GenericType, out object? result))
			{
				continue;
			}

			_arr[0] = result;
			if (!_addMethod.TryInvoke(_set, _arr, false, out Exception? caught))
			{
				// The set's Add method runs through reflection, which wraps what it throws.
				Exception error = caught is TargetInvocationException { InnerException: { } inner } ? inner : caught;
				this.WriteError(error.ToRecord(ErrorCategory.InvalidType, item));
			}
		}

		return flag;
	}
	/// <summary>
	/// Writes the set to the pipeline as a single object.
	/// </summary>
	/// <param name="state">The run state of the cmdlet. When <see cref="CmdletRunState.FoundMatch"/> is <see langword="true"/>, nothing is written.</param>
	protected override void EndCore(CmdletRunState state)
	{
		if (!state.FoundMatch)
		{
			this.WriteObject(_set);
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
	/// Builds a comparer from <see cref="ComparingScript"/> when that parameter is bound.
	/// </summary>
	/// <param name="genericType">The element type of the set.</param>
	/// <returns>A script-based comparer for <paramref name="genericType"/>, or <see langword="null"/> when <see cref="ComparingScript"/> is not bound.</returns>
	private IComparer? GetCustomComparer(Type genericType)
	{
		return this.MyInvocation.BoundParameters.ContainsKey(nameof(this.ComparingScript))
			? ComparingBlock.Create(this.ComparingScript, genericType, this.GetAction())
			: null;
	}
}

