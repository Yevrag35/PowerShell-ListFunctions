using ListFunctions.Components;
using ListFunctions.Exceptions;
using ListFunctions.Extensions;
using ListFunctions.Modern;
using ListFunctions.Modern.Variables;
using ListFunctions.Validation;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ZLinq;

using PSAllowNull = System.Management.Automation.AllowNullAttribute;

#nullable enable

namespace ListFunctions.Cmdlets.Constructs;

/// <summary>
/// Converts input objects into a <see cref="Dictionary{TKey, TValue}"/> whose keys and values are selected from each
/// object.
/// </summary>
/// <remarks>
/// <para>
/// Each object's key comes from the property named by <see cref="KeyPropertyName"/> or from the output of
/// <see cref="KeySelector"/>. Its value comes from the property named by <see cref="ValuePropertyName"/>, from the
/// output of <see cref="ValueSelector"/>, or, when neither is supplied, from the object itself.
/// </para>
/// <para>
/// The key type is inferred from the key of the first input object. The value type is <see cref="ValueType"/>, or
/// is inferred from the value of the first input object. When an inferred type is a PowerShell custom object, or
/// the first key or value is <see langword="null"/>, <see cref="object"/> is used. Later keys and values are
/// converted to these types.
/// </para>
/// <para>
/// <see cref="DuplicateKeyBehavior"/> controls what happens when a key repeats. <see cref="string"/> keys compare
/// with <see cref="StringComparer.OrdinalIgnoreCase"/> unless <see cref="KeyComparer"/> is supplied.
/// </para>
/// <para>
/// The dictionary is written as a single object and is not enumerated into the pipeline. When no input objects are
/// received, the cmdlet writes an empty, case-insensitive <see cref="Hashtable"/>.
/// </para>
/// </remarks>
[Cmdlet(VerbsData.ConvertTo, "Dictionary", DefaultParameterSetName = "None")]
public sealed class ConvertToDictionaryCmdlet : ListFunctionCmdletBase
{
	/// <summary>
	/// Gets or sets the objects to convert. The value is accepted from the pipeline.
	/// </summary>
	/// <remarks>
	/// <see langword="null"/> elements are skipped, as are elements whose key is <see langword="null"/>.
	/// </remarks>
	/// <value>The objects to convert, or <see langword="null"/> when no objects are supplied.</value>
	[Parameter(Mandatory = true, ValueFromPipeline = true)]
	[AllowEmptyCollection, PSAllowNull, AllowEmptyString]
	public object?[]? InputObject { get; set; }

	/// <summary>
	/// Gets or sets the behavior to use when an input object produces a key that is already in the dictionary.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <see cref="DuplicateKeyBehavior.Error"/> writes a non-terminating error for the duplicate and keeps the
	/// existing value. <see cref="DuplicateKeyBehavior.Skip"/> writes a warning and keeps the existing value.
	/// </para>
	/// <para>
	/// <see cref="DuplicateKeyBehavior.Concatenate"/> collects every value for the key in an <see cref="ObjectList"/>.
	/// With this option, the dictionary's value type is always <see cref="object"/>, and a
	/// <see cref="ValueType"/> other than <see cref="object"/> is ignored with a warning.
	/// </para>
	/// </remarks>
	/// <value>The duplicate key behavior. Defaults to <see cref="DuplicateKeyBehavior.Error"/>.</value>
	[Parameter]
	public DuplicateKeyBehavior DuplicateKeyBehavior { get; set; }

	/// <summary>
	/// Gets or sets the equality comparer used to compare keys.
	/// </summary>
	/// <value>
	/// The key equality comparer. When not specified, <see cref="string"/> keys use
	/// <see cref="StringComparer.OrdinalIgnoreCase"/> and other key types use their default equality comparer.
	/// </value>
	[Parameter]
	public IEqualityComparer? KeyComparer { get; set; }

	/// <summary>
	/// Gets or sets the name of the property whose value becomes each object's key.
	/// </summary>
	/// <value>The key property name.</value>
	[Parameter(Mandatory = true, Position = 0, ParameterSetName = "KeyProperty"), Alias("KeyName", "Key")]
	[ValidateNotNullOrWhiteSpace]
	public string KeyPropertyName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the script block that computes each object's key.
	/// </summary>
	/// <remarks>
	/// The script block receives the current object as <c>$_</c>, <c>$PSItem</c>, <c>$this</c>, or <c>$args[0]</c>
	/// and must reference at least one of them. The first object it outputs becomes the key.
	/// </remarks>
	/// <value>The key selector <see cref="ScriptBlock"/>.</value>
	[Parameter(Mandatory = true, Position = 0, ParameterSetName = "KeyScript")]
	[ValidateScriptVariable(PSThisVariable.Underscore, PSThisVariable.PSItem, PSThisVariable.This, PSThisVariable.FirstArg)]
	public ScriptBlock KeySelector { get; set; } = null!;

	/// <summary>
	/// Gets or sets the name of the property whose value becomes each object's value, or a script block that computes
	/// the value.
	/// </summary>
	/// <remarks>
	/// A non-empty string selects a property by name. A <see cref="ScriptBlock"/> is used like
	/// <see cref="ValueSelector"/> when <see cref="ValueSelector"/> is not supplied. A property name takes precedence
	/// over <see cref="ValueSelector"/>.
	/// </remarks>
	/// <value>A property name, a <see cref="ScriptBlock"/>, or <see langword="null"/> to use each object as its own value.</value>
	[Parameter(Mandatory = false, Position = 1), Alias("ValueName", "Value")]
	[AllowEmptyString, PSAllowNull]
	public object? ValuePropertyName { get; set; }

	/// <summary>
	/// Gets or sets the script block that computes each object's value.
	/// </summary>
	/// <remarks>
	/// The script block receives the current object as <c>$_</c>, <c>$PSItem</c>, <c>$this</c>, or <c>$args[0]</c>
	/// and must reference at least one of them. The first object it outputs becomes the value. When it outputs
	/// nothing or <see langword="null"/>, the object itself becomes the value.
	/// </remarks>
	/// <value>The value selector <see cref="ScriptBlock"/>, or <see langword="null"/>.</value>
	[Parameter]
	[PSAllowNull, AllowEmptyString]
	[ValidateScriptVariable(PSThisVariable.Underscore, PSThisVariable.PSItem, PSThisVariable.This, PSThisVariable.FirstArg)]
	public ScriptBlock? ValueSelector { get; set; }

	/// <summary>
	/// Gets or sets the type of the dictionary's values.
	/// </summary>
	/// <remarks>
	/// The parameter accepts a <see cref="Type"/>, a type name, or a script block that contains a type literal such
	/// as <c>{ [int] }</c>. Selected values are converted to this type. The value is ignored when
	/// <see cref="DuplicateKeyBehavior"/> is <see cref="DuplicateKeyBehavior.Concatenate"/>.
	/// </remarks>
	/// <value>The value type, or <see langword="null"/> to infer it from the first input object.</value>
	[Parameter]
	[ArgumentToTypeTransform]
	public Type? ValueType
	{
		get => _valueType;
		set => _valueType = value;
	}

	private IDictionary _dictionary = null!;
	private Type _keyType = null!;
	private Type? _valueType;
	private nint _addToDictionaryPtr;

	/// <summary>
	/// Prepares the key and value selectors and, when input is bound by parameter, infers the key and value types.
	/// </summary>
	/// <remarks>
	/// Property names are turned into selector script blocks, and references to <c>$_</c>, <c>$PSItem</c>, and
	/// <c>$this</c> in user script blocks are rewritten to <c>$args[0]</c>. The method also chooses the function that
	/// adds entries according to <see cref="DuplicateKeyBehavior"/>.
	/// </remarks>
	protected override void BeginCore()
	{
		_addToDictionaryPtr = StoreAddToDictionaryFunction(this.DuplicateKeyBehavior);

		if (this.ParameterSetName.StartsWith("KeyProperty", StringComparison.Ordinal))
		{
			this.KeySelector = ScriptBlock.Create(string.Concat("$args[0].'", this.KeyPropertyName, "'"));
			this.KeyPropertyName = string.Empty;
		}
		else
		{
			this.KeySelector = this.KeySelector.ReplaceWithArgsZero();
		}

		if (this.ValuePropertyName is string s && !string.IsNullOrWhiteSpace(s))
		{
			this.ValueSelector = ScriptBlock.Create(string.Concat("$args[0].'", this.ValuePropertyName, "'"));
			this.ValuePropertyName = string.Empty;
		}
		else
		{
			this.ValueSelector = this.ValueSelector is not null
				? this.ValueSelector.ReplaceWithArgsZero()
				: this.ValuePropertyName is ScriptBlock valSc
					? valSc.ReplaceWithArgsZero()
					: null;
		}

		object?[]? inputObjects = this.InputObject;
		if (inputObjects is not null && inputObjects.Length > 0)
		{
			_keyType = GetTypeForElement(inputObjects, this.KeySelector);
			_valueType = this.GetValueType(_valueType, inputObjects);
		}
	}

	/// <summary>
	/// Adds an entry to the dictionary for each object in the current <see cref="InputObject"/> array.
	/// </summary>
	/// <remarks>The dictionary is created when the first non-empty array arrives.</remarks>
	/// <returns><see langword="true"/> to continue processing pipeline input; otherwise, <see langword="false"/>.</returns>
	protected override bool ProcessCore()
	{
		bool flag = true;
		object?[]? inputObjects = this.InputObject;
		if (inputObjects is null || inputObjects.Length == 0)
			return flag;

		_dictionary ??= this.CreateDictionary(inputObjects);

		unsafe
		{
			return this.AddToDictionary(inputObjects, (delegate*<ConvertToDictionaryCmdlet, object, object?, void>)_addToDictionaryPtr);
		}
	}

	/// <summary>
	/// Creates the dictionary for the resolved key and value types.
	/// </summary>
	/// <remarks>
	/// The key and value types are inferred from <paramref name="inputObjects"/> when they were not resolved during
	/// the begin phase. When <see cref="KeyComparer"/> is <see langword="null"/> and the key type is
	/// <see cref="string"/>, the method sets it to <see cref="StringComparer.OrdinalIgnoreCase"/>.
	/// </remarks>
	/// <param name="inputObjects">The first non-empty array of input objects.</param>
	/// <returns>The new, empty dictionary.</returns>
	/// <exception cref="PipelineStoppedException">Thrown after a terminating error is written because the dictionary cannot be constructed.</exception>
	[SuppressMessage("Style", "IDE0009", Justification = "Used in nameof()")]
	private IDictionary CreateDictionary(object?[] inputObjects)
	{
		if (_keyType is null || _valueType is null)
		{
			_keyType = GetTypeForElement(inputObjects, this.KeySelector);
			_valueType = this.GetValueType(_valueType, inputObjects);
		}

		if (this.KeyComparer is null && _keyType.Equals(typeof(string)))
		{
			this.KeyComparer = StringComparer.OrdinalIgnoreCase;
		}

		object[] args = this.KeyComparer is null
			? Array.Empty<object>()
			: [this.KeyComparer];

		Type? dictType = null;
		try
		{
			dictType = typeof(Dictionary<,>).MakeGenericType(_keyType, _valueType);
			return Activator.CreateInstance(dictType, args) as IDictionary
				?? throw new InvalidOperationException("Somehow, Dictionary is not an IDictionary?");
		}
		catch (Exception e)
		{
			ListFunctionsException ex = new($"Failed to instantiate dictionary with the arguments supplied - {e.Message}", e);
			IDictionary data = ex.Data;
			data["KeyType"] = _keyType.FullName ?? _keyType.Name;
			data[nameof(InputObject)] = inputObjects.DeepClone();
			data[nameof(ValueType)] = _valueType.FullName ?? _valueType.Name;
			data["DictionaryType"] = dictType?.FullName ?? dictType?.Name;

			var rec = ex.ToRecord(ErrorCategory.InvalidOperation, targetObj: null);

			this.ThrowTerminatingError(rec);
			throw;
		}
	}

	/// <summary>
	/// Selects a key and value from each input object and adds them to the dictionary.
	/// </summary>
	/// <remarks>
	/// <see langword="null"/> objects and objects whose key is <see langword="null"/> are skipped. A key or value that
	/// cannot be converted to the dictionary's type produces a non-terminating error. Any other exception, including
	/// one thrown by a selector script block, becomes a terminating error.
	/// </remarks>
	/// <param name="inputObjects">The input objects to add.</param>
	/// <param name="addToDictionaryAction">The function that adds a single entry according to <see cref="DuplicateKeyBehavior"/>.</param>
	/// <returns><see langword="true"/> when all objects were processed; otherwise, <see langword="false"/>.</returns>
	private unsafe bool AddToDictionary(object?[] inputObjects, delegate*<ConvertToDictionaryCmdlet, object, object?, void> addToDictionaryAction)
	{
		foreach (object? item in inputObjects.AsValueEnumerable())
		{
			if (item is null) continue;

			try
			{
				object? key = this.KeySelector.Invoke(item).AsValueEnumerable().FirstOrDefault().GetBaseObject();
				if (key is null)
					continue;

				key = LanguagePrimitives.ConvertTo(key, _keyType);

				object? value = this.ValueSelector?.Invoke(item).AsValueEnumerable().FirstOrDefault().GetBaseObject() is object o
					? LanguagePrimitives.ConvertTo(o, _valueType)
					: item;

				addToDictionaryAction(this, key, value);
			}
			catch (PSInvalidCastException e)
			{
				var rec = e.ToRecord(ErrorCategory.InvalidArgument, item);
				this.WriteError(rec);
			}
			catch (Exception e)
			{
				var rec = e.ToRecord(ErrorCategory.InvalidOperation, item);
				this.ThrowTerminatingError(rec);
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Writes the dictionary to the pipeline as a single object.
	/// </summary>
	/// <remarks>When no input objects were received, the method writes an empty, case-insensitive <see cref="Hashtable"/>.</remarks>
	/// <param name="state">The run state of the cmdlet. When <see cref="CmdletRunState.FoundMatch"/> is <see langword="true"/>, nothing is written.</param>
	protected override void EndCore(CmdletRunState state)
	{
		if (state.FoundMatch)
			return;

		if (_dictionary is null)
		{
			this.WriteObject(new Hashtable(StringComparer.OrdinalIgnoreCase));
			return;
		}

		this.WriteObject(_dictionary, enumerateCollection: false);
	}

	/// <summary>
	/// Resolves the dictionary's value type.
	/// </summary>
	/// <remarks>
	/// When <see cref="DuplicateKeyBehavior"/> is <see cref="DuplicateKeyBehavior.Concatenate"/>, the method returns
	/// <see cref="object"/> and writes a warning if <paramref name="specifiedType"/> is another type.
	/// </remarks>
	/// <param name="specifiedType">The value type supplied by the user, or <see langword="null"/>.</param>
	/// <param name="inputObjects">The input objects used to infer the value type.</param>
	/// <returns>The value type for the dictionary.</returns>
	private Type GetValueType(Type? specifiedType, object?[] inputObjects)
	{
		if (this.DuplicateKeyBehavior == DuplicateKeyBehavior.Concatenate)
		{
			if (specifiedType is not null && !typeof(object).Equals(specifiedType))
			{
				this.WriteWarning("ValueType is ignored when 'DuplicateKeyBehavior::Concatenate' is used as the values can either be objects or lists of objects.");
			}

			return typeof(object);
		}

		return specifiedType ?? GetTypeForElement(inputObjects, this.ValueSelector);
	}
	/// <summary>
	/// Infers a type from the first input object, optionally through a selector script block.
	/// </summary>
	/// <param name="inputObj">The input objects. The first element is used for inference.</param>
	/// <param name="selector">The selector to invoke with the first element as <c>$args[0]</c>, or <see langword="null"/> to use the first element itself.</param>
	/// <returns>
	/// The runtime type of the first element or of the selector's first output, or <see cref="object"/> when that value
	/// is <see langword="null"/>, a <see cref="PSObject"/>, or a <see cref="PSCustomObject"/>.
	/// </returns>
	private static Type GetTypeForElement(object?[] inputObj, ScriptBlock? selector)
	{
		Type? type;
		if (selector is null)
		{
			type = inputObj[0].GetBaseObject()?.GetType();
		}
		else
		{
			var firstObj = selector.Invoke(inputObj).AsValueEnumerable().FirstOrDefault();
			if (firstObj is null)
			{
				return typeof(object);
			}

			type = firstObj.GetBaseObject()?.GetType();
		}

		if (type is null || typeof(PSObject).IsAssignableFrom(type) || typeof(PSCustomObject).IsAssignableFrom(type))
		{
			return typeof(object);
		}

		return type;
	}

	/// <summary>
	/// Adds an entry, or appends the value to the existing entry's <see cref="ObjectList"/> when the key exists.
	/// </summary>
	/// <remarks>The first duplicate replaces the existing value with an <see cref="ObjectList"/> that contains it.</remarks>
	/// <param name="cmdlet">The cmdlet that owns the dictionary.</param>
	/// <param name="key">The key to add.</param>
	/// <param name="value">The value to add.</param>
	private static void AddConcat(ConvertToDictionaryCmdlet cmdlet, object key, object? value)
	{
		if (cmdlet._dictionary.Contains(key))
		{
			cmdlet.WriteVerbose("Key exists, concatenating next value.");
			object? existingValue = cmdlet._dictionary[key];
			if (existingValue is not ObjectList objList)
			{
				objList = new ObjectList()
					{
						existingValue,
					};

				cmdlet._dictionary[key] = objList;
			}

			objList.Add(value);
		}
		else
		{
			cmdlet._dictionary.Add(key, value);
		}
	}
	/// <summary>
	/// Adds an entry, or writes a warning and keeps the existing value when the key exists.
	/// </summary>
	/// <param name="cmdlet">The cmdlet that owns the dictionary.</param>
	/// <param name="key">The key to add.</param>
	/// <param name="value">The value to add.</param>
	private static void AddSkip(ConvertToDictionaryCmdlet cmdlet, object key, object? value)
	{
		if (cmdlet._dictionary.Contains(key))
		{
			cmdlet.WriteWarning("Key already exists, skipping value.");
			return;
		}

		cmdlet._dictionary.Add(key, value);
	}
	/// <summary>
	/// Adds an entry, or writes a non-terminating error when the entry cannot be added.
	/// </summary>
	/// <remarks>
	/// The method catches only <see cref="ArgumentException"/>, which the dictionary throws for a duplicate key or a
	/// value of the wrong type.
	/// </remarks>
	/// <param name="cmdlet">The cmdlet that owns the dictionary.</param>
	/// <param name="key">The key to add.</param>
	/// <param name="value">The value to add.</param>
	private static void AddVolatile(ConvertToDictionaryCmdlet cmdlet, object key, object? value)
	{
		try
		{
			cmdlet._dictionary.Add(key, value);
		}
		catch (ArgumentException e)
		{
			var rec = e.ToRecord(ErrorCategory.InvalidData, key);
			cmdlet.WriteError(rec);
		}
	}

	/// <summary>
	/// Returns a pointer to the function that adds entries for the specified duplicate key behavior.
	/// </summary>
	/// <remarks>
	/// <para>Unrecognized values use the same function as <see cref="DuplicateKeyBehavior.Error"/>.</para>
	/// <para><b>Performance:</b> The function is chosen once per invocation and called through an unmanaged function pointer, which avoids a per-item branch and a delegate allocation.</para>
	/// </remarks>
	/// <param name="duplicateBehavior">The duplicate key behavior.</param>
	/// <returns>The address of <see cref="AddVolatile"/>, <see cref="AddSkip"/>, or <see cref="AddConcat"/>.</returns>
	private static nint StoreAddToDictionaryFunction(DuplicateKeyBehavior duplicateBehavior)
	{
		unsafe
		{
			delegate*<ConvertToDictionaryCmdlet, object, object?, void> action = duplicateBehavior switch
			{
				DuplicateKeyBehavior.Error => &AddVolatile,
				DuplicateKeyBehavior.Skip => &AddSkip,
				DuplicateKeyBehavior.Concatenate => &AddConcat,
				_ => &AddVolatile,
			};
			return (nint)action;
		}
	}
}
