using ListFunctions.Modern;
using ListFunctions.Modern.Constructors;
using ListFunctions.Modern.Variables;
using ListFunctions.Validation;

#nullable enable

namespace ListFunctions.Cmdlets.Constructs;

/// <summary>
/// Creates a new <see cref="Dictionary{TKey, TValue}"/> and optionally copies entries into it from a hashtable.
/// </summary>
/// <remarks>
/// <para>
/// The cmdlet always writes a <see cref="Dictionary{TKey, TValue}"/> closed over <see cref="KeyType"/> and
/// <see cref="ValueType"/>, which are both <see cref="object"/> by default.
/// </para>
/// <para>
/// When <see cref="KeyType"/> is <see cref="string"/>, keys compare with
/// <see cref="StringComparer.OrdinalIgnoreCase"/>, or with <see cref="StringComparer.Ordinal"/> when
/// <c>-CaseSensitive</c> is set. <see cref="object"/> keys compare with the same comparer, whatever
/// <see cref="ValueType"/> is: two strings the same way as <see cref="string"/> keys, and any other two keys with their
/// own <see cref="object.Equals(object)"/> method, so <c>1</c> and <c>"1"</c> are different keys.
/// <see cref="EqualityScript"/> and <see cref="HashCodeScript"/> replace the default key comparison with PowerShell
/// script blocks.
/// </para>
/// <para>
/// Errors from <see cref="EqualityScript"/> and <see cref="HashCodeScript"/> reach PowerShell unchanged, the way they do
/// from a <c>ForEach-Object</c> script block, and the cmdlet then writes no dictionary. When
/// <see cref="ScriptBlockErrorAction"/> is <see cref="ActionPreference.Stop"/>, the default, an error that a script
/// block writes ends the script that runs the cmdlet, as <c>-ErrorAction Stop</c> does. A <c>throw</c> does too unless
/// the errors are suppressed. A failed method call, and output of <see cref="HashCodeScript"/> that isn't a hash code,
/// end only the statement, and <c>break</c> leaves the loop around the cmdlet.
/// </para>
/// <para>
/// The dictionary is written as a single object and is not enumerated into the pipeline.
/// </para>
/// </remarks>
[Cmdlet(VerbsCommon.New, "Dictionary", DefaultParameterSetName = "None")]
[OutputType(typeof(Dictionary<object, object>))]
public sealed class NewDictionaryCmdlet : EqualityConstructingCmdlet<IDictionary>, IDynamicParameters
{
	private const string CLONE_VALUES = "CloneValues";
	private const string STR_DICT = "StringDict";

	/// <inheritdoc/>
	protected override string CaseSensitiveParameterSetName => STR_DICT;

	/// <summary>
	/// Gets the name of the parameter set that copies <see cref="InputObject"/> without custom equality, in which
	/// <c>-CaseSensitive</c> is optional.
	/// </summary>
	/// <remarks>
	/// The mandatory <see cref="InputObject"/> tells that set apart, so <c>-CaseSensitive</c> can be combined with
	/// <see cref="InputObject"/> and <see cref="CloneValues"/>, from the pipeline or as an argument.
	/// </remarks>
	/// <value>The name of the parameter set that copies entries without custom equality.</value>
	protected override string? CaseSensitiveOptionalParameterSetName => JUST_COPY;

	/// <summary>
	/// Gets or sets the initial capacity requested for the dictionary.
	/// </summary>
	/// <remarks>
	/// The dictionary is created with room for this many entries, so it doesn't have to grow until it holds more.
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
	/// as <c>{ [int] }</c>. Keys copied from <see cref="InputObject"/> are converted to this type, and a key that
	/// can't be converted produces a non-terminating error for its entry.
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
	/// as <c>{ [int] }</c>. Values copied from <see cref="InputObject"/> are converted to this type, and a value that
	/// can't be converted produces a non-terminating error for its entry.
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
	/// <para>
	/// Each key is converted to <see cref="KeyType"/>, and each value to <see cref="ValueType"/>. A
	/// <see langword="null"/> value is converted too, so it's stored as what the dictionary's <c>Add</c> method stores
	/// for it when PowerShell calls the method: <see langword="null"/> for <see cref="object"/>, an empty string for
	/// <see cref="string"/>, and 0 for <see cref="int"/>. An entry whose key or value can't be converted, or that can't
	/// be added, such as a duplicate key, produces a non-terminating error.
	/// </para>
	/// <para>
	/// The hashtable comes from the pipeline or from the parameter. The parameter can't be combined with pipeline input.
	/// </para>
	/// </remarks>
	/// <value>The source <see cref="Hashtable"/>.</value>
	[Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = JUST_COPY)]
	[Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = AND_COPY)]
	[Alias("CopyFrom")]
	public IDictionary InputObject { get; set; } = null!;

	/// <summary>
	/// Gets or sets the script block that determines whether two keys are equal.
	/// </summary>
	/// <remarks>
	/// The script block receives the two keys as <c>$x</c> and <c>$y</c>, as <c>$left</c> and <c>$right</c>, and as its
	/// two arguments, in order: <c>$args[0]</c> and <c>$args[1]</c>, or the parameters of its <c>param()</c> block. It
	/// must reference one variable for each key. Parameter validation also rejects a script block that the cmdlet can't
	/// run, such as one that has a <c>begin</c> block. Its output is converted to a <see cref="bool"/> by using
	/// PowerShell's truthiness rules.
	/// </remarks>
	/// <value>The key equality <see cref="ScriptBlock"/>.</value>
	[Parameter(Mandatory = true, ParameterSetName = WITH_CUSTOM_EQUALITY)]
	[Parameter(Mandatory = true, ParameterSetName = AND_COPY)]
	[IsScriptBlock]
	[ValidateScriptVariable(PSComparingVariable.X, PSComparingVariable.LEFT, PSThisVariable.FirstArg)]
	[ValidateScriptVariable(PSComparingVariable.Y, PSComparingVariable.RIGHT, PSThisVariable.SecondArg)]
	public ScriptBlock EqualityScript { get; set; } = null!;

	/// <summary>
	/// Gets or sets the script block that computes the hash code of a key.
	/// </summary>
	/// <remarks>
	/// The script block receives the key as <c>$_</c>, <c>$this</c>, and <c>$PSItem</c>, and as its first argument:
	/// <c>$args[0]</c>, or the first parameter of its <c>param()</c> block. It must reference at least one of them.
	/// Parameter validation also rejects a script block that the cmdlet can't run, such as one that has a <c>begin</c>
	/// block. Its first output is converted to an <see cref="int"/>. Keys that <see cref="EqualityScript"/> considers
	/// equal must produce the same hash code.
	/// </remarks>
	/// <value>The key hash code <see cref="ScriptBlock"/>.</value>
	[Parameter(Mandatory = true, ParameterSetName = WITH_CUSTOM_EQUALITY)]
	[Parameter(Mandatory = true, ParameterSetName = AND_COPY)]
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
	[Parameter(ParameterSetName = AND_COPY)]
	[Alias("ScriptErrorAction")]
	[PSDefaultValue(Value = ActionPreference.Stop)]
	public override ActionPreference ScriptBlockErrorAction { get; set; } = ActionPreference.Stop;

	/// <summary>
	/// Copies the entries of <see cref="InputObject"/> into the dictionary.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Each key is converted to <see cref="KeyType"/>. Each value is cloned when <see cref="CloneValues"/> is set, and
	/// is then converted to <see cref="ValueType"/> unless that type is <see cref="object"/>. A <see langword="null"/>
	/// value is converted like any other value, so <see cref="string"/> values store an empty string for it, and
	/// <see cref="int"/> values store 0.
	/// </para>
	/// <para>
	/// A key or value that can't be converted produces a non-terminating error, and its entry is skipped. That includes a
	/// <see langword="null"/> value when <see cref="ValueType"/> is a value type that can't hold
	/// <see langword="null"/>, such as <see cref="DateTime"/>. An entry that can't be added, such as one whose converted
	/// key is already in the dictionary, produces a non-terminating error for the exception that the dictionary threw,
	/// with the converted key as its target. Either way, the remaining entries are still copied.
	/// </para>
	/// <para>
	/// An error from <see cref="EqualityScript"/> or <see cref="HashCodeScript"/>, including one for output that isn't a
	/// hash code, reaches PowerShell unchanged instead, so the cmdlet ends without writing a dictionary.
	/// </para>
	/// </remarks>
	/// <param name="collection">The dictionary to copy entries into.</param>
	/// <param name="collectionType">The closed generic type of the dictionary.</param>
	/// <exception cref="RuntimeException">Thrown when adding an entry throws one, for example because <see cref="HashCodeScript"/> fails.</exception>
	/// <exception cref="FlowControlException">Thrown when adding an entry throws one, for example because <see cref="HashCodeScript"/> runs <c>break</c>.</exception>
	protected override void Process(IDictionary collection, Type collectionType)
	{
		if (null != this.InputObject && this.InputObject.Count > 0)
		{
			Type keyType = this.KeyType ?? typeof(object);
			Type valueType = this.ValueType ?? typeof(object);
			bool convertValues = !typeof(object).Equals(valueType);

			object?[] args = new object?[2];
			foreach (DictionaryEntry de in this.InputObject)
			{
				if (!this.TryConvertItem(de.Key, keyType, out object? key))
				{
					continue;
				}

				object? value = CloneValue(de.Value, this.CloneValues);
				if (convertValues && !this.TryConvertItem(value, valueType, out value))
				{
					continue;
				}

				args[0] = key;
				args[1] = value;
				this.AddToCollection(collection, args);
			}
		}
	}
	/// <summary>
	/// Writes the dictionary to the pipeline as a single object.
	/// </summary>
	/// <param name="collection">The constructed dictionary.</param>
	protected override void End(IDictionary collection)
	{
		this.WriteObject(collection, false);
	}

	#region BACKEND
	/// <summary>
	/// Creates a <see cref="DictionaryCtor"/> for <see cref="KeyType"/> and <see cref="ValueType"/>.
	/// </summary>
	/// <param name="comparer">The key equality comparer, or <see langword="null"/> to use the default for the key type.</param>
	/// <param name="genericTypes">The generic type arguments. This implementation reads <see cref="KeyType"/> and <see cref="ValueType"/> instead.</param>
	/// <returns>A <see cref="DictionaryCtor"/> that honors the <c>-CaseSensitive</c> switch.</returns>
	private protected override EqualityCollectionCtor GetConstructor(IEqualityComparer? comparer, Type[]? genericTypes)
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
	/// Returns the key equality comparer, building one from <see cref="EqualityScript"/> and
	/// <see cref="HashCodeScript"/> in the custom equality parameter sets.
	/// </summary>
	/// <remarks>
	/// The custom equality parameter sets are the one without <see cref="InputObject"/> and the one with it. In any
	/// other parameter set, the method defers to the base implementation. The script-based comparer runs its script
	/// blocks with <c>$ErrorActionPreference</c> set to <see cref="ScriptBlockErrorAction"/>.
	/// </remarks>
	/// <param name="genericType">The key type.</param>
	/// <returns>The key equality comparer, or <see langword="null"/> to use the default for the key type.</returns>
	protected override IEqualityComparer? GetCustomEqualityComparer(Type genericType)
	{
		if (!WITH_CUSTOM_EQUALITY.Equals(this.ParameterSetName, StringComparison.OrdinalIgnoreCase)
			&& !AND_COPY.Equals(this.ParameterSetName, StringComparison.OrdinalIgnoreCase))
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
	/// <returns>An array that contains <see cref="KeyType"/> and <see cref="ValueType"/>.</returns>
	protected override Type[]? GetGenericTypes()
	{
		this.KeyType ??= typeof(object);
		this.ValueType ??= typeof(object);

		return [this.KeyType, this.ValueType];
	}

	#endregion
}
