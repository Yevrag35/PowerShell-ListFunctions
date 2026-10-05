using ListFunctions.Extensions;
using ListFunctions.Modern;
using ListFunctions.Modern.Constructors;
using ListFunctions.Modern.Variables;
using ListFunctions.Validation;

#nullable enable

namespace ListFunctions.Cmdlets.Constructs;

/// <summary>
/// Creates a new <see cref="HashSet{T}"/> and optionally adds elements to it from the pipeline.
/// </summary>
/// <remarks>
/// <para>
/// When <see cref="GenericType"/> is <see cref="object"/> and no custom equality script blocks are supplied, elements
/// compare by using PowerShell's equality rules, so <c>"1"</c> and <c>1</c> are equal. Strings compare
/// case-insensitively unless <c>-CaseSensitive</c> is set.
/// </para>
/// <para>
/// When <see cref="GenericType"/> is <see cref="string"/>, elements compare with
/// <see cref="StringComparer.OrdinalIgnoreCase"/>, or with <see cref="StringComparer.Ordinal"/> when
/// <c>-CaseSensitive</c> is set. Other element types use <see cref="EqualityComparer{T}.Default"/>.
/// <see cref="EqualityScript"/> and <see cref="HashCodeScript"/> replace the default comparison with PowerShell
/// script blocks.
/// </para>
/// <para>
/// Duplicate elements are ignored. The set is written as a single object and is not enumerated into the pipeline.
/// </para>
/// </remarks>
[Cmdlet(VerbsCommon.New, "HashSet", DefaultParameterSetName = SPECIFIED_TYPE)]
[OutputType(typeof(HashSet<>))]
public sealed class NewHashSetCmdlet : EqualityConstructingCmdlet<object>, IDynamicParameters
{
	private const string DYN_PSET_NAME = "StringSet";
	private const string SPECIFIED_TYPE = "SpecifiedType";

	/// <inheritdoc/>
	protected override string CaseSensitiveParameterSetName => DYN_PSET_NAME;

	/// <summary>
	/// Gets or sets the initial capacity requested for the set.
	/// </summary>
	/// <remarks>
	/// The set is created with room for this many elements, so it doesn't have to grow until it holds more.
	/// </remarks>
	/// <value>The requested initial capacity, from 0 through <see cref="int.MaxValue"/>. Defaults to 0.</value>
	[Parameter, ValidateRange(0, int.MaxValue), PSDefaultValue(Value = 0)]
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
	/// element type is <see cref="object"/> or <see cref="string"/>. It can't be combined with
	/// <see cref="EqualityScript"/> and <see cref="HashCodeScript"/>.
	/// </para>
	/// </remarks>
	/// <value>The element type. Defaults to <see cref="object"/>.</value>
	[Parameter(Mandatory = false, Position = 0, ParameterSetName = SPECIFIED_TYPE)]
	[Parameter(Mandatory = false, Position = 0, ParameterSetName = DYN_PSET_NAME)]
	[ArgumentToTypeTransform, Alias("Type")]
	[PSDefaultValue(Value = typeof(object))]
	public Type GenericType { get; set; } = null!;

	/// <summary>
	/// Gets or sets the elements to add to the set. The value is accepted from the pipeline.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Each pipeline object is one element, even when it's <see langword="null"/> or an array. An array passed to the
	/// parameter supplies its elements, and <see langword="null"/> supplies none.
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
	/// The script block receives the two elements as <c>$x</c> and <c>$y</c>, as <c>$left</c> and <c>$right</c>, or
	/// as <c>$args[0]</c> and <c>$args[1]</c>. It must reference one variable for each element. Its output is
	/// converted to a <see cref="bool"/> by using PowerShell's truthiness rules.
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
	/// The script block receives the element as <c>$_</c>, <c>$this</c>, <c>$PSItem</c>, or <c>$args[0]</c> and must
	/// reference at least one of them. Elements that <see cref="EqualityScript"/> considers equal must produce the
	/// same hash code.
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
	[Parameter(ParameterSetName = WITH_CUSTOM_EQUALITY)]
	[PSDefaultValue(Value = ActionPreference.Stop)]
	public override ActionPreference ScriptBlockErrorAction { get; set; } = ActionPreference.Stop;

	#region PROCESSING

	/// <summary>
	/// Adds the elements of the current <see cref="InputObject"/> to the set.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Elements are added to a set of <see cref="object"/> as they are. For a typed set, each element is first
	/// converted to the element type, and a failed conversion produces the non-terminating error that <c>New-List</c>
	/// writes.
	/// </para>
	/// <para>
	/// Any other failure while adding an element, such as a script block equality comparer that throws, produces a
	/// non-terminating error and stops processing. The remaining elements of the current input are still added, but
	/// later pipeline input is ignored and no set is written.
	/// </para>
	/// </remarks>
	/// <param name="collection">The set to add elements to.</param>
	/// <param name="collectionType">The closed generic type of the set.</param>
	/// <returns><see langword="false"/> when an element fails to be added for a reason other than conversion; otherwise, <see langword="true"/>.</returns>
	protected override bool Process(object collection, Type collectionType)
	{
		bool flag = true;
		object?[] elements = this.GetInputElements(this.InputObject);
		if (elements.Length == 0)
		{
			return flag;
		}

		if (collection is ICollection<object?> objCol)
		{
			foreach (object? item in elements)
			{
				try
				{
					objCol.Add(item);
				}
				catch (Exception e)
				{
					var rec = e.ToRecord(ErrorCategory.InvalidOperation, item);
					this.WriteError(rec);
					flag = false;
				}
			}
		}
		else
		{
			foreach (object? item in elements)
			{
				try
				{
					if (!this.AddToCollection(collection, item, LanguagePrimitives.ConvertTo))
					{
						flag = false;
					}
				}
				catch (PSInvalidCastException e)
				{
					this.WriteConversionError(e, item, this.GenericType);
				}
				catch (Exception e)
				{
					var rec = e.ToRecord(ErrorCategory.InvalidOperation, item);
					this.WriteError(rec);
					flag = false;
				}
			}
		}

		return flag;
	}

	/// <summary>
	/// Writes the set to the pipeline as a single object.
	/// </summary>
	/// <param name="collection">The constructed set.</param>
	/// <param name="wantsToStop"><see langword="true"/> to skip writing the set; otherwise, <see langword="false"/>.</param>
	protected override void End(object collection, bool wantsToStop)
	{
		if (!wantsToStop)
		{
			this.WriteObject(collection, false);
		}
	}

	#endregion

	#region BACKEND
	/// <summary>
	/// Creates a <see cref="HashSetCtor"/> for the first generic type argument.
	/// </summary>
	/// <param name="comparer">The element equality comparer, or <see langword="null"/> to use the default for the element type.</param>
	/// <param name="genericTypes">The generic type arguments. The first element is the element type; when the array is <see langword="null"/> or empty, <see cref="object"/> is used.</param>
	/// <returns>A <see cref="HashSetCtor"/> that honors the <c>-CaseSensitive</c> switch.</returns>
	protected override EqualityCollectionCtor GetConstructor(IEqualityComparer? comparer, Type[]? genericTypes)
	{
		return new HashSetCtor(genericTypes is null || genericTypes.Length <= 0
			? typeof(object)
			: genericTypes[0], comparer)
		{
			IsCaseSensitive = this.CaseSensitive,
		};
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
	/// <summary>
	/// Returns the element type, setting <see cref="GenericType"/> to <see cref="object"/> when it is <see langword="null"/>.
	/// </summary>
	/// <returns>A single-element array that contains <see cref="GenericType"/>.</returns>
	protected override Type[]? GetGenericTypes()
	{
		this.GenericType ??= typeof(object);

		return [this.GenericType];
	}

	#endregion
}
