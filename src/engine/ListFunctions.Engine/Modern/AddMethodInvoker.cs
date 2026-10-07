using ListFunctions.Modern.Constructors;

namespace ListFunctions.Modern;

/// <summary>
/// Represents a cached <c>Add</c> method of a generic collection type, which adds items to a collection that's known
/// only at run time.
/// </summary>
/// <remarks>
/// <para>
/// The invoker looks up the <c>Add</c> method once, when it's constructed, and calls it through reflection for every
/// item.
/// </para>
/// <para>
/// The invoker doesn't change after construction, so one instance can be shared between threads. The thread safety of
/// each call depends on the collection it adds to.
/// </para>
/// </remarks>
public sealed class AddMethodInvoker
{
	private readonly Type[] _genericTypes;
	private readonly MethodInfo _method;
	/// <summary>
	/// Gets the collection type whose <c>Add</c> method the invoker calls.
	/// </summary>
	/// <value>The value of <see cref="GenericCollectionCtor.ConstructingGenericType"/> when the invoker was constructed.</value>
	public Type ImplementingType { get; }

	/// <summary>
	/// Initializes a new <see cref="AddMethodInvoker"/> instance for the collection type that the specified constructor
	/// creates.
	/// </summary>
	/// <remarks>
	/// The constructor reads <see cref="GenericCollectionCtor.ConstructingGenericType"/> and
	/// <see cref="GenericCollectionCtor.GenericArgumentTypes"/> once. Because
	/// <see cref="GenericCollectionCtor.Construct"/> can change both, create the invoker after the collection is
	/// constructed.
	/// </remarks>
	/// <param name="constructor">The constructor that describes the collection type. This value must not be <see langword="null"/>.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="constructor"/> is null.</exception>
	/// <exception cref="ArgumentException">Thrown when the collection type isn't a supported collection or dictionary, or has the wrong number of type arguments.</exception>
	public AddMethodInvoker(GenericCollectionCtor constructor)
	{
		ArgumentNullException.ThrowIfNull(constructor);

		this.ImplementingType = constructor.ConstructingGenericType;
		_genericTypes = constructor.GenericArgumentTypes;
		_method = ReflectionResolver.GetAddMethod(this.ImplementingType, _genericTypes);
	}

	/// <summary>
	/// Tries to add the specified arguments to the specified collection, and returns any exception instead of throwing
	/// it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// When <paramref name="addIfNull"/> is <see langword="false"/> and any argument is <see langword="null"/>, the
	/// method skips the item and still returns <see langword="true"/>.
	/// </para>
	/// <para>
	/// When the <c>Add</c> method throws, <paramref name="caughtException"/> is the exception it threw, not the
	/// <see cref="TargetInvocationException"/> that reflection wraps it in.
	/// </para>
	/// </remarks>
	/// <param name="collection">The collection to add to. It must be an instance of <see cref="ImplementingType"/>.</param>
	/// <param name="arguments">The arguments to pass to the <c>Add</c> method: the item for a collection, or the key and value for a dictionary. This value must not be <see langword="null"/>.</param>
	/// <param name="addIfNull"><see langword="true"/> to add the item even when an argument is <see langword="null"/>; <see langword="false"/> to skip it.</param>
	/// <param name="caughtException">When the method returns <see langword="false"/>, the exception that stopped the item from being added; otherwise, <see langword="null"/>.</param>
	/// <returns><see langword="true"/> if the method added or deliberately skipped the item; <see langword="false"/> if <paramref name="arguments"/> is <see langword="null"/> or adding the item failed.</returns>
	public bool TryInvoke(object collection, object?[] arguments, bool addIfNull, [NotNullWhen(false)] out Exception? caughtException)
	{
		if (arguments is null)
		{
			caughtException = new ArgumentNullException(nameof(arguments), "The arguments array itself cannot be null.");
			return false;
		}

		caughtException = null;
		if (!addIfNull)
		{
			for (int i = 0; i < arguments.Length; i++)
			{
				if (arguments[i] is null)
				{
					return true;    // don't add but also don't error.
				}
			}
		}

		try
		{
			_ = _method.Invoke(collection, arguments);
			return true;
		}
		catch (TargetInvocationException e) when (e.InnerException is not null)
		{
			// Reflection wraps the exception that the Add method throws.
			caughtException = e.InnerException;
			return false;
		}
		catch (Exception e)
		{
			caughtException = e;
			return false;
		}
	}
}

