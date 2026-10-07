using ListFunctions.Components;
using ListFunctions.Extensions;
using ListFunctions.Internal;
using ListFunctions.Validation;
#nullable enable

namespace ListFunctions.Cmdlets.Constructs;

/// <summary>
/// Creates a new <see cref="List{T}"/> and optionally adds elements to it from the pipeline.
/// </summary>
/// <remarks>
/// <para>
/// The list's element type is <see cref="GenericType"/>, or <see cref="object"/> when it is not specified. For a
/// typed list, each input element is converted to the element type, and an element that cannot be converted produces
/// a non-terminating error and is skipped.
/// </para>
/// <para>
/// When the list type cannot be constructed, the cmdlet writes a non-terminating error, ignores all input, and writes
/// no list. Otherwise, the list is written as a single object and is not enumerated into the pipeline.
/// </para>
/// </remarks>
[Cmdlet(VerbsCommon.New, "List", DefaultParameterSetName = "None")]
[OutputType(typeof(List<object>))]
public sealed class NewListCmdlet : ListFunctionCmdletBase
{
	private ListWrapper? _list;

	/// <summary>
	/// Gets or sets the initial capacity of the list.
	/// </summary>
	/// <remarks>
	/// The list is created with room for this many elements, so it doesn't have to grow until it holds more. A value of 0
	/// gives the list a capacity of 0, as <see cref="List{T}.List()"/> does.
	/// </remarks>
	/// <value>The initial capacity, from 0 through <see cref="int.MaxValue"/>. Defaults to 0.</value>
	[Parameter]
	[Alias("Size"), PSDefaultValue(Value = 0), ValidateRange(0, int.MaxValue)]
	public int Capacity { get; set; }

	/// <summary>
	/// Gets or sets the element type of the list.
	/// </summary>
	/// <remarks>
	/// The parameter accepts a <see cref="Type"/>, a type name, or a script block that contains a type literal such
	/// as <c>{ [int] }</c>. Setting the value to <see langword="null"/> selects <see cref="object"/>.
	/// </remarks>
	/// <value>The element type. Defaults to <see cref="object"/>.</value>
	[Parameter(Position = 0)]
	[Alias("Type"), ArgumentToTypeTransform, PSDefaultValue(Value = typeof(object))]
	[AllowsNull, PSAllowNull]
	public Type GenericType { get => field ??= typeof(object); set; }

	/// <summary>
	/// Gets or sets the elements to add to the list. The value is accepted from the pipeline.
	/// </summary>
	/// <remarks>
	/// Each pipeline object is one element, even when it's <see langword="null"/> or an array. An array passed to the
	/// parameter supplies its elements, and <see langword="null"/> supplies none. The parameter can't be combined with
	/// pipeline input. <see langword="null"/> elements are skipped unless <see cref="IncludeNullElements"/> is set.
	/// </remarks>
	/// <value>The current pipeline object, or the argument of the parameter. The value can be <see langword="null"/>.</value>
	[Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "InitialAdd")]
	[AllowEmptyCollection, PSAllowNull, AllowEmptyString]
	public object? InputObject { get; set; }

	/// <summary>
	/// Gets or sets a value that indicates whether <see langword="null"/> elements of <see cref="InputObject"/> are
	/// added to the list.
	/// </summary>
	/// <remarks>
	/// <para>
	/// For a typed list, a <see langword="null"/> element is converted to the element type by PowerShell's conversion
	/// rules, so the list holds what <see cref="List{T}.Add(T)"/> stores when PowerShell calls it with
	/// <see langword="null"/>. For example, the element becomes 0 for <see cref="int"/> and an empty string for
	/// <see cref="string"/>, but it stays <see langword="null"/> for <see cref="Nullable{T}"/> and for most other
	/// reference types, such as <see cref="Version"/>.
	/// </para>
	/// <para>
	/// When this switch is not set, an element that converts to <see langword="null"/> is skipped too.
	/// </para>
	/// </remarks>
	/// <value><see langword="true"/> to add <see langword="null"/> elements; otherwise, <see langword="false"/>.</value>
	[Parameter(ParameterSetName = "InitialAdd")]
	[Alias("IncludeNulls")]
	public SwitchParameter IncludeNullElements { get; set; }

	/// <summary>
	/// Creates the list with the configured element type and capacity.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The method also sets the callback that the list calls for each element that cannot be converted to the element
	/// type. The callback writes a non-terminating error. Setting it once here means that pipeline input does not
	/// allocate a delegate for each record.
	/// </para>
	/// <para>
	/// When the list cannot be created, the method writes a non-terminating error instead of throwing, and the cmdlet
	/// ignores all input and writes no list.
	/// </para>
	/// </remarks>
	protected override void BeginCore()
	{
		try
		{
			_list = ListWrapper.CreateTyped(this.GenericType, (uint)this.Capacity, this.IncludeNullElements);
		}
		catch (Exception e)
		{
			this.WriteError(e.ToRecord(ErrorCategory.InvalidArgument, this.GenericType));
			return;
		}

		_list.ConversionFailed = (item, exception) => this.WriteConversionError(exception, item, this.GenericType);
	}

	/// <summary>
	/// Adds the elements of the current <see cref="InputObject"/> to the list.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The method does nothing when the list could not be created.
	/// </para>
	/// <para>
	/// The list converts each element to the element type by PowerShell's conversion rules, so a list of
	/// <see cref="object"/> receives each element as it is. An element that cannot be converted produces a
	/// non-terminating error and is skipped. A <see langword="null"/> element, or one that converts to
	/// <see langword="null"/>, is skipped unless <see cref="IncludeNullElements"/> is set.
	/// </para>
	/// </remarks>
	/// <returns>Always <see langword="true"/>, so all pipeline input is processed.</returns>
	protected override bool ProcessCore()
	{
		_list?.AddRange(this.GetInputElements(this.InputObject));
		return true;
	}

	/// <summary>
	/// Writes the list to the pipeline as a single object.
	/// </summary>
	/// <remarks>Nothing is written when the list could not be created.</remarks>
	/// <param name="state">The run state of the cmdlet. When <see cref="CmdletRunState.FoundMatch"/> is <see langword="true"/>, nothing is written.</param>
	protected override void EndCore(CmdletRunState state)
	{
		if (!state.FoundMatch && _list is not null)
		{
			this.WriteObject(_list.AsList(), false);
		}
	}
}
