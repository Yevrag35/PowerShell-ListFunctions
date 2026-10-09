using ListFunctions.Extensions;
using ListFunctions.Modern;

namespace ListFunctions.Internal;

/// <summary>
/// Provides a non-generic front end over a strongly typed <see cref="Dictionary{TKey, TValue}"/> whose key and value
/// types are known only at run time.
/// </summary>
/// <remarks>
/// <para>
/// Each added key and value is converted to the dictionary's key or value type with PowerShell's conversion rules, so the
/// dictionary stores what <c>$dictionary.Add($key, $value)</c> stores in PowerShell. A key or value that fails to convert
/// is skipped and reported through <see cref="ConversionFailed"/> instead of throwing.
/// </para>
/// <para>
/// The conversions and the calls to the dictionary run inside the closed <see cref="DictionaryWrapper{TKey, TValue}"/>, so
/// adding an entry doesn't go through reflection. An exception from the dictionary's comparer, such as an error from an
/// <see cref="EqualityBlock"/>'s script blocks, reaches the caller as it is, not inside a
/// <see cref="TargetInvocationException"/>.
/// </para>
/// <para>
/// Create instances with <see cref="CreateTyped(Type, Type, uint, IEqualityComparer, bool)"/>.
/// </para>
/// <para>
/// This type is not thread-safe. Synchronize access externally when multiple threads use the same instance.
/// </para>
/// </remarks>
internal abstract class DictionaryWrapper
{
	/// <summary>
	/// The generic method definition of <see cref="Create{TKey, TValue}(int, IEqualityComparer)"/>.
	/// </summary>
	/// <remarks>
	/// The definition comes from a delegate to one closed form of the method, so the compiler checks the method's name and
	/// signature, and no lookup by name can miss it.
	/// </remarks>
	private static readonly MethodInfo s_createDefinition = new Factory(Create<object, object>).Method.GetGenericMethodDefinition();

	/// <summary>
	/// Gets or sets the callback that is invoked when a key or a value cannot be converted to its type.
	/// </summary>
	/// <remarks>
	/// The callback receives the original key or value, the type that the conversion targeted, and the
	/// <see cref="PSInvalidCastException"/> that the conversion threw. The entry is not added, and the method that tried to
	/// add it returns <see cref="DictionaryAddResult.NotConverted"/>. When this property is <see langword="null"/>, entries
	/// that fail to convert are skipped silently.
	/// </remarks>
	/// <value>
	/// The callback to invoke for each key or value that fails to convert, or <see langword="null"/> to skip such entries
	/// without notification.
	/// </value>
	public Action<object?, Type, PSInvalidCastException>? ConversionFailed { get; set; }

	/// <summary>
	/// Gets or sets the callback that <see cref="Add(object, object)"/> invokes when the dictionary refuses an entry.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The callback receives the converted key and the exception that the dictionary threw, such as the
	/// <see cref="ArgumentException"/> for a key that is already in the dictionary, or the
	/// <see cref="ArgumentNullException"/> for a key that converts to <see langword="null"/>. The entry is not added, and
	/// <see cref="Add(object, object)"/> returns <see cref="DictionaryAddResult.Failed"/>. Only this path boxes a key of a
	/// value type.
	/// </para>
	/// <para>
	/// A <see cref="RuntimeException"/> or a <see cref="FlowControlException"/>, such as an error or a <c>break</c> from an
	/// <see cref="EqualityBlock"/>'s script blocks, never reaches the callback. It reaches the caller of
	/// <see cref="Add(object, object)"/> unchanged, so PowerShell handles it the way it handles one from any other script
	/// block. When this property is <see langword="null"/>, every exception reaches the caller.
	/// </para>
	/// </remarks>
	/// <value>
	/// The callback to invoke for each entry that the dictionary refuses, or <see langword="null"/> to let the exception
	/// reach the caller.
	/// </value>
	public Action<object?, Exception>? AddFailed { get; set; }

	/// <summary>
	/// Returns the underlying dictionary as a non-generic <see cref="IDictionary"/>.
	/// </summary>
	/// <remarks>
	/// The returned dictionary is the wrapper's own storage, not a copy. Entries added through the wrapper afterward appear
	/// in it, and changes made through it are visible to the wrapper.
	/// </remarks>
	/// <returns>The underlying dictionary.</returns>
	public abstract IDictionary AsDictionary();

	/// <summary>
	/// Converts the specified key and value and adds them to the dictionary as a new entry.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The key is converted first, and the value isn't converted when the key fails. A key or value that fails to convert
	/// is reported through <see cref="ConversionFailed"/>, and the entry is skipped.
	/// </para>
	/// <para>
	/// The method calls the dictionary's <c>Add</c> method directly. A key that is already in the dictionary, or that
	/// converts to <see langword="null"/>, makes it throw an <see cref="ArgumentException"/>. When <see cref="AddFailed"/>
	/// is set, the method passes that exception, and any other exception that isn't PowerShell's own, to the callback
	/// instead of throwing it.
	/// </para>
	/// </remarks>
	/// <param name="key">The key to convert and add. This value can be <see langword="null"/>.</param>
	/// <param name="value">The value to convert and add. This value can be <see langword="null"/>.</param>
	/// <returns>
	/// <see cref="DictionaryAddResult.Added"/> when the entry was added; <see cref="DictionaryAddResult.NotConverted"/> when
	/// the key or the value failed to convert; or <see cref="DictionaryAddResult.Failed"/> when the dictionary refused the
	/// entry and <see cref="AddFailed"/> received the exception.
	/// </returns>
	/// <exception cref="ArgumentException">Thrown when the converted key is already in the dictionary or is null, and <see cref="AddFailed"/> is null.</exception>
	/// <exception cref="RuntimeException">Thrown when the dictionary's comparer throws one, for example because an <see cref="EqualityBlock"/>'s script block fails.</exception>
	/// <exception cref="FlowControlException">Thrown when the dictionary's comparer throws one, for example because an <see cref="EqualityBlock"/>'s script block runs <c>break</c>.</exception>
	public abstract DictionaryAddResult Add(object? key, object? value);

	/// <summary>
	/// Converts the specified key and value, and adds them to the dictionary as a new entry or adds the value to the list
	/// of a key that is already in it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The first repeat of a key replaces the key's value with an <see cref="ObjectList"/> that holds the existing value and
	/// the new one, and each later repeat adds its value to that list. A key or value that fails to convert is reported
	/// through <see cref="ConversionFailed"/>, and the entry is skipped.
	/// </para>
	/// <para>
	/// The dictionary's value type must be able to hold an <see cref="ObjectList"/>, as <see cref="object"/> can. The method
	/// doesn't use <see cref="AddFailed"/>, so an exception from the dictionary reaches the caller.
	/// </para>
	/// <para>
	/// <b>Performance:</b> On .NET 6 and later, the method finds or adds the key's entry with one lookup, through
	/// <c>CollectionsMarshal.GetValueRefOrAddDefault</c>. On .NET Standard 2.0, it takes two lookups.
	/// </para>
	/// </remarks>
	/// <param name="key">The key to convert and add. This value can be <see langword="null"/>.</param>
	/// <param name="value">The value to convert and add. This value can be <see langword="null"/>.</param>
	/// <returns>
	/// <see cref="DictionaryAddResult.Added"/> when the entry was added; <see cref="DictionaryAddResult.NotConverted"/> when
	/// the key or the value failed to convert; or <see cref="DictionaryAddResult.Appended"/> when the key was already in
	/// the dictionary and the value was added to its list.
	/// </returns>
	/// <exception cref="InvalidOperationException">Thrown when the dictionary's value type can't hold an <see cref="ObjectList"/>.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the converted key is null.</exception>
	/// <exception cref="RuntimeException">Thrown when the dictionary's comparer throws one, for example because an <see cref="EqualityBlock"/>'s script block fails.</exception>
	/// <exception cref="FlowControlException">Thrown when the dictionary's comparer throws one, for example because an <see cref="EqualityBlock"/>'s script block runs <c>break</c>.</exception>
	public abstract DictionaryAddResult AddOrAppend(object? key, object? value);

	/// <summary>
	/// Converts the specified key and value, and adds them to the dictionary as a new entry unless the key is already in
	/// it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A key that is already in the dictionary keeps its value, and the method doesn't throw for it. A key or value that
	/// fails to convert is reported through <see cref="ConversionFailed"/>, and the entry is skipped.
	/// </para>
	/// <para>
	/// The method doesn't use <see cref="AddFailed"/>, so an exception from the dictionary reaches the caller. .NET Standard
	/// 2.0 has no <c>TryAdd</c> method on <see cref="Dictionary{TKey, TValue}"/>, so there the method looks up a new key
	/// twice: once to check for it and once to add it.
	/// </para>
	/// </remarks>
	/// <param name="key">The key to convert and add. This value can be <see langword="null"/>.</param>
	/// <param name="value">The value to convert and add. This value can be <see langword="null"/>.</param>
	/// <returns>
	/// <see cref="DictionaryAddResult.Added"/> when the entry was added; <see cref="DictionaryAddResult.NotConverted"/> when
	/// the key or the value failed to convert; or <see cref="DictionaryAddResult.KeyExists"/> when the key was already in
	/// the dictionary.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when the converted key is null.</exception>
	/// <exception cref="RuntimeException">Thrown when the dictionary's comparer throws one, for example because an <see cref="EqualityBlock"/>'s script block fails.</exception>
	/// <exception cref="FlowControlException">Thrown when the dictionary's comparer throws one, for example because an <see cref="EqualityBlock"/>'s script block runs <c>break</c>.</exception>
	public abstract DictionaryAddResult TryAdd(object? key, object? value);

	/// <summary>
	/// Creates a wrapper over a new <see cref="Dictionary{TKey, TValue}"/> whose key type is <paramref name="keyType"/>
	/// and whose value type is <paramref name="valueType"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// When <paramref name="comparer"/> is <see langword="null"/>, <see cref="string"/> and <see cref="object"/> keys
	/// compare with <see cref="StringComparer.OrdinalIgnoreCase"/>, or with <see cref="StringComparer.Ordinal"/> when
	/// <paramref name="caseSensitive"/> is <see langword="true"/>. A <see cref="StringComparer"/> compares two strings as
	/// strings, and any other two keys with their own <see cref="object.Equals(object)"/> method, so <c>1</c> and
	/// <c>"1"</c> are different keys. Keys of any other type compare with <see cref="EqualityComparer{T}.Default"/>.
	/// </para>
	/// <para>
	/// A comparer that isn't an <see cref="IEqualityComparer{T}"/> of the key type, such as a <see cref="StringComparer"/>
	/// for <see cref="object"/> keys or an <see cref="EqualityBlock"/> for <see cref="int"/> keys, is wrapped in an
	/// <see cref="EqualityComparerAdapter{T}"/>.
	/// </para>
	/// <para>
	/// The method closes a generic factory method over the two types and calls it through a delegate, not through
	/// <see cref="Activator.CreateInstance(Type, object[])"/>. So an exception from the dictionary's constructor, such as
	/// the <see cref="OutOfMemoryException"/> for a capacity that no array can hold, reaches the caller as it is. On .NET
	/// 10, <see cref="Activator.CreateInstance(Type, object[])"/> would wrap it in a
	/// <see cref="TargetInvocationException"/>.
	/// </para>
	/// </remarks>
	/// <param name="keyType">The key type of the dictionary. This value must not be <see langword="null"/>.</param>
	/// <param name="valueType">The value type of the dictionary. This value must not be <see langword="null"/>.</param>
	/// <param name="capacity">
	/// The initial capacity of the dictionary. A value of <c>0</c> leaves the dictionary empty with no preallocated storage.
	/// Values greater than <see cref="int.MaxValue"/> are clamped to <see cref="int.MaxValue"/>.
	/// </param>
	/// <param name="comparer">The equality comparer for the keys, or <see langword="null"/> to use the default for the key type.</param>
	/// <param name="caseSensitive">
	/// <see langword="true"/> to have the default comparer of <see cref="string"/> and <see cref="object"/> keys consider
	/// case; otherwise, <see langword="false"/>. The value has no effect when <paramref name="comparer"/> isn't
	/// <see langword="null"/>.
	/// </param>
	/// <returns>A new, empty wrapper whose dictionary has the key type <paramref name="keyType"/> and the value type <paramref name="valueType"/>.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="keyType"/> or <paramref name="valueType"/> is null.</exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="keyType"/> or <paramref name="valueType"/> cannot be used as a generic type argument, such
	/// as a pointer, by-ref, or <see cref="Void"/> type.
	/// </exception>
	/// <exception cref="OutOfMemoryException">Thrown when <paramref name="capacity"/> exceeds the maximum array length.</exception>
	public static DictionaryWrapper CreateTyped(Type keyType, Type valueType, uint capacity, IEqualityComparer? comparer, bool caseSensitive = false)
	{
		ArgumentNullException.ThrowIfNull(keyType);
		ArgumentNullException.ThrowIfNull(valueType);

		comparer ??= GetDefaultComparer(keyType, caseSensitive);
		var create = (Factory)s_createDefinition
			.MakeGenericMethod(keyType, valueType)
			.CreateDelegate(typeof(Factory));

		return create((int)Math.Min(capacity, int.MaxValue), comparer);
	}

	/// <summary>
	/// Creates a <see cref="DictionaryWrapper{TKey, TValue}"/> with the specified capacity and key comparer.
	/// </summary>
	/// <remarks>
	/// <see cref="CreateTyped(Type, Type, uint, IEqualityComparer, bool)"/> closes this method over the key and value
	/// types and calls it through a <see cref="Factory"/> delegate.
	/// </remarks>
	/// <typeparam name="TKey">The key type of the dictionary.</typeparam>
	/// <typeparam name="TValue">The value type of the dictionary.</typeparam>
	/// <param name="capacity">The initial capacity of the dictionary, from 0 through <see cref="int.MaxValue"/>.</param>
	/// <param name="comparer">The equality comparer for the keys, or <see langword="null"/> to use <see cref="EqualityComparer{T}.Default"/>.</param>
	/// <returns>The new wrapper.</returns>
	/// <exception cref="OutOfMemoryException">Thrown when <paramref name="capacity"/> exceeds the maximum array length.</exception>
	private static DictionaryWrapper Create<TKey, TValue>(int capacity, IEqualityComparer? comparer)
		where TKey : notnull
	{
		return new DictionaryWrapper<TKey, TValue>(capacity, comparer);
	}

	/// <summary>
	/// Returns the comparer that a dictionary with the specified key type uses when no comparer is given.
	/// </summary>
	/// <param name="keyType">The key type of the dictionary.</param>
	/// <param name="caseSensitive"><see langword="true"/> to compare strings with regard to case; otherwise, <see langword="false"/>.</param>
	/// <returns>
	/// <see cref="StringComparer.Ordinal"/> or <see cref="StringComparer.OrdinalIgnoreCase"/> for <see cref="string"/> and
	/// <see cref="object"/> keys; otherwise, <see langword="null"/>, which gives the dictionary
	/// <see cref="EqualityComparer{T}.Default"/>.
	/// </returns>
	private static StringComparer? GetDefaultComparer(Type keyType, bool caseSensitive)
	{
		if (!typeof(string).Equals(keyType) && !typeof(object).Equals(keyType))
		{
			return null;
		}

		return caseSensitive
			? StringComparer.Ordinal
			: StringComparer.OrdinalIgnoreCase;
	}

	/// <summary>
	/// Represents <see cref="Create{TKey, TValue}(int, IEqualityComparer)"/> after it's closed over a key type and a value
	/// type.
	/// </summary>
	/// <param name="capacity">The initial capacity of the dictionary, from 0 through <see cref="int.MaxValue"/>.</param>
	/// <param name="comparer">The equality comparer for the keys, or <see langword="null"/> to use <see cref="EqualityComparer{T}.Default"/>.</param>
	/// <returns>The new wrapper.</returns>
	private delegate DictionaryWrapper Factory(int capacity, IEqualityComparer? comparer);
}

/// <summary>
/// Wraps a <see cref="Dictionary{TKey, TValue}"/> and converts each added key to <typeparamref name="TKey"/> and each
/// value to <typeparamref name="TValue"/> with PowerShell's conversion rules.
/// </summary>
/// <remarks>
/// <para>
/// The <c>notnull</c> constraint on <typeparamref name="TKey"/> affects only nullable analysis, so the type can be closed
/// over a <see cref="Nullable{T}"/> key type at run time. The dictionary still rejects a key that is
/// <see langword="null"/>.
/// </para>
/// <para>
/// This type is not thread-safe. Synchronize access externally when multiple threads use the same instance.
/// </para>
/// </remarks>
/// <typeparam name="TKey">The key type of the underlying dictionary.</typeparam>
/// <typeparam name="TValue">The value type of the underlying dictionary.</typeparam>
internal sealed class DictionaryWrapper<TKey, TValue> : DictionaryWrapper
	where TKey : notnull
{
	/// <summary>
	/// Indicates whether <typeparamref name="TValue"/> can hold an <see cref="ObjectList"/>, which
	/// <see cref="AddOrAppend(object, object)"/> requires.
	/// </summary>
	private static readonly bool s_valuesHoldLists = typeof(TValue).IsAssignableFrom(typeof(ObjectList));

	private readonly Dictionary<TKey, TValue> _dictionary;

	/// <summary>
	/// Initializes a new <see cref="DictionaryWrapper{TKey, TValue}"/> instance with an empty dictionary that has the
	/// specified capacity and key comparer.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A comparer that isn't an <see cref="IEqualityComparer{T}"/> of <typeparamref name="TKey"/> is wrapped in an
	/// <see cref="EqualityComparerAdapter{T}"/>. The type check honors variance, so an <see cref="EqualityBlock"/>, which is
	/// an <see cref="IEqualityComparer{T}"/> of <see cref="object"/>, is used as it is for <see cref="string"/> keys and
	/// other reference types, and is wrapped for value types, such as <see cref="int"/> and <see cref="Nullable{T}"/>.
	/// </para>
	/// <para>
	/// <see cref="DictionaryWrapper.CreateTyped(Type, Type, uint, IEqualityComparer, bool)"/> calls this constructor
	/// through a delegate when the key and value types are known only at run time. It chooses the default comparer for
	/// <see cref="string"/> and <see cref="object"/> keys before it does.
	/// </para>
	/// </remarks>
	/// <param name="capacity">The initial capacity of the dictionary. A value of <c>0</c> leaves the dictionary empty with no preallocated storage.</param>
	/// <param name="comparer">The equality comparer for the keys, or <see langword="null"/> to use <see cref="EqualityComparer{T}.Default"/>.</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="capacity"/> is negative.</exception>
	/// <exception cref="OutOfMemoryException">Thrown when <paramref name="capacity"/> exceeds the maximum array length.</exception>
	public DictionaryWrapper(int capacity, IEqualityComparer? comparer)
	{
		IEqualityComparer<TKey>? typedComparer = comparer switch
		{
			null => null,
			IEqualityComparer<TKey> typed => typed,
			_ => new EqualityComparerAdapter<TKey>(comparer),
		};

		_dictionary = new Dictionary<TKey, TValue>(capacity, typedComparer);
	}

	/// <summary>
	/// Returns the underlying <see cref="Dictionary{TKey, TValue}"/> as a non-generic <see cref="IDictionary"/>.
	/// </summary>
	/// <remarks>
	/// The returned dictionary is the wrapper's own storage, not a copy. Callers can cast it back to a
	/// <see cref="Dictionary{TKey, TValue}"/> whose type arguments are <typeparamref name="TKey"/> and
	/// <typeparamref name="TValue"/>.
	/// </remarks>
	/// <returns>The underlying dictionary.</returns>
	public override IDictionary AsDictionary()
	{
		return _dictionary;
	}

	/// <inheritdoc/>
	public override DictionaryAddResult Add(object? key, object? value)
	{
		if (!this.TryConvert(key, out TKey typedKey) || !this.TryConvert(value, out TValue typedValue))
		{
			return DictionaryAddResult.NotConverted;
		}

		try
		{
			_dictionary.Add(typedKey, typedValue);
			return DictionaryAddResult.Added;
		}
		catch (Exception e) when (this.AddFailed is { } addFailed && e is not (RuntimeException or FlowControlException))
		{
			addFailed(typedKey, e);
			return DictionaryAddResult.Failed;
		}
	}

	/// <inheritdoc/>
	public override DictionaryAddResult AddOrAppend(object? key, object? value)
	{
		if (!s_valuesHoldLists)
		{
			throw new InvalidOperationException(
				$"Cannot collect the values of a repeated key in a dictionary whose value type is [{typeof(TValue).GetTypeName()}], "
				+ "because the type can't hold a list. Use [object] values instead.");
		}

		if (!this.TryConvert(key, out TKey typedKey) || !this.TryConvert(value, out TValue typedValue))
		{
			return DictionaryAddResult.NotConverted;
		}

#if NET6_0_OR_GREATER
		ref TValue? slot = ref CollectionsMarshal.GetValueRefOrAddDefault(_dictionary, typedKey, out bool exists);
		if (!exists)
		{
			slot = typedValue;
			return DictionaryAddResult.Added;
		}

		slot = Append(slot, typedValue);
#else
		if (!_dictionary.TryGetValue(typedKey, out TValue? existing))
		{
			_dictionary.Add(typedKey, typedValue);
			return DictionaryAddResult.Added;
		}

		_dictionary[typedKey] = Append(existing, typedValue);
#endif

		return DictionaryAddResult.Appended;
	}

	/// <inheritdoc/>
	public override DictionaryAddResult TryAdd(object? key, object? value)
	{
		if (!this.TryConvert(key, out TKey typedKey) || !this.TryConvert(value, out TValue typedValue))
		{
			return DictionaryAddResult.NotConverted;
		}

		return _dictionary.TryAdd(typedKey, typedValue)
			? DictionaryAddResult.Added
			: DictionaryAddResult.KeyExists;
	}

	/// <summary>
	/// Adds a value to the list that a key's value already is, or creates a list that holds both values.
	/// </summary>
	/// <remarks>
	/// The caller stores the result as the key's value. <see cref="AddOrAppend(object, object)"/> calls the method only
	/// when <typeparamref name="TValue"/> can hold an <see cref="ObjectList"/>.
	/// </remarks>
	/// <param name="existing">The key's current value, or <see langword="null"/>.</param>
	/// <param name="value">The value to add, or <see langword="null"/>.</param>
	/// <returns>
	/// <paramref name="existing"/> itself, after <paramref name="value"/> is added to it, when it's an
	/// <see cref="ObjectList"/>; otherwise, a new <see cref="ObjectList"/> that holds <paramref name="existing"/> and
	/// <paramref name="value"/>, in that order.
	/// </returns>
	private static TValue Append(TValue? existing, TValue value)
	{
		if (existing is ObjectList values)
		{
			values.Add(value);
			return existing;
		}

		ObjectList collected = [existing, value];
		return (TValue)(object)collected;
	}

	/// <summary>
	/// Converts the specified key or value to <typeparamref name="T"/> with PowerShell's conversion rules.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A key or value that already has the type passes through unchanged, so a key that the caller converted itself keeps
	/// the result of that conversion.
	/// </para>
	/// <para>
	/// When the conversion throws a <see cref="PSInvalidCastException"/>, the method passes the item, the target type, and
	/// the exception to <see cref="DictionaryWrapper.ConversionFailed"/>, if it is set.
	/// </para>
	/// </remarks>
	/// <typeparam name="T">The type to convert to: <typeparamref name="TKey"/> or <typeparamref name="TValue"/>.</typeparam>
	/// <param name="item">The key or value to convert. This value can be <see langword="null"/>.</param>
	/// <param name="result">
	/// When this method returns <see langword="true"/>, the converted item, which can be <see langword="null"/>; otherwise,
	/// the default value of <typeparamref name="T"/>.
	/// </param>
	/// <returns>
	/// <see langword="true"/> if the conversion succeeds, even when <paramref name="result"/> is <see langword="null"/>;
	/// otherwise, <see langword="false"/>.
	/// </returns>
	private bool TryConvert<T>(object? item, out T result)
	{
		try
		{
			result = LanguagePrimitives.ConvertTo<T>(item);
			return true;
		}
		catch (PSInvalidCastException e)
		{
			this.ConversionFailed?.Invoke(item, typeof(T), e);

			// The callers return without reading the result when the conversion fails.
			result = default!;
			return false;
		}
	}
}
