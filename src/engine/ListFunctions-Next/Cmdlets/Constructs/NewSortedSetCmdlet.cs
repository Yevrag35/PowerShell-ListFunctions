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
	private object[] _arr = null!;
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
	/// The script block receives the two elements as <c>$x</c> and <c>$y</c>, as <c>$left</c> and <c>$right</c>, or
	/// as <c>$args[0]</c> and <c>$args[1]</c>. It must reference one variable for each element and return an integer
	/// that is less than zero, zero, or greater than zero, as <see cref="IComparer{T}.Compare(T, T)"/> does.
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
	/// Each element is converted to <see cref="GenericType"/>. <see langword="null"/> elements and elements that
	/// cannot be converted are skipped without an error.
	/// </remarks>
	/// <value>The elements to add, or <see langword="null"/> to create an empty set.</value>
	[Parameter(ValueFromPipeline = true)]
	public object[] InputObject { get; set; } = null!;

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
	/// Converts the elements of the current <see cref="InputObject"/> array and adds them to the set.
	/// </summary>
	/// <remarks>
	/// <see langword="null"/> elements and elements that cannot be converted to <see cref="GenericType"/> are skipped
	/// without an error. When adding an element throws, the method writes a non-terminating error and continues.
	/// </remarks>
	/// <returns>Always <see langword="true"/>, so all pipeline input is processed.</returns>
	protected override bool ProcessCore()
	{
		bool flag = true;
		if (this.InputObject is null || this.InputObject.Length == 0)
		{
			return flag;
		}

		_addMethod ??= new AddMethodInvoker(_ctor);
		_arr ??= new object[1];

		foreach (object? item in this.InputObject)
		{
			if (item is null || !LanguagePrimitives.TryConvertTo(item, this.GenericType, out object? result))
			{
				continue;
			}

			_arr[0] = result;
			if (!_addMethod.TryInvoke(_set, _arr, false, out Exception? caught))
			{
				this.WriteError(caught.ToRecord(ErrorCategory.InvalidType, item));
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

