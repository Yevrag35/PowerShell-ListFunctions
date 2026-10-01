namespace ListFunctions.Internal;

public abstract class ListWrapper
{
	public abstract int Count { get; }
	public bool IncludeNulls { get; set; }
	public Action<object?, PSInvalidCastException>? ConversionFailed { get; set; }

	public void AddRange(ReadOnlySpan<object?> items)
	{
		foreach (object? item in items)
		{
			this.AddItem(item);
		}
	}

	public abstract IList AsList();
	protected abstract void AddItem(object? item);
	protected abstract void SetCapacity(uint capacity);

	public static ListWrapper CreateTyped(Type elementType, uint capacity, bool includeNulls = false)
	{
		Guard.NotNull(elementType);
		Type listType = typeof(ListWrapper<>).MakeGenericType(elementType);
		ListWrapper wrapper = (ListWrapper)Activator.CreateInstance(listType)!;
		wrapper.SetCapacity(capacity);
		wrapper.IncludeNulls = includeNulls;
		return wrapper;
	}
}

public sealed class ListWrapper<T> : ListWrapper
{
	[SuppressMessage("Style", "IDE0028", Justification = "Keeps an empty array on creation.")]
	private readonly List<T> _list = new(0);

	public override int Count => _list.Count;

	public ListWrapper()
	{
	}

	public override IList AsList()
	{
		return _list;
	}
	protected override void SetCapacity(uint capacity)
	{
		if (capacity == 0) return;

		_list.Capacity = (int)Math.Min(capacity, int.MaxValue);
	}

	protected override void AddItem(object? item)
	{
		if (item is null && !this.IncludeNulls)
		{
			return;
		}

		T value;
		try
		{
			value = LanguagePrimitives.ConvertTo<T>(item);
		}
		catch (PSInvalidCastException e)
		{
			this.ConversionFailed?.Invoke(item, e);
			return;
		}

		if (value is not null || this.IncludeNulls)
		{
			_list.Add(value);
		}
	}
}
