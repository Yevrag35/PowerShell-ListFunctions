using ListFunctions.Extensions;
using ListFunctions.Modern.Exceptions;

namespace ListFunctions.Modern.Constructors;

/// <summary>
/// Represents a method that closes a generic type definition over a set of type arguments.
/// </summary>
/// <remarks>
/// Pass a <see cref="CreateConstructingType"/> to a <see cref="GenericCollectionCtor"/> to replace the default
/// behavior, which calls <see cref="Type.MakeGenericType(Type[])"/> on the definition.
/// </remarks>
/// <param name="genericTypeDefinition">The open generic type definition, such as <c>typeof(List&lt;&gt;)</c>.</param>
/// <param name="genericTypeArguments">The type arguments to close <paramref name="genericTypeDefinition"/> over.</param>
/// <returns>The closed generic type that the collection constructor creates instances of.</returns>
public delegate Type CreateConstructingType(Type genericTypeDefinition, Type[] genericTypeArguments);

/// <summary>
/// Provides the base class for objects that create instances of a closed generic collection type through
/// reflection.
/// </summary>
/// <remarks>
/// <para>
/// A derived class supplies the generic type definition and type arguments, the arguments for the collection's
/// constructor, and a fallback collection to create when the type arguments call for one. <see cref="Construct"/>
/// either creates that fallback collection or calls <see cref="Activator.CreateInstance(Type, object[])"/> on
/// <see cref="ConstructingGenericType"/>.
/// </para>
/// <para>
/// Instances aren't thread-safe. <see cref="Construct"/> can change <see cref="ConstructingGenericType"/> and
/// <see cref="GenericArgumentTypes"/>.
/// </para>
/// </remarks>
public abstract class GenericCollectionCtor
{
	/// <summary>
	/// Gets the closed generic type of the collection that <see cref="Construct"/> creates.
	/// </summary>
	/// <remarks>
	/// When <see cref="Construct"/> creates the fallback collection, this property changes to the type of that
	/// collection, which might not be generic.
	/// </remarks>
	/// <value>The runtime type of the collection.</value>
	public Type ConstructingGenericType { get; private set; }
	/// <summary>
	/// Gets the open generic type definition of the collection.
	/// </summary>
	/// <value>The generic type definition passed to the constructor, such as <c>typeof(List&lt;&gt;)</c>.</value>
	public Type GenericDefinitionType { get; }
	/// <summary>
	/// Gets the generic type arguments of the collection.
	/// </summary>
	/// <remarks>
	/// When <see cref="Construct"/> creates the fallback collection, this property changes to the type arguments of
	/// that collection, or to an empty array when that collection isn't generic.
	/// </remarks>
	/// <value>A copy of the type arguments passed to the constructor, or an empty array when none were passed.</value>
	public Type[] GenericArgumentTypes { get; private set; }
	/// <summary>
	/// Gets a value that indicates whether the collection has any generic type arguments.
	/// </summary>
	/// <value><see langword="true"/> if <see cref="GenericArgumentTypes"/> isn't empty; otherwise, <see langword="false"/>.</value>
	public bool HasGenerics => this.GenericArgumentTypes.Length > 0;

	/// <summary>
	/// Initializes a new <see cref="GenericCollectionCtor"/> instance with the specified generic type definition, type
	/// arguments, and callback that closes the definition over the arguments.
	/// </summary>
	/// <remarks>
	/// The constructor copies <paramref name="genericTypes"/>, so later changes to that array don't affect this
	/// instance.
	/// </remarks>
	/// <param name="genericDefinition">
	/// The open generic type definition of the collection. It must be a generic class that isn't abstract, and it
	/// must not be <see langword="null"/>.
	/// </param>
	/// <param name="genericTypes">
	/// The type arguments to close <paramref name="genericDefinition"/> over, or <see langword="null"/> for none.
	/// </param>
	/// <param name="makeConstructingTypeCallback">
	/// The method that closes <paramref name="genericDefinition"/> over <paramref name="genericTypes"/>, or
	/// <see langword="null"/> to call <see cref="Type.MakeGenericType(Type[])"/>.
	/// </param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="genericDefinition"/> is null.</exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="genericDefinition"/> isn't a generic type, isn't a class, or is abstract; or when
	/// <paramref name="makeConstructingTypeCallback"/> is null and <paramref name="genericTypes"/> doesn't satisfy
	/// the definition's type parameters.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when <paramref name="makeConstructingTypeCallback"/> is null and <paramref name="genericDefinition"/> is a
	/// closed generic type instead of a generic type definition.
	/// </exception>
	protected GenericCollectionCtor(Type genericDefinition, Type[]? genericTypes, CreateConstructingType? makeConstructingTypeCallback)
	{
		ArgumentNullException.ThrowIfNull(genericDefinition);
		GuardDefinition(genericDefinition);

		this.GenericDefinitionType = genericDefinition;
		this.GenericArgumentTypes = SetGenericTypes(genericTypes);
		this.ConstructingGenericType = makeConstructingTypeCallback is null
			? MakeConstructingType(genericDefinition, this.GenericArgumentTypes)
			: makeConstructingTypeCallback(genericDefinition, this.GenericArgumentTypes);
	}

	/// <summary>
	/// Creates a new, empty collection.
	/// </summary>
	/// <remarks>
	/// When <see cref="ShouldConstructDefault(Type[])"/> returns <see langword="true"/>, the method returns the
	/// collection from <see cref="ConstructDefault"/> and updates <see cref="ConstructingGenericType"/> and
	/// <see cref="GenericArgumentTypes"/> to match it. Otherwise, it creates an instance of
	/// <see cref="ConstructingGenericType"/> with the arguments from <see cref="GetConstructorArguments(Type[])"/>.
	/// </remarks>
	/// <returns>The new collection.</returns>
	/// <exception cref="ActivatorCtorException">
	/// Thrown when creating an instance of <see cref="ConstructingGenericType"/> fails or returns null.
	/// </exception>
	public object Construct()
	{
		if (this.ShouldConstructDefault(this.GenericArgumentTypes))
		{
			object defCol = this.ConstructDefault();
			this.ConstructingGenericType = defCol.GetType();
			this.GenericArgumentTypes = this.ConstructingGenericType.GetGenericArguments()
				?? Array.Empty<Type>();

			return defCol;
		}

		object?[]? ctorArgs = this.EnumerateCtorArguments(this.GenericArgumentTypes);

		return CallActivator(this.ConstructingGenericType, ctorArgs);
	}

	/// <summary>
	/// Returns the arguments from <see cref="GetConstructorArguments(Type[])"/> as an array.
	/// </summary>
	/// <param name="genericTypes">The generic type arguments of the collection.</param>
	/// <returns>The constructor arguments, or an empty array when the derived class supplies none.</returns>
	private object?[] EnumerateCtorArguments(Type[] genericTypes)
	{
		object?[]? ctorArgs;
		var enumCtorArgs = this.GetConstructorArguments(genericTypes);
		if (enumCtorArgs is null)
		{
			ctorArgs = Array.Empty<object>();
		}
		else
		{
			ctorArgs = enumCtorArgs.ToArray();
		}

		return ctorArgs;
	}

	/// <summary>
	/// Creates an instance of the specified type with the constructor that best matches the specified arguments.
	/// </summary>
	/// <remarks>
	/// Any exception that <see cref="Activator.CreateInstance(Type, object[])"/> throws is wrapped in an
	/// <see cref="ActivatorCtorException"/>.
	/// </remarks>
	/// <param name="constructingType">The type to create an instance of.</param>
	/// <param name="ctorArgs">The arguments to pass to the constructor, or <see langword="null"/> for none.</param>
	/// <returns>The new instance.</returns>
	/// <exception cref="ActivatorCtorException">
	/// Thrown when creating the instance throws an exception or returns null.
	/// </exception>
	private static object CallActivator(Type constructingType, object?[]? ctorArgs)
	{
		object? collection;
		try
		{
			collection = Activator.CreateInstance(constructingType, ctorArgs);
		}
		catch (Exception ex)
		{
			Debug.WriteLine(ex.Message);
			return ThrowBadCtor(constructingType, ex);
		}

		return collection
			?? ThrowBadCtor(constructingType);
	}

	/// <summary>
	/// Creates the fallback collection that <see cref="Construct"/> returns when
	/// <see cref="ShouldConstructDefault(Type[])"/> returns <see langword="true"/>.
	/// </summary>
	/// <remarks>
	/// The collection doesn't have to be an instance of <see cref="ConstructingGenericType"/>, and it doesn't have to be
	/// generic.
	/// </remarks>
	/// <returns>The new, empty collection. This value must not be <see langword="null"/>.</returns>
	protected abstract object ConstructDefault();
	/// <summary>
	/// Returns the arguments to pass to the constructor of <see cref="ConstructingGenericType"/>.
	/// </summary>
	/// <param name="genericTypes">The generic type arguments of the collection.</param>
	/// <returns>
	/// The constructor arguments in order, or <see langword="null"/> to call the parameterless constructor. This
	/// implementation always returns <see langword="null"/>.
	/// </returns>
	protected virtual IEnumerable<object?>? GetConstructorArguments(Type[] genericTypes)
	{
		return null;
	}

	/// <summary>
	/// Verifies that the specified type can serve as the generic type definition of a collection.
	/// </summary>
	/// <param name="genericDefinition">The type to verify.</param>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="genericDefinition"/> isn't a generic type, isn't a class, or is abstract.
	/// </exception>
	private static void GuardDefinition(Type genericDefinition)
	{
		if (!genericDefinition.IsGenericType)
		{
			throw new ArgumentException($"\"{genericDefinition.GetTypeName()}\" is not a valid generic type.");
		}
		else if (!genericDefinition.IsClass)
		{
			throw new ArgumentException($"\"{genericDefinition.GetTypeName()}\" is not a valid generic class.");
		}
		else if (genericDefinition.IsAbstract)
		{
			throw new ArgumentException($"\"{genericDefinition.GetTypeName()}\" cannot be an abstract class.");
		}
	}
	/// <summary>
	/// Closes the specified generic type definition over the specified type arguments.
	/// </summary>
	/// <param name="genericTypeDefinition">The open generic type definition.</param>
	/// <param name="genericTypes">The type arguments to close <paramref name="genericTypeDefinition"/> over.</param>
	/// <returns>The closed generic type.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when <paramref name="genericTypeDefinition"/> isn't a generic type definition.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="genericTypes"/> doesn't match the number of type parameters or doesn't satisfy their
	/// constraints.
	/// </exception>
	private static Type MakeConstructingType(Type genericTypeDefinition, Type[] genericTypes)
	{
		return genericTypeDefinition.MakeGenericType(genericTypes);
	}
	/// <summary>
	/// Determines whether <see cref="Construct"/> creates the fallback collection from
	/// <see cref="ConstructDefault"/> instead of an instance of <see cref="ConstructingGenericType"/>.
	/// </summary>
	/// <param name="genericTypes">The generic type arguments of the collection.</param>
	/// <returns>
	/// <see langword="true"/> to create the fallback collection; otherwise, <see langword="false"/>.
	/// </returns>
	protected abstract bool ShouldConstructDefault(Type[] genericTypes);
	/// <summary>
	/// Copies the specified type arguments into a new array.
	/// </summary>
	/// <param name="types">The type arguments to copy, or <see langword="null"/> for none.</param>
	/// <returns>
	/// A new array that contains the elements of <paramref name="types"/>, or an empty array when
	/// <paramref name="types"/> is <see langword="null"/> or empty.
	/// </returns>
	private static Type[] SetGenericTypes(IReadOnlyList<Type>? types)
	{
		if (types is null || types.Count <= 0)
		{
			return Array.Empty<Type>();
		}

		Type[] copyTo = new Type[types.Count];
		for (int i = 0; i < types.Count; i++)
		{
			copyTo[i] = types[i];
		}

		return copyTo;
	}
	/// <summary>
	/// Throws an <see cref="ActivatorCtorException"/> for the specified type.
	/// </summary>
	/// <param name="badType">The type that couldn't be created.</param>
	/// <returns>This method never returns.</returns>
	/// <exception cref="ActivatorCtorException">Always thrown.</exception>
	[DoesNotReturn]
	private static object ThrowBadCtor(Type badType)
	{
		throw new ActivatorCtorException(badType);
	}
	/// <summary>
	/// Throws an <see cref="ActivatorCtorException"/> for the specified type that wraps the specified exception.
	/// </summary>
	/// <param name="badType">The type that couldn't be created.</param>
	/// <param name="caughtException">The exception that creating the instance threw.</param>
	/// <returns>This method never returns.</returns>
	/// <exception cref="ActivatorCtorException">Always thrown.</exception>
	[DoesNotReturn]
	private static object ThrowBadCtor(Type badType, Exception caughtException)
	{
		throw new ActivatorCtorException(badType, caughtException);
	}
}

