using ListFunctions.Components;
using ListFunctions.Extensions;
using ListFunctions.Validation;
using ZLinq;
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
[OutputType(typeof(List<>))]
public sealed class NewListCmdlet : ListFunctionCmdletBase
{
	/// <summary>
	/// The open generic <see cref="List{T}"/> type definition.
	/// </summary>
	internal static readonly Type ListTypeNoT = typeof(List<>);
	private static readonly object[] _defaultCapacityArgs = new[] { (object)4 };

	private bool _isObjectType;
	private IList _list = Array.Empty<object>();
	private bool _listIsNull;
	private Type? _genericType;

	/// <summary>
	/// Gets or sets the initial capacity of the list.
	/// </summary>
	/// <remarks>A value of 0 creates the list with a capacity of 4.</remarks>
	/// <value>The initial capacity, from 0 through <see cref="int.MaxValue"/>.</value>
	[Parameter(Position = 1)]
	[Alias("Size"), PSDefaultValue(Value = 4), ValidateRange(0, int.MaxValue)]
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
	public Type GenericType
	{
		get => _genericType ?? typeof(object);
		set => _genericType = value;
	}

	/// <summary>
	/// Gets or sets the elements to add to the list. The value is accepted from the pipeline.
	/// </summary>
	/// <remarks>
	/// <see langword="null"/> elements are skipped unless <see cref="IncludeNullElements"/> is set.
	/// </remarks>
	/// <value>The elements to add, or <see langword="null"/> when no elements are supplied.</value>
	[Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "InitialAdd")]
	[AllowEmptyCollection, PSAllowNull, AllowEmptyString]
	public object?[]? InputObject { get; set; }

	/// <summary>
	/// Gets or sets a value that indicates whether <see langword="null"/> elements of <see cref="InputObject"/> are
	/// added to the list.
	/// </summary>
	/// <remarks>
	/// For a typed list, a <see langword="null"/> element is added only when it converts to a non-null value of the
	/// element type, such as 0 for <see cref="int"/>. Otherwise, it is skipped.
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
	/// When <see cref="GenericType"/> was not specified, the method creates a <see cref="List{T}"/> of
	/// <see cref="object"/> directly. Otherwise, it creates the closed list type through reflection.
	/// </para>
	/// <para>
	/// The method also records whether the element type is <see cref="object"/>, which decides whether
	/// <see cref="ProcessCore"/> adds elements as they are or converts them.
	/// </para>
	/// </remarks>
	protected override void BeginCore()
	{
		_isObjectType = _genericType is null || typeof(object).Equals(_genericType);
		_list = _genericType is null
			? new List<object?>(this.Capacity > 0 ? this.Capacity : 4)
			: this.CreateNewList(this.Capacity, this.GenericType, out _listIsNull)!;
	}

	/// <summary>
	/// Creates a <see cref="List{T}"/> of the specified element type through reflection.
	/// </summary>
	/// <remarks>When construction fails, the method writes a non-terminating error instead of throwing.</remarks>
	/// <param name="capacity">The initial capacity. A value of 0 or less uses a capacity of 4.</param>
	/// <param name="genericType">The element type of the list.</param>
	/// <param name="listIsNull">When this method returns, contains <see langword="true"/> if the list could not be created; otherwise, <see langword="false"/>.</param>
	/// <returns>The new list, or <see langword="null"/> when it could not be created.</returns>
	private IList? CreateNewList(int capacity, Type genericType, out bool listIsNull)
	{
		Type listType = ListTypeNoT.MakeGenericType(genericType);

		object[] args = capacity > 0
			? new[] { (object)capacity }
			: _defaultCapacityArgs;

		try
		{
			listIsNull = false;
			return (IList)Activator.CreateInstance(listType, args)!;
		}
		catch (Exception e)
		{
			var rec = e.ToRecord(ErrorCategory.InvalidArgument, listType);
			this.WriteError(rec);
			listIsNull = true;
			return null;
		}
	}
	/// <summary>
	/// Adds the elements of the current <see cref="InputObject"/> array to the list.
	/// </summary>
	/// <remarks>
	/// The method does nothing when the list could not be created. Elements are added to a list of
	/// <see cref="object"/> as they are and are converted to the element type for a typed list.
	/// </remarks>
	/// <returns>Always <see langword="true"/>, so all pipeline input is processed.</returns>
	protected override bool ProcessCore()
	{
		bool flag = true;
		if (_listIsNull || this.InputObject is null || this.InputObject.Length == 0)
		{
			return flag;
		}

		try
		{
			if (_isObjectType)
			{
				this.AddItemsToList(_list, this.InputObject);
			}
			else
			{
				this.AddTypedItemsToList(_list, this.InputObject, this.GenericType);
			}

			return flag;
		}
		catch
		{
			flag = false;
			throw;
		}
	}
	/// <summary>
	/// Adds the specified elements to a list of <see cref="object"/> without conversion.
	/// </summary>
	/// <param name="list">The list to add to.</param>
	/// <param name="items">The elements to add. <see langword="null"/> elements are skipped unless <see cref="IncludeNullElements"/> is set.</param>
	private void AddItemsToList(IList list, object?[] items)
	{
		foreach (object? item in items.AsValueEnumerable())
		{
			if (item is null && !this.IncludeNullElements)
			{
				continue;
			}

			list.Add(item);
		}
	}
	/// <summary>
	/// Converts the specified elements to the element type and adds them to a typed list.
	/// </summary>
	/// <remarks>
	/// An element that cannot be converted produces a non-terminating error and is skipped. An element that converts
	/// to <see langword="null"/> is skipped without an error.
	/// </remarks>
	/// <param name="list">The list to add to.</param>
	/// <param name="items">The elements to add. <see langword="null"/> elements are skipped unless <see cref="IncludeNullElements"/> is set.</param>
	/// <param name="type">The element type of <paramref name="list"/>.</param>
	private void AddTypedItemsToList(IList list, object?[] items, Type type)
	{
		foreach (object? item in items.AsValueEnumerable())
		{
			if (item is null && !this.IncludeNullElements)
			{
				continue;
			}

			if (this.TryConvertItem(item, type, out object? result))
			{
				list.Add(result);
			}
		}
	}

	/// <summary>
	/// Writes the list to the pipeline as a single object.
	/// </summary>
	/// <remarks>Nothing is written when the list could not be created.</remarks>
	/// <param name="state">The run state of the cmdlet. When <see cref="CmdletRunState.FoundMatch"/> is <see langword="true"/>, nothing is written.</param>
	protected override void EndCore(CmdletRunState state)
	{
		if (!state.FoundMatch && !_listIsNull)
		{
			this.WriteObject(_list, false);
		}
	}
}
