using ListFunctions.Completion;
using ListFunctions.Extensions;
using ListFunctions.Internal;
using ListFunctions.Modern;
using ListFunctions.Modern.Variables;
using ListFunctions.Validation;

#nullable enable

namespace ListFunctions.Cmdlets.Constructs;

/// <summary>
/// Creates a new <see cref="HashSet{T}"/> and optionally adds elements to it from the pipeline.
/// </summary>
/// <remarks>
/// <para>
/// When <see cref="GenericType"/> is <see cref="string"/>, elements compare with
/// <see cref="StringComparer.OrdinalIgnoreCase"/>, or with <see cref="StringComparer.Ordinal"/> when
/// <c>-CaseSensitive</c> is set. When it's <see cref="object"/>, elements compare with the same comparer: two strings
/// the same way as <see cref="string"/> elements, and any other two elements with their own
/// <see cref="object.Equals(object)"/> and <see cref="object.GetHashCode"/> methods, so <c>"1"</c> and <c>1</c> are
/// different elements. Other element types use <see cref="EqualityComparer{T}.Default"/>.
/// <see cref="EqualityScript"/> and <see cref="HashCodeScript"/> replace the default comparison with PowerShell
/// script blocks.
/// </para>
/// <para>
/// Errors from <see cref="EqualityScript"/> and <see cref="HashCodeScript"/> reach PowerShell unchanged, the way they do
/// from a <c>ForEach-Object</c> script block, and the cmdlet then writes no set. When
/// <see cref="ScriptBlockErrorAction"/> is <see cref="ActionPreference.Stop"/>, the default, an error that a script
/// block writes ends the script that runs the cmdlet, as <c>-ErrorAction Stop</c> does. A <c>throw</c> does too unless
/// the errors are suppressed. A failed method call, and output of <see cref="HashCodeScript"/> that isn't a hash code,
/// end only the statement, and <c>break</c> leaves the loop around the cmdlet.
/// </para>
/// <para>
/// Duplicate elements are ignored. The set is written as a single object and is not enumerated into the pipeline.
/// </para>
/// </remarks>
[Cmdlet(VerbsCommon.New, "HashSet", DefaultParameterSetName = SPECIFIED_TYPE)]
[OutputType(typeof(HashSet<object>))]
public sealed class NewHashSetCmdlet : EqualityConstructingCmdlet<object>, IDynamicParameters
{
	private const string DYN_PSET_NAME = "StringSet";
	private const string SPECIFIED_TYPE = "SpecifiedType";

	private SetWrapper _set = null!;

	/// <inheritdoc/>
	protected override string CaseSensitiveParameterSetName => DYN_PSET_NAME;

	/// <summary>
	/// Gets or sets the initial capacity requested for the set.
	/// </summary>
	/// <remarks>
	/// The set is created with room for this many elements, so it doesn't have to grow until it holds more.
	/// </remarks>
	/// <value>The requested initial capacity, from 0 through <see cref="int.MaxValue"/>. Defaults to 0.</value>
	[Parameter, Alias("Size"), ValidateRange(0, int.MaxValue), PSDefaultValue(Value = 0)]
	public override int Capacity { get; set; }

	/// <summary>
	/// Gets or sets the element type of the set.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The parameter accepts a <see cref="Type"/>, a type name, or a script block that contains a type literal such
	/// as <c>{ [int] }</c>.
	/// </para>
	/// <para>
	/// The parameter also belongs to the parameter set of <c>-CaseSensitive</c>, so the two can be combined when the
	/// element type is <see cref="object"/> or <see cref="string"/>. It can be combined with <see cref="EqualityScript"/>
	/// and <see cref="HashCodeScript"/> too, for any element type, and the script blocks then receive values of that type.
	/// </para>
	/// </remarks>
	/// <value>The element type. Defaults to <see cref="object"/>.</value>
	[Parameter(Mandatory = false, Position = 0, ParameterSetName = SPECIFIED_TYPE)]
	[Parameter(Mandatory = false, Position = 0, ParameterSetName = DYN_PSET_NAME)]
	[Parameter(Mandatory = false, Position = 0, ParameterSetName = WITH_CUSTOM_EQUALITY)]
	[ArgumentToTypeTransform, Alias("Type")]
	[ArgumentCompleter(typeof(TypeNameCompleter))]
	[PSDefaultValue(Value = typeof(object))]
	public Type GenericType { get; set; } = null!;

	/// <summary>
	/// Gets or sets the elements to add to the set. The value is accepted from the pipeline.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Each pipeline object is one element, even when it's <see langword="null"/> or an array. An array passed to the
	/// parameter supplies its elements, and <see langword="null"/> supplies none. The parameter can't be combined with
	/// pipeline input.
	/// </para>
	/// <para>
	/// For a typed set, each element is converted to <see cref="GenericType"/>. An element that cannot be converted
	/// produces the non-terminating error that <c>New-List</c> writes and is skipped, and <see langword="null"/>
	/// elements are skipped without an error. A set of <see cref="object"/> adds each element as it is, including
	/// <see langword="null"/>.
	/// </para>
	/// </remarks>
	/// <value>The current pipeline object, or the argument of the parameter. The value can be <see langword="null"/>.</value>
	[Parameter(ValueFromPipeline = true)]
	public object? InputObject { get; set; }

	/// <summary>
	/// Gets or sets the script block that determines whether two elements are equal.
	/// </summary>
	/// <remarks>
	/// The script block receives the two elements as <c>$x</c> and <c>$y</c>, as <c>$left</c> and <c>$right</c>, and
	/// as its two arguments, in order: <c>$args[0]</c> and <c>$args[1]</c>, or the parameters of its <c>param()</c>
	/// block. It must reference one variable for each element. Parameter validation also rejects a script block that the
	/// cmdlet can't run, such as one that has a <c>begin</c> block. Its output is converted to a <see cref="bool"/> by
	/// using PowerShell's truthiness rules.
	/// </remarks>
	/// <value>The element equality <see cref="ScriptBlock"/>.</value>
	[Parameter(Mandatory = true, ParameterSetName = WITH_CUSTOM_EQUALITY), IsScriptBlock]
	[ValidateScriptVariable(PSComparingVariable.X, PSComparingVariable.LEFT, PSThisVariable.FirstArg)]
	[ValidateScriptVariable(PSComparingVariable.Y, PSComparingVariable.RIGHT, PSThisVariable.SecondArg)]
	public ScriptBlock EqualityScript { get; set; } = null!;

	/// <summary>
	/// Gets or sets the script block that computes the hash code of an element.
	/// </summary>
	/// <remarks>
	/// The script block receives the element as <c>$_</c>, <c>$this</c>, and <c>$PSItem</c>, and as its first argument:
	/// <c>$args[0]</c>, or the first parameter of its <c>param()</c> block. It must reference at least one of them.
	/// Parameter validation also rejects a script block that the cmdlet can't run, such as one that has a <c>begin</c>
	/// block. Its first output is converted to an <see cref="int"/>. Elements that <see cref="EqualityScript"/> considers
	/// equal must produce the same hash code.
	/// </remarks>
	/// <value>The element hash code <see cref="ScriptBlock"/>.</value>
	[Parameter(Mandatory = true, ParameterSetName = WITH_CUSTOM_EQUALITY)]
	[IsScriptBlock, ValidateScriptVariable(PSThisVariable.Underscore, PSThisVariable.This, PSThisVariable.PSItem, PSThisVariable.FirstArg)]
	public ScriptBlock HashCodeScript { get; set; } = null!;

	/// <summary>
	/// Gets or sets the error action preference applied while <see cref="EqualityScript"/> and
	/// <see cref="HashCodeScript"/> run.
	/// </summary>
	/// <remarks>
	/// The value is assigned to <c>$ErrorActionPreference</c> in the script blocks' scope. It does not change the
	/// cmdlet's own <c>-ErrorAction</c> behavior.
	/// </remarks>
	/// <value>The error action preference for script block execution. Defaults to <see cref="ActionPreference.Stop"/>.</value>
	[Parameter(ParameterSetName = WITH_CUSTOM_EQUALITY), Alias("ScriptErrorAction")]
	[PSDefaultValue(Value = ActionPreference.Stop)]
	public override ActionPreference ScriptBlockErrorAction { get; set; } = ActionPreference.Stop;

	#region PROCESSING

	/// <summary>
	/// Adds the elements of the current <see cref="InputObject"/> to the set.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Each element is converted to <see cref="GenericType"/>, which leaves it as it is when that type is
	/// <see cref="object"/>, and a failed conversion produces the non-terminating error that <c>New-List</c> writes. A set
	/// of <see cref="object"/> adds <see langword="null"/> elements too. A typed set skips them, along with elements that
	/// convert to <see langword="null"/>.
	/// </para>
	/// <para>
	/// An error from <see cref="EqualityScript"/> or <see cref="HashCodeScript"/>, including one for output that isn't a
	/// hash code, reaches PowerShell unchanged, so the cmdlet ends without writing a set. Any other failure while adding
	/// an element, such as an element type whose own <see cref="object.GetHashCode"/> method throws, produces a
	/// non-terminating error whose target is the element as it was before conversion, and the cmdlet goes on with the next
	/// one.
	/// </para>
	/// </remarks>
	/// <param name="collection">The set to add elements to.</param>
	/// <exception cref="RuntimeException">Thrown when adding an element throws one, for example because <see cref="HashCodeScript"/> fails.</exception>
	/// <exception cref="FlowControlException">Thrown when adding an element throws one, for example because <see cref="EqualityScript"/> runs <c>break</c>.</exception>
	protected override void Process(object collection)
	{
		// collection is the wrapper's own set. The wrapper converts each element and adds it with typed calls.
		_set.AddRange(this.GetInputElements(this.InputObject));
	}

	/// <summary>
	/// Writes the set to the pipeline as a single object.
	/// </summary>
	/// <param name="collection">The constructed set.</param>
	protected override void End(object collection)
	{
		this.WriteObject(collection, false);
	}

	#endregion

	#region BACKEND
	/// <summary>
	/// Creates the set for <see cref="GenericType"/> with the specified element comparer.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The method creates the set through a <see cref="SetWrapper"/> and keeps the wrapper, so
	/// <see cref="Process(object)"/> can add elements with typed calls. The set gets <see cref="Capacity"/> as its initial
	/// capacity. Without a comparer, <see cref="string"/> and <see cref="object"/> elements compare ordinally, without
	/// regard to case unless <c>-CaseSensitive</c> is set. Only a set of <see cref="object"/> adds
	/// <see langword="null"/> elements, so a typed set doesn't hold a value that wasn't in the input, such as 0 in a set of
	/// <see cref="int"/>.
	/// </para>
	/// <para>
	/// The method also sets the wrapper's callbacks. One writes the non-terminating error that <c>New-List</c> writes for
	/// each element that can't be converted. The other writes a non-terminating error for each element that the set fails
	/// to add, whose target is the element as it was before conversion. Setting them once here means that pipeline input
	/// doesn't allocate a delegate for each record.
	/// </para>
	/// </remarks>
	/// <param name="comparer">The element equality comparer, or <see langword="null"/> to use the default for the element type.</param>
	/// <returns>The new, empty set.</returns>
	/// <exception cref="ArgumentException">Thrown when <see cref="GenericType"/> can't be a type argument of <see cref="HashSet{T}"/>, such as a pointer type.</exception>
	/// <exception cref="OutOfMemoryException">Thrown when <see cref="Capacity"/> exceeds the maximum array length.</exception>
	private protected override object CreateCollection(IEqualityComparer? comparer)
	{
		Type elementType = this.GetEqualityForType();
		_set = SetWrapper.CreateHashSet(elementType, (uint)this.Capacity, comparer, this.CaseSensitive);

		_set.IncludeNulls = typeof(object).Equals(elementType);
		_set.ConversionFailed = (item, exception) => this.WriteConversionError(exception, item, this.GenericType);
		_set.AddFailed = (item, exception) => this.WriteError(exception.ToRecord(ErrorCategory.InvalidOperation, item));
		return _set.AsSet();
	}
	/// <summary>
	/// Returns the element equality comparer, building one from <see cref="EqualityScript"/> and
	/// <see cref="HashCodeScript"/> in the custom equality parameter set.
	/// </summary>
	/// <remarks>
	/// In any other parameter set, the method defers to the base implementation. The script-based comparer runs its
	/// script blocks with <c>$ErrorActionPreference</c> set to <see cref="ScriptBlockErrorAction"/>.
	/// </remarks>
	/// <param name="genericType">The element type.</param>
	/// <returns>The element equality comparer, or <see langword="null"/> to use the default for the element type.</returns>
	protected override IEqualityComparer? GetCustomEqualityComparer(Type genericType)
	{
		if (!WITH_CUSTOM_EQUALITY.Equals(this.ParameterSetName, StringComparison.OrdinalIgnoreCase))
		{
			return base.GetCustomEqualityComparer(genericType);
		}

		HashBlock hashBlock = new(this.HashCodeScript);
		ActionPreference errorPreference = this.ScriptBlockErrorAction;

		PSVariable variable = new(ERROR_ACTION_PREFERENCE, errorPreference);
#if NET9_0_OR_GREATER
		return new EqualityBlock(this.EqualityScript, hashBlock, variable);
#else
		return new EqualityBlock(this.EqualityScript, hashBlock, [variable]);
#endif
	}
	/// <summary>
	/// Returns <see cref="GenericType"/>, setting it to <see cref="object"/> when it is <see langword="null"/>.
	/// </summary>
	/// <returns>The element type.</returns>
	protected override Type GetEqualityForType()
	{
		return this.GenericType ??= typeof(object);
	}

	#endregion
}
