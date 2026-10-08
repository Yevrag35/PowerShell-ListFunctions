using ListFunctions.Components;
using ListFunctions.Extensions;
using ListFunctions.Modern;
using ListFunctions.Modern.Constructors;

#nullable enable

namespace ListFunctions.Cmdlets.Constructs;

/// <summary>
/// Provides a base class for cmdlets that construct a generic collection whose elements or keys are compared for
/// equality.
/// </summary>
/// <remarks>
/// <para>
/// The begin phase is sealed. It resolves the collection's generic type arguments, chooses an equality comparer,
/// constructs the collection, and then calls <see cref="Begin(T, Type)"/>. Derived classes add pipeline input in
/// <see cref="Process(T, Type)"/> and write the finished collection in <see cref="End(T, bool)"/>. Only classes in this
/// assembly can derive from this class.
/// </para>
/// <para>
/// When the type used for equality is <see cref="string"/> or <see cref="object"/>, the cmdlet exposes a mandatory
/// dynamic <c>-CaseSensitive</c> switch in the parameter set named by <see cref="CaseSensitiveParameterSetName"/>, and
/// an optional one in the set named by <see cref="CaseSensitiveOptionalParameterSetName"/>, if any. Derived classes must
/// implement <see cref="IDynamicParameters"/> for PowerShell to call <see cref="GetDynamicParameters"/>.
/// </para>
/// </remarks>
/// <typeparam name="T">The type through which the derived cmdlet handles the constructed collection.</typeparam>
public abstract class EqualityConstructingCmdlet<T> : ListFunctionCmdletBase
{
	/// <summary>
	/// The name of the parameter set that copies existing entries without a custom equality comparer.
	/// </summary>
	protected const string JUST_COPY = "JustCopy";
	/// <summary>
	/// The name of the parameter set that copies existing entries and uses a custom equality comparer.
	/// </summary>
	protected const string AND_COPY = WITH_CUSTOM_EQUALITY + "AndCopy";

	private object?[]? _addArgs;
	private AddMethodInvoker _addMethod = null!;
	private RuntimeDefinedParameter _caseSensitive = null!;
	private T _collection = default!;
	private Type _collectionType = null!;
	private Type[] _genericTypes = null!;

	/// <summary>
	/// Gets the dictionary that holds the cmdlet's dynamic parameters, creating it on first access.
	/// </summary>
	/// <value>The <see cref="RuntimeDefinedParameterDictionary"/> that holds the dynamic parameters.</value>
	[MemberNotNullWhen(true, nameof(_addMethod))]
	private RuntimeDefinedParameterDictionary DynParamLib => field ??= [];
	/// <summary>
	/// Gets the name of the parameter set that the dynamic <c>-CaseSensitive</c> parameter belongs to.
	/// </summary>
	/// <remarks>
	/// The switch is mandatory in this parameter set, which tells the set apart from the cmdlet's other parameter sets.
	/// </remarks>
	/// <value>The parameter set name for the <c>-CaseSensitive</c> switch.</value>
	protected abstract string CaseSensitiveParameterSetName { get; }
	/// <summary>
	/// Gets the name of another parameter set that the dynamic <c>-CaseSensitive</c> parameter belongs to, as an
	/// optional parameter.
	/// </summary>
	/// <remarks>
	/// A parameter set that a mandatory parameter of its own already tells apart, such as one that copies existing
	/// entries, can offer the switch this way. The base implementation returns <see langword="null"/>.
	/// </remarks>
	/// <value>The name of the parameter set, or <see langword="null"/> when the switch belongs to no other set.</value>
	protected virtual string? CaseSensitiveOptionalParameterSetName => null;

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
	/// from <see cref="GetCustomEqualityComparer(Type)"/>, and constructs the collection with them, with
	/// <see cref="Capacity"/> as its initial capacity. It also prepares the invoker that
	/// <see cref="AddToCollection(T, object[])"/> and <see cref="AddToCollection(T, object)"/> use to call the
	/// collection's <c>Add</c> method.
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
	private protected sealed override void EndCore(CmdletRunState state)
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
	private protected abstract EqualityCollectionCtor GetConstructor(IEqualityComparer? comparer, Type[]? genericTypes);

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
	/// Adds the <c>-CaseSensitive</c> switch to <see cref="DynParamLib"/> when the equality type is <see cref="string"/> or <see cref="object"/>.
	/// </summary>
	/// <remarks>
	/// The switch is mandatory in <paramref name="parameterSetName"/>, and optional in
	/// <see cref="CaseSensitiveOptionalParameterSetName"/> when that isn't <see langword="null"/>. The
	/// <see cref="RuntimeDefinedParameter"/> is created once and reused on later calls.
	/// </remarks>
	/// <param name="genericType">The type used for equality.</param>
	/// <param name="parameterSetName">The name of the parameter set in which the switch is mandatory.</param>
	/// <returns><see langword="true"/> when the switch was added; otherwise, <see langword="false"/>.</returns>
	private bool TryGetDynamicCaseParam(Type genericType, string parameterSetName)
	{
		bool returnLib = false;
		if (EqualityCollectionCtor.IsTypeObjectOrString(genericType))
		{
			if (_caseSensitive is null)
			{
				var attributes = new Collection<Attribute>()
				{
					new ParameterAttribute()
					{
						Mandatory = true,
						ParameterSetName = parameterSetName,
					},
				};

				if (this.CaseSensitiveOptionalParameterSetName is string optionalSetName)
				{
					attributes.Add(new ParameterAttribute()
					{
						ParameterSetName = optionalSetName,
					});
				}

				_caseSensitive = new RuntimeDefinedParameter(CASE_SENSE, typeof(SwitchParameter), attributes);
			}

			returnLib = this.DynParamLib.TryAdd(CASE_SENSE, _caseSensitive);
		}

		return returnLib;
	}

	/// <summary>
	/// Converts the specified item to the collection's element type and passes it to the collection's <c>Add</c>
	/// method.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The method is for collections whose <c>Add</c> method takes a single argument, such as sets. The element type is
	/// the collection's first generic type argument. The method does nothing when <paramref name="collection"/> or
	/// <paramref name="item"/> is <see langword="null"/>, or when <paramref name="item"/> converts to
	/// <see langword="null"/>. An item that can't be converted produces the non-terminating error that <c>New-List</c>
	/// writes.
	/// </para>
	/// <para>
	/// When <c>Add</c> throws a <see cref="RuntimeException"/> or a <see cref="FlowControlException"/>, such as an error
	/// from a script block that compares the elements, the method throws it again unchanged, so it reaches PowerShell the
	/// way an error from a <c>ForEach-Object</c> script block does. Any other exception from <c>Add</c>, such as one from
	/// an element type's own <see cref="object.GetHashCode"/> method, produces a non-terminating error instead. The
	/// error's target object is <paramref name="item"/> as it was before conversion.
	/// </para>
	/// <para>
	/// <b>Performance:</b> The method reuses one argument array for every call, so it doesn't allocate an array for
	/// each item. For the same reason, it isn't thread-safe.
	/// </para>
	/// </remarks>
	/// <param name="collection">The collection to add to.</param>
	/// <param name="item">The item to convert and add, or <see langword="null"/>.</param>
	/// <exception cref="RuntimeException">Thrown when <c>Add</c> throws one, for example because a script block that compares the elements fails.</exception>
	/// <exception cref="FlowControlException">Thrown when <c>Add</c> throws one, for example because a script block that compares the elements runs <c>break</c>.</exception>
	protected void AddToCollection(T collection, object? item)
	{
		if (collection is null || item is null || !this.TryConvertItem(item, _genericTypes[0], out object? converted))
		{
			return;
		}

		object?[] args = _addArgs ??= new object?[1];
		args[0] = converted;

		if (!_addMethod.TryInvoke(collection, args, addIfNull: false, out Exception? caughtEx))
		{
			RethrowIfPassesThrough(caughtEx);
			this.WriteError(caughtEx.ToRecord(ErrorCategory.InvalidOperation, item));
		}
	}
	/// <summary>
	/// Passes the specified arguments to the collection's <c>Add</c> method.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Every call reaches <c>Add</c>, even when an argument is <see langword="null"/>, so the collection decides whether
	/// it accepts <see langword="null"/>. For example, a dictionary stores a <see langword="null"/> value when its value
	/// type can hold one, and it rejects a <see langword="null"/> key. The method does nothing when
	/// <paramref name="collection"/> is <see langword="null"/>.
	/// </para>
	/// <para>
	/// When <c>Add</c> throws a <see cref="RuntimeException"/> or a <see cref="FlowControlException"/>, such as an error
	/// from a script block that compares the keys, the method throws it again unchanged, so it reaches PowerShell the way
	/// an error from a <c>ForEach-Object</c> script block does. Any other exception from <c>Add</c>, such as the one for a
	/// duplicate key, produces a non-terminating error instead. The error's target object is the first argument, such as
	/// a dictionary's key.
	/// </para>
	/// </remarks>
	/// <param name="collection">The collection to add to.</param>
	/// <param name="arguments">The arguments for the <c>Add</c> method, in parameter order. This value must not be <see langword="null"/>.</param>
	/// <exception cref="RuntimeException">Thrown when <c>Add</c> throws one, for example because a script block that compares the keys fails.</exception>
	/// <exception cref="FlowControlException">Thrown when <c>Add</c> throws one, for example because a script block that compares the keys runs <c>break</c>.</exception>
	protected void AddToCollection(T collection, object?[] arguments)
	{
		if (collection is null)
		{
			return;
		}

		if (!_addMethod.TryInvoke(collection, arguments, addIfNull: true, out Exception? caughtEx))
		{
			RethrowIfPassesThrough(caughtEx);

			// The caller can reuse the array for its next entry, so the record keeps the first argument instead.
			object? target = arguments.Length > 0 ? arguments[0] : null;
			this.WriteError(caughtEx.ToRecord(ErrorCategory.InvalidOperation, target));
		}
	}

	/// <summary>
	/// Returns the equality comparer to construct the collection with.
	/// </summary>
	/// <remarks>
	/// For <see cref="string"/> elements, the base implementation returns <see cref="StringComparer.Ordinal"/> when
	/// <c>-CaseSensitive</c> is set and <see cref="StringComparer.OrdinalIgnoreCase"/> otherwise. Both comparisons are
	/// ordinal, so <c>-CaseSensitive</c> changes only whether case matters. For any other type the method returns
	/// <see langword="null"/>, and the collection constructor chooses its default comparer.
	/// </remarks>
	/// <param name="genericType">The type used for equality.</param>
	/// <returns>The equality comparer to use, or <see langword="null"/> to use the constructor's default.</returns>
	protected virtual IEqualityComparer? GetCustomEqualityComparer(Type genericType)
	{
		if (!typeof(string).Equals(genericType))
			return null;

		return IsParameterValueCaseSensitive(_caseSensitive)
			? StringComparer.Ordinal
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
