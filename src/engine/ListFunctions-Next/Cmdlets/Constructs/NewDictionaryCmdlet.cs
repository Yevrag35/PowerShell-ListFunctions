using ListFunctions.Modern;
using ListFunctions.Modern.Constructors;
using ListFunctions.Modern.Variables;
using ListFunctions.Validation;

#nullable enable

namespace ListFunctions.Cmdlets.Constructs;

/// <summary>
/// Creates a new <see cref="Dictionary{TKey, TValue}"/>, or a <see cref="Hashtable"/>, and optionally copies entries
/// into it from a hashtable.
/// </summary>
/// <remarks>
/// <para>
/// When both <see cref="KeyType"/> and <see cref="ValueType"/> are <see cref="object"/> and no custom equality
/// script blocks are supplied, the cmdlet writes a <see cref="Hashtable"/>. Its keys compare case-insensitively
/// unless <c>-CaseSensitive</c> is set. Otherwise, it writes a <see cref="Dictionary{TKey, TValue}"/> closed over
/// the key and value types.
/// </para>
/// <para>
/// When <see cref="KeyType"/> is <see cref="string"/>, keys compare with
/// <see cref="StringComparer.OrdinalIgnoreCase"/>, or with <see cref="StringComparer.CurrentCulture"/> when
/// <c>-CaseSensitive</c> is set. <see cref="EqualityScript"/> and <see cref="HashCodeScript"/> replace the default
/// key comparison with PowerShell script blocks.
/// </para>
/// <para>
/// The dictionary is written as a single object and is not enumerated into the pipeline.
/// </para>
/// </remarks>
[Cmdlet(VerbsCommon.New, "Dictionary", DefaultParameterSetName = "None")]
[OutputType(typeof(Dictionary<,>), typeof(Hashtable))]
public sealed class NewDictionaryCmdlet : EqualityConstructingCmdlet<IDictionary>, IDynamicParameters
{
	private const string CLONE_VALUES = "CloneValues";
	private const string STR_DICT = "StringDict";

	/// <inheritdoc/>
	protected override string CaseSensitiveParameterSetName => STR_DICT;

	/// <summary>
	/// Gets or sets the initial capacity requested for the dictionary.
	/// </summary>
	/// <remarks>
	/// The dictionary, or the <see cref="Hashtable"/>, is created with room for this many entries, so it doesn't have to
	/// grow until it holds more.
	/// </remarks>
	/// <value>The requested initial capacity, from 0 through <see cref="int.MaxValue"/>. Defaults to 0.</value>
	[Parameter, Alias("Size")]
	[ValidateRange(0, int.MaxValue)]
	[PSDefaultValue(Value = 0)]
	public override int Capacity
	{
		get => base.Capacity;
		set => base.Capacity = value;
	}

	/// <summary>
	/// Gets or sets a value that indicates whether values copied from <see cref="InputObject"/> are cloned.
	/// </summary>
	/// <remarks>
	/// A value that implements <see cref="ICloneable"/> is replaced with the result of
	/// <see cref="ICloneable.Clone"/>, and a <see cref="PSObject"/> is replaced with the result of
	/// <see cref="PSObject.Copy"/>. Other values are copied by reference.
	/// </remarks>
	/// <value><see langword="true"/> to clone copied values; otherwise, <see langword="false"/>.</value>
	[Parameter(ParameterSetName = JUST_COPY)]
	[Parameter(ParameterSetName = AND_COPY)]
	public SwitchParameter CloneValues { get; set; }

	/// <summary>
	/// Gets or sets the type of the dictionary's keys.
	/// </summary>
	/// <remarks>
	/// The parameter accepts a <see cref="Type"/>, a type name, or a script block that contains a type literal such
	/// as <c>{ [int] }</c>. Keys copied from <see cref="InputObject"/> are converted to this type.
	/// </remarks>
	/// <value>The key type. When not specified, the cmdlet uses <see cref="object"/>.</value>
	[Parameter(Position = 0)]
	[ArgumentToTypeTransform]
	public Type? KeyType { get; set; } = null!;

	/// <summary>
	/// Gets or sets the type of the dictionary's values.
	/// </summary>
	/// <remarks>
	/// The parameter accepts a <see cref="Type"/>, a type name, or a script block that contains a type literal such
	/// as <c>{ [int] }</c>. Values copied from <see cref="InputObject"/> are not converted, so each value must already
	/// be assignable to this type or the cmdlet writes a non-terminating error for that entry.
	/// </remarks>
	/// <value>The value type. Defaults to <see cref="object"/>.</value>
	[Parameter(Position = 1)]
	[ArgumentToTypeTransform]
	[PSDefaultValue(Value = typeof(object))]
	public Type? ValueType { get; set; } = null!;

	/// <summary>
	/// Gets or sets the hashtable whose entries are copied into the new dictionary. The value is accepted from the
	/// pipeline.
	/// </summary>
	/// <remarks>
	/// Each key is converted to <see cref="KeyType"/>. Entries whose value is <see langword="null"/> are skipped, and
	/// an entry that cannot be added, such as a duplicate key, produces a non-terminating error.
	/// </remarks>
	/// <value>The source <see cref="Hashtable"/>.</value>
	[Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = JUST_COPY)]
	[Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = AND_COPY)]
	[Alias("CopyFrom")]
	public Hashtable InputObject { get; set; } = null!;

	/// <summary>
	/// Gets or sets the script block that determines whether two keys are equal.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The script block receives the two keys as <c>$x</c> and <c>$y</c>, or as <c>$left</c> and <c>$right</c>. It
	/// must reference one variable for each key. Its output is converted to a <see cref="bool"/> by using
	/// PowerShell's truthiness rules.
	/// </para>
	/// <para>TODO: The script-based comparer is built only in the custom equality parameter set. When <see cref="InputObject"/> is also supplied, this script block and <see cref="HashCodeScript"/> are currently ignored.</para>
	/// </remarks>
	/// <value>The key equality <see cref="ScriptBlock"/>.</value>
	[Parameter(Mandatory = true, ParameterSetName = WITH_CUSTOM_EQUALITY)]
	[Parameter(Mandatory = true, ParameterSetName = AND_COPY)]
	[ValidateScriptVariable(PSComparingVariable.X, PSComparingVariable.LEFT)]
	[ValidateScriptVariable(PSComparingVariable.Y, PSComparingVariable.RIGHT)]
	public ScriptBlock EqualityScript { get; set; } = null!;

	/// <summary>
	/// Gets or sets the script block that computes the hash code of a key.
	/// </summary>
	/// <remarks>
	/// The script block receives the key as <c>$_</c>, <c>$this</c>, or <c>$PSItem</c> and must reference at least
	/// one of them. Keys that <see cref="EqualityScript"/> considers equal must produce the same hash code.
	/// </remarks>
	/// <value>The key hash code <see cref="ScriptBlock"/>.</value>
	[Parameter(Mandatory = true, ParameterSetName = WITH_CUSTOM_EQUALITY)]
	[Parameter(Mandatory = true, ParameterSetName = AND_COPY)]
	[ValidateScriptVariable(PSThisVariable.Underscore, PSThisVariable.This, PSThisVariable.PSItem)]
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

	/// <summary>
	/// Copies the entries of <see cref="InputObject"/> into the dictionary.
	/// </summary>
	/// <remarks>
	/// Each key is converted to <see cref="KeyType"/>, and each value is cloned when <see cref="CloneValues"/> is set.
	/// A key that cannot be converted throws, which ends the cmdlet with a terminating error.
	/// </remarks>
	/// <param name="collection">The dictionary to copy entries into.</param>
	/// <param name="collectionType">The closed generic type of the dictionary.</param>
	/// <returns>Always <see langword="true"/>, so all pipeline input is processed.</returns>
	protected override bool Process(IDictionary collection, Type collectionType)
	{
		if (null != this.InputObject && this.InputObject.Count > 0)
		{
			object?[] args = new object?[2];
			foreach (DictionaryEntry de in this.InputObject)
			{
				args[0] = LanguagePrimitives.ConvertTo(de.Key, this.KeyType);
				args[1] = CloneValue(de.Value, this.CloneValues);

				this.AddToCollection(collection, args, false);
			}
		}

		return true;
	}
	/// <summary>
	/// Writes the dictionary to the pipeline as a single object.
	/// </summary>
	/// <param name="collection">The constructed dictionary.</param>
	/// <param name="wantsToStop"><see langword="true"/> to skip writing the dictionary; otherwise, <see langword="false"/>.</param>
	protected override void End(IDictionary collection, bool wantsToStop)
	{
		if (wantsToStop)
			return;

		this.WriteObject(collection, false);
	}

	#region BACKEND
	/// <summary>
	/// Creates a <see cref="DictionaryCtor"/> for <see cref="KeyType"/> and <see cref="ValueType"/>.
	/// </summary>
	/// <param name="comparer">The key equality comparer, or <see langword="null"/> to use the default for the key type.</param>
	/// <param name="genericTypes">The generic type arguments. This implementation reads <see cref="KeyType"/> and <see cref="ValueType"/> instead.</param>
	/// <returns>A <see cref="DictionaryCtor"/> that honors the <c>-CaseSensitive</c> switch.</returns>
	protected override EqualityCollectionCtor GetConstructor(IEqualityComparer? comparer, Type[]? genericTypes)
	{
		return new DictionaryCtor(comparer, this.KeyType, this.ValueType)
		{
			IsCaseSensitive = this.CaseSensitive,
		};
	}

	/// <summary>
	/// Returns a clone of the specified value when cloning is requested.
	/// </summary>
	/// <param name="value">The value to clone, or <see langword="null"/>.</param>
	/// <param name="wantsCloning"><see langword="true"/> to clone <paramref name="value"/>; otherwise, <see langword="false"/>.</param>
	/// <returns>
	/// The result of <see cref="ICloneable.Clone"/> or <see cref="PSObject.Copy"/> when <paramref name="wantsCloning"/>
	/// is <see langword="true"/> and <paramref name="value"/> supports it; otherwise, <paramref name="value"/> itself.
	/// </returns>
	[return: NotNullIfNotNull(nameof(value))]
	private static object? CloneValue(object? value, bool wantsCloning)
	{
		if (!wantsCloning)
		{
			return value;
		}

		return value switch
		{
			ICloneable cloneable => cloneable.Clone(),
			PSObject pso => pso.Copy(),
			_ => value,
		};
	}

	/// <summary>
	/// Gets the public instance <c>Add</c> method of the specified closed dictionary type.
	/// </summary>
	/// <remarks>The method is not currently called.</remarks>
	/// <param name="genericBaseType">A closed generic dictionary type whose generic arguments are the key and value types.</param>
	/// <returns>The <see cref="MethodInfo"/> for <c>Add(TKey, TValue)</c>.</returns>
	private static MethodInfo GetAddMethod(Type genericBaseType)
	{
		return genericBaseType.GetMethod(nameof(Dictionary<object, object>.Add),
			bindingAttr: BindingFlags.Public | BindingFlags.Instance,
			binder: null,
			types: genericBaseType.GetGenericArguments(),
			modifiers: null)!;
	}
	/// <summary>
	/// Returns the key equality comparer, building one from <see cref="EqualityScript"/> and
	/// <see cref="HashCodeScript"/> in the custom equality parameter set.
	/// </summary>
	/// <remarks>
	/// In any other parameter set, the method defers to the base implementation. The script-based comparer runs its
	/// script blocks with <c>$ErrorActionPreference</c> set to <see cref="ScriptBlockErrorAction"/>.
	/// </remarks>
	/// <param name="genericType">The key type.</param>
	/// <returns>The key equality comparer, or <see langword="null"/> to use the default for the key type.</returns>
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
	/// Returns <see cref="KeyType"/>, setting it to <see cref="object"/> when it is <see langword="null"/>.
	/// </summary>
	/// <returns>The key type.</returns>
	protected override Type GetEqualityForType()
	{
		return this.KeyType ??= typeof(object);
	}
	/// <summary>
	/// Returns the key and value types, setting each to <see cref="object"/> when it is <see langword="null"/>.
	/// </summary>
	/// <returns>An array that contains <see cref="KeyType"/> and <see cref="ValueType"/>, or <see langword="null"/> when both are <see cref="object"/>.</returns>
	protected override Type[]? GetGenericTypes()
	{
		Type objType = typeof(object);
		this.KeyType ??= objType;
		this.ValueType ??= objType;

		return !objType.Equals(this.KeyType) || !objType.Equals(this.ValueType)
			? new Type[] { this.KeyType, this.ValueType }
			: null;
	}
	/// <summary>
	/// Gets the <see cref="MethodInfo"/> of the <see cref="Hashtable"/> method called in the specified expression.
	/// </summary>
	/// <remarks>The method is not currently called.</remarks>
	/// <param name="addExpression">An expression whose body is a single method call on a <see cref="Hashtable"/>.</param>
	/// <returns>The <see cref="MethodInfo"/> of the called method.</returns>
	/// <exception cref="ArgumentException">Thrown when the body of <paramref name="addExpression"/> is not a method call.</exception>
	private static MethodInfo GetHashtableAddMethod(Expression<Action<Hashtable>> addExpression)
	{
		return addExpression.Body is MethodCallExpression methodCall
			? methodCall.Method
			: throw new ArgumentException("What the hell? That's not a method call...");
	}

	#endregion
}
