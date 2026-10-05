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
/// Without <see cref="ComparingScript"/>, the element type defaults to <see cref="string"/>, and its elements compare
/// with <see cref="StringComparer.InvariantCultureIgnoreCase"/>. Other element types use
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
/// Elements that compare as equal are stored once. The set is written as a single object and is not enumerated into
/// the pipeline.
/// </para>
/// </remarks>
[Cmdlet(VerbsCommon.New, "SortedSet", DefaultParameterSetName = "None")]
[OutputType(typeof(SortedSet<object>))]
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
	[PSDefaultValue(Help = "[string], or [object] with -ComparingScript")]
	[Alias("Type")]
	public Type GenericType { get; set; } = null!;

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
	/// Resolves the element type and creates the sorted set with it.
	/// </summary>
	/// <remarks>
	/// With <see cref="ComparingScript"/>, the element type defaults to <see cref="object"/>, and the set compares its
	/// elements with the script block. Without it, the element type defaults to <see cref="string"/>, and the set uses
	/// the element type's default order, which the method checks for before it creates the set.
	/// </remarks>
	/// <exception cref="ArgumentException">
	/// Thrown when <see cref="ComparingScript"/> isn't supplied and <see cref="GenericType"/> has no consistent default
	/// order.
	/// </exception>
	protected override void BeginCore()
	{
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
		}

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

