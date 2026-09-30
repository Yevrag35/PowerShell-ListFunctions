using ListFunctions.Components;
using ListFunctions.Extensions;
using ListFunctions.Modern;
using ListFunctions.Modern.Constructors;
using System.Collections.ObjectModel;

#nullable enable

namespace ListFunctions.Cmdlets.Constructs;

/// <summary>
/// Provides a base class for cmdlets that construct a generic collection whose elements or keys are compared for
/// equality.
/// </summary>
/// <remarks>
/// <para>
/// The begin phase is sealed. It resolves the collection's generic type arguments, chooses an equality comparer,
/// constructs the collection through an <see cref="EqualityCollectionCtor"/>, and then calls
/// <see cref="Begin(T, Type)"/>. Derived classes add pipeline input in <see cref="Process(T, Type)"/> and write the
/// finished collection in <see cref="End(T, bool)"/>.
/// </para>
/// <para>
/// When the type used for equality is <see cref="string"/> or <see cref="object"/>, the cmdlet exposes a mandatory
/// dynamic <c>-CaseSensitive</c> switch in the parameter set named by <see cref="CaseSensitiveParameterSetName"/>.
/// Derived classes must implement <see cref="IDynamicParameters"/> for PowerShell to call
/// <see cref="GetDynamicParameters"/>.
/// </para>
/// </remarks>
/// <typeparam name="T">The type through which the derived cmdlet handles the constructed collection.</typeparam>
public abstract class EqualityConstructingCmdlet<T> : ListFunctionCmdletBase
{
	/// <summary>
	/// The name of the dynamic <c>-CaseSensitive</c> parameter.
	/// </summary>
	protected const string CASE_SENSE = "CaseSensitive";
	/// <summary>
	/// The name of the parameter set that copies existing entries without a custom equality comparer.
	/// </summary>
	protected const string JUST_COPY = "JustCopy";
	/// <summary>
	/// The name of the parameter set that copies existing entries and uses a custom equality comparer.
	/// </summary>
	protected const string AND_COPY = WITH_CUSTOM_EQUALITY + "AndCopy";

	private AddMethodInvoker _addMethod = null!;
	private RuntimeDefinedParameter _caseSensitive = null!;
	private T _collection = default!;
	private Type _collectionType = null!;
	private RuntimeDefinedParameterDictionary? _dict = null!;
	private Type[] _genericTypes = null!;

	/// <summary>
	/// Gets the dictionary that holds the cmdlet's dynamic parameters, creating it on first access.
	/// </summary>
	/// <value>The <see cref="RuntimeDefinedParameterDictionary"/> that holds the dynamic parameters.</value>
	[MemberNotNullWhen(true, nameof(_addMethod))]
	private RuntimeDefinedParameterDictionary DynParamLib
	{
		get => _dict ??= new RuntimeDefinedParameterDictionary();
	}
	/// <summary>
	/// Gets the name of the parameter set that the dynamic <c>-CaseSensitive</c> parameter belongs to.
	/// </summary>
	/// <value>The parameter set name for the <c>-CaseSensitive</c> switch.</value>
	protected abstract string CaseSensitiveParameterSetName { get; }

	/// <summary>
	/// Gets or sets the initial capacity requested for the collection.
	/// </summary>
	/// <remarks>
	/// <see cref="BeginCore"/> passes the value to the collection's constructor, so the collection doesn't have to
	/// grow until it holds more elements than this. Derived classes override the property to make it a parameter.
	/// </remarks>
	/// <value>The requested initial capacity.</value>
	public virtual int Capacity { get; set; }
	/// <summary>
	/// Gets a value that indicates whether the dynamic <c>-CaseSensitive</c> switch is set.
	/// </summary>
	/// <value><see langword="true"/> when <c>-CaseSensitive</c> is present and set; otherwise, <see langword="false"/>.</value>
	protected bool CaseSensitive => IsParameterValueCaseSensitive(_caseSensitive);
	/// <summary>
	/// Gets or sets the error action preference applied while custom equality script blocks run.
	/// </summary>
	/// <value>The error action preference for script block execution.</value>
	public virtual ActionPreference ScriptBlockErrorAction { get; set; }

	/// <summary>
	/// Returns the dynamic parameters for the current invocation.
	/// </summary>
	/// <remarks>
	/// The method rebuilds the dynamic parameter set on each call. It adds <c>-CaseSensitive</c> when the type used
	/// for equality is <see cref="string"/> or <see cref="object"/>, and then lets the derived class add its own
	/// parameters through <see cref="TryGetDynamicParameters(RuntimeDefinedParameterDictionary, bool)"/>.
	/// </remarks>
	/// <returns>A <see cref="RuntimeDefinedParameterDictionary"/> that contains the dynamic parameters, or <see langword="null"/> when there are none.</returns>
	public object? GetDynamicParameters()
	{
		this.DynParamLib.Clear();
		bool hasCase = this.TryGetDynamicCaseParam(this.GetEqualityForType(), this.CaseSensitiveParameterSetName);

		return this.TryGetDynamicParameters(this.DynParamLib, hasCase)
			? this.DynParamLib
			: null;
	}

	#region PROCESSING
	/// <summary>
	/// Constructs the collection and then calls <see cref="Begin(T, Type)"/>.
	/// </summary>
	/// <remarks>
	/// The method resolves the generic type arguments from <see cref="GetGenericTypes"/>, gets the equality comparer
	/// from <see cref="GetCustomEqualityComparer(Type)"/>, and gets an <see cref="EqualityCollectionCtor"/> from
	/// <see cref="GetConstructor(IEqualityComparer, Type[])"/>. It passes <see cref="Capacity"/> to that object and
	/// constructs the collection with it. It also prepares the invoker that
	/// <see cref="AddToCollection(T, object[], bool)"/> and
	/// <see cref="AddToCollection(T, object[], Func{object, Type[], object})"/> use to call the collection's
	/// <c>Add</c> method.
	/// </remarks>
	protected sealed override void BeginCore()
	{
		Type[]? genericTypes = this.GetGenericTypes();
		IEqualityComparer? comparer = this.GetCustomEqualityComparer(this.GetEqualityForType());

		var ctor = this.GetConstructor(comparer, genericTypes);
		ctor.Capacity = this.Capacity;
		_collection = (T)ctor.Construct();

		_collectionType = ctor.ConstructingGenericType;
		_genericTypes = ctor.GenericArgumentTypes;
		_addMethod = new AddMethodInvoker(ctor);

		this.Begin(_collection, _collectionType);
	}
	/// <summary>
	/// When overridden in a derived class, performs begin-phase work after the collection is constructed.
	/// </summary>
	/// <remarks>The base implementation does nothing.</remarks>
	/// <param name="collection">The newly constructed collection.</param>
	/// <param name="genericBaseType">The closed generic type of the constructed collection.</param>
	protected virtual void Begin(T collection, Type genericBaseType)
	{
		return;
	}

	/// <summary>
	/// Passes the constructed collection to <see cref="Process(T, Type)"/> for the current pipeline record.
	/// </summary>
	/// <returns>The value returned by <see cref="Process(T, Type)"/>.</returns>
	protected sealed override bool ProcessCore()
	{
		return this.Process(_collection, _collectionType);
	}
	/// <summary>
	/// When implemented in a derived class, adds the current pipeline input to the collection.
	/// </summary>
	/// <param name="collection">The collection to add input to.</param>
	/// <param name="collectionType">The closed generic type of the collection.</param>
	/// <returns><see langword="true"/> to continue processing pipeline input; <see langword="false"/> to stop.</returns>
	protected abstract bool Process(T collection, Type collectionType);

	/// <summary>
	/// Passes the constructed collection to <see cref="End(T, bool)"/>.
	/// </summary>
	/// <param name="state">The run state of the cmdlet. Its <see cref="CmdletRunState.FoundMatch"/> value indicates whether <see cref="Process(T, Type)"/> requested a stop.</param>
	protected sealed override void EndCore(CmdletRunState state)
	{
		this.End(_collection, state.FoundMatch);
	}
	/// <summary>
	/// When overridden in a derived class, completes the cmdlet, typically by writing the collection to the pipeline.
	/// </summary>
	/// <remarks>The base implementation does nothing.</remarks>
	/// <param name="collection">The constructed collection.</param>
	/// <param name="wantsToStop"><see langword="true"/> when <see cref="Process(T, Type)"/> returned <see langword="false"/> for a pipeline record; otherwise, <see langword="false"/>.</param>
	protected virtual void End(T collection, bool wantsToStop)
	{
		return;
	}

	#endregion

	#region BACKEND
	/// <summary>
	/// When implemented in a derived class, creates the constructor object that builds the collection.
	/// </summary>
	/// <param name="comparer">The equality comparer for the collection, or <see langword="null"/> to use the constructor's default.</param>
	/// <param name="genericTypes">The generic type arguments returned by <see cref="GetGenericTypes"/>, or <see langword="null"/> when none were resolved.</param>
	/// <returns>The <see cref="EqualityCollectionCtor"/> that constructs the collection.</returns>
	protected abstract EqualityCollectionCtor GetConstructor(IEqualityComparer? comparer, Type[]? genericTypes);

	/// <summary>
	/// When overridden in a derived class, adds the derived cmdlet's own dynamic parameters.
	/// </summary>
	/// <remarks>The base implementation adds nothing and returns <paramref name="hasCaseSensitive"/>.</remarks>
	/// <param name="paramDict">The dictionary to add dynamic parameters to.</param>
	/// <param name="hasCaseSensitive"><see langword="true"/> when <c>-CaseSensitive</c> was added to <paramref name="paramDict"/>; otherwise, <see langword="false"/>.</param>
	/// <returns><see langword="true"/> when <paramref name="paramDict"/> contains at least one dynamic parameter; otherwise, <see langword="false"/>.</returns>
	protected virtual bool TryGetDynamicParameters(RuntimeDefinedParameterDictionary paramDict, bool hasCaseSensitive)
	{
		return hasCaseSensitive;
	}
	/// <summary>
	/// Adds the mandatory <c>-CaseSensitive</c> switch to <see cref="DynParamLib"/> when the equality type is <see cref="string"/> or <see cref="object"/>.
	/// </summary>
	/// <remarks>The <see cref="RuntimeDefinedParameter"/> is created once and reused on later calls.</remarks>
	/// <param name="genericType">The type used for equality.</param>
	/// <param name="parameterSetName">The name of the parameter set that the switch belongs to.</param>
	/// <returns><see langword="true"/> when the switch was added; otherwise, <see langword="false"/>.</returns>
	private bool TryGetDynamicCaseParam(Type genericType, string parameterSetName)
	{
		bool returnLib = false;
		if (EqualityCollectionCtor.IsTypeObjectOrString(genericType))
		{
			_caseSensitive ??= new RuntimeDefinedParameter(CASE_SENSE, typeof(SwitchParameter),
				new Collection<Attribute>()
				{
						new ParameterAttribute()
						{
							Mandatory = true,
							ParameterSetName = parameterSetName,
						}
				});

			returnLib = this.DynParamLib.TryAdd(CASE_SENSE, _caseSensitive);
		}

		return returnLib;
	}

	/// <summary>
	/// Converts the specified arguments and passes them to the collection's <c>Add</c> method.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The method does nothing when <paramref name="collection"/> or <paramref name="items"/> is
	/// <see langword="null"/>, when <paramref name="items"/> is empty, or when its first element is
	/// <see langword="null"/>. Each element of <paramref name="items"/> is replaced in place with the result of
	/// <paramref name="conversion"/>.
	/// </para>
	/// <para>
	/// When any converted argument is <see langword="null"/>, the call to <c>Add</c> is skipped without an error. When
	/// <c>Add</c> throws, the method writes a non-terminating error instead of throwing. Exceptions thrown by
	/// <paramref name="conversion"/> propagate to the caller.
	/// </para>
	/// </remarks>
	/// <param name="collection">The collection to add to.</param>
	/// <param name="items">The arguments for the <c>Add</c> method, in parameter order. The array is modified in place.</param>
	/// <param name="conversion">A function that receives an argument and the collection's generic type arguments and returns the converted argument.</param>
	protected void AddToCollection(T collection, object?[]? items, Func<object?, Type[], object?> conversion)
	{
		if (collection is null || items is null || items.Length < 1 || items[0] is null)
		{
			return;
		}

		for (int i = items.Length - 1; i >= 0; i--)
		{
			items[i] = conversion(items[i], _genericTypes);
		}

		if (!_addMethod.TryInvoke(collection, items, false, out Exception? caughtEx))
		{
			this.WriteError(caughtEx.ToRecord(ErrorCategory.InvalidOperation, items));
		}
	}
	/// <summary>
	/// Passes the specified arguments to the collection's <c>Add</c> method.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The method does nothing when <paramref name="collection"/> is <see langword="null"/>, or when
	/// <paramref name="item"/> is <see langword="null"/> and <paramref name="addIfNull"/> is <see langword="false"/>.
	/// When <c>Add</c> throws, the method writes a non-terminating error instead of throwing.
	/// </para>
	/// <para>
	/// When any argument is <see langword="null"/>, the call to <c>Add</c> is skipped without an error.
	/// </para>
	/// <para>TODO: Because null arguments are always skipped, <paramref name="addIfNull"/> currently has no observable effect.</para>
	/// </remarks>
	/// <param name="collection">The collection to add to.</param>
	/// <param name="item">The arguments for the <c>Add</c> method, in parameter order, or <see langword="null"/>.</param>
	/// <param name="addIfNull"><see langword="true"/> to substitute a single <see langword="null"/> argument when <paramref name="item"/> is <see langword="null"/>; otherwise, <see langword="false"/>.</param>
	protected void AddToCollection(T collection, object?[]? item, bool addIfNull)
	{
		if (collection is null || (item is null && !addIfNull))
		{
			return;
		}

		item ??= new object?[] { null };

		if (!_addMethod.TryInvoke(collection, item, false, out Exception? caughtEx))
		{
			this.WriteError(caughtEx.ToRecord(ErrorCategory.InvalidOperation, item));
		}
	}

	/// <summary>
	/// Returns the equality comparer to construct the collection with.
	/// </summary>
	/// <remarks>
	/// For <see cref="string"/> elements, the base implementation returns <see cref="StringComparer.CurrentCulture"/>
	/// when <c>-CaseSensitive</c> is set and <see cref="StringComparer.OrdinalIgnoreCase"/> otherwise. For any other
	/// type it returns <see langword="null"/>, and the collection constructor chooses its default comparer.
	/// </remarks>
	/// <param name="genericType">The type used for equality.</param>
	/// <returns>The equality comparer to use, or <see langword="null"/> to use the constructor's default.</returns>
	protected virtual IEqualityComparer? GetCustomEqualityComparer(Type genericType)
	{
		if (!typeof(string).Equals(genericType))
			return null;

		return IsParameterValueCaseSensitive(_caseSensitive)
			? StringComparer.CurrentCulture
			: StringComparer.OrdinalIgnoreCase;
	}
	/// <summary>
	/// When implemented in a derived class, returns the generic type arguments for the collection.
	/// </summary>
	/// <returns>The generic type arguments, or <see langword="null"/> to let the collection constructor use its defaults.</returns>
	protected abstract Type[]? GetGenericTypes();
	/// <summary>
	/// When implemented in a derived class, returns the type whose values the collection compares for equality.
	/// </summary>
	/// <returns>The element type for sets, or the key type for dictionaries.</returns>
	protected abstract Type GetEqualityForType();

	/// <summary>
	/// Determines whether the specified dynamic switch parameter is set.
	/// </summary>
	/// <param name="parameter">The dynamic parameter to inspect, or <see langword="null"/>.</param>
	/// <returns><see langword="true"/> when <paramref name="parameter"/> has a value that PowerShell treats as <see langword="true"/>; otherwise, <see langword="false"/>.</returns>
	private static bool IsParameterValueCaseSensitive(RuntimeDefinedParameter? parameter)
	{
		return LanguagePrimitives.IsTrue(parameter?.Value);
	}

	#endregion
}
