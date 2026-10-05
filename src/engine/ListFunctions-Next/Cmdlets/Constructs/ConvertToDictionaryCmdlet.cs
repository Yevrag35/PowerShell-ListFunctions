using ListFunctions.Components;
using ListFunctions.Exceptions;
using ListFunctions.Extensions;
using ListFunctions.Modern;
using ListFunctions.Modern.Variables;
using ListFunctions.Validation;
using ZLinq;

#nullable enable

namespace ListFunctions.Cmdlets.Constructs;

/// <summary>
/// Converts input objects into a <see cref="Dictionary{TKey, TValue}"/> whose keys and values are selected from each
/// object.
/// </summary>
/// <remarks>
/// <para>
/// Each object's key comes from the property named by <see cref="KeyPropertyName"/> or from the output of
/// <see cref="KeySelector"/>, and one of the two is required. Its value comes from the property named by
/// <see cref="ValuePropertyName"/>, from the output of <see cref="ValueSelector"/>, or, when neither is supplied, from
/// the object itself. A property or selector that gives <see langword="null"/> stores <see langword="null"/> converted
/// to the value type. <see cref="KeySelector"/> and <see cref="ValueSelector"/> run at most once for each input object.
/// </para>
/// <para>
/// The key type is inferred from the key of the first input object that isn't <see langword="null"/>. The value type
/// is <see cref="ValueType"/>, or is inferred from the value of that object. When an inferred type is a PowerShell
/// custom object, or the first key or value is <see langword="null"/>, <see cref="object"/> is used. Later keys and
/// values are converted to these types.
/// </para>
/// <para>
/// <see cref="DuplicateKeyBehavior"/> controls what happens when a key repeats. <see cref="string"/> keys compare
/// with <see cref="StringComparer.OrdinalIgnoreCase"/> unless <see cref="KeyComparer"/> is supplied. A key or value
/// that can't be converted produces the non-terminating error that <c>New-List</c> writes, and its object is skipped.
/// </para>
/// <para>
/// <see cref="KeySelector"/> and <see cref="ValueSelector"/> run under the caller's <c>$ErrorActionPreference</c>, and
/// their errors reach PowerShell unchanged, the way errors from a <c>ForEach-Object</c> script block do. For example, a
/// <c>throw</c> ends the whole script, and <c>break</c> leaves the loop around the cmdlet.
/// </para>
/// <para>
/// The dictionary is written as a single object and is not enumerated into the pipeline. When no input objects are
/// received, the cmdlet writes an empty, case-insensitive <see cref="Hashtable"/>.
/// </para>
/// </remarks>
[Cmdlet(VerbsData.ConvertTo, "Dictionary", DefaultParameterSetName = KEY_PROPERTY)]
public sealed class ConvertToDictionaryCmdlet : ListFunctionCmdletBase
{
	/// <summary>
	/// The name of the parameter set in which <see cref="KeyPropertyName"/> selects each key.
	/// </summary>
	/// <remarks>
	/// It's the default parameter set, so when neither <see cref="KeyPropertyName"/> nor <see cref="KeySelector"/> is
	/// given, PowerShell asks for <see cref="KeyPropertyName"/>, or reports it as missing when it can't prompt.
	/// </remarks>
	private const string KEY_PROPERTY = "KeyProperty";
	/// <summary>
	/// The name of the parameter set in which <see cref="KeySelector"/> computes each key.
	/// </summary>
	private const string KEY_SCRIPT = "KeyScript";

	/// <summary>
	/// Gets or sets the objects to convert. The value is accepted from the pipeline.
	/// </summary>
	/// <remarks>
	/// Each pipeline object is one input object, even when it's an array. An array passed to the parameter supplies its
	/// elements, and <see langword="null"/> supplies none. <see langword="null"/> elements are skipped, as are elements
	/// whose key is <see langword="null"/>.
	/// </remarks>
	/// <value>The current pipeline object, or the argument of the parameter. The value can be <see langword="null"/>.</value>
	[Parameter(Mandatory = true, ValueFromPipeline = true)]
	[AllowEmptyCollection, PSAllowNull, AllowEmptyString]
	public object? InputObject { get; set; }

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
	[Parameter(Mandatory = true, Position = 0, ParameterSetName = KEY_PROPERTY), Alias("KeyName", "Key")]
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
	[Parameter(Mandatory = true, Position = 0, ParameterSetName = KEY_SCRIPT)]
	[ValidateScriptVariable(PSThisVariable.Underscore, PSThisVariable.PSItem, PSThisVariable.This, PSThisVariable.FirstArg)]
	public ScriptBlock KeySelector { get; set; } = null!;

	/// <summary>
	/// Gets or sets the name of the property whose value becomes each object's value, or a script block that computes
	/// the value.
	/// </summary>
	/// <remarks>
	/// A non-empty string selects a property by name. A <see cref="ScriptBlock"/> is used like
	/// <see cref="ValueSelector"/> when <see cref="ValueSelector"/> is not supplied. A property name takes precedence
	/// over <see cref="ValueSelector"/>. A property whose value is <see langword="null"/> gives <see langword="null"/>
	/// converted to the value type, as <see cref="ValueSelector"/> describes.
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
	/// and must reference at least one of them. The first object it outputs becomes the value, converted to the value
	/// type. When it outputs nothing or <see langword="null"/>, the value is <see langword="null"/> converted to the
	/// value type, the way PowerShell converts it when it calls the dictionary's <c>Add</c> method: an empty string for
	/// <see cref="string"/>, 0 for <see cref="int"/>, and <see langword="null"/> for <see cref="object"/> and most other
	/// reference types.
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
	public Type? ValueType { get; set; }

	private IDictionary _dictionary = null!;
	private Type _keyType = null!;
	private nint _addToDictionaryPtr;
	private readonly PSThisVariable _current = new();
	private readonly List<PSVariable> _variables = [];

	// The outputs of the selectors that InferTypes ran for the first input object. AddToDictionary uses them when it
	// adds that object, instead of running the selectors again.
	private bool _hasFirstOutputs;
	private object? _firstKey;
	private bool _hasFirstValue;
	private object? _firstValue;

	/// <summary>
	/// Prepares the key and value selectors.
	/// </summary>
	/// <remarks>
	/// Property names are turned into selector script blocks, and a script block passed to
	/// <see cref="ValuePropertyName"/> becomes the value selector when <see cref="ValueSelector"/> isn't supplied. The
	/// method also chooses the function that adds entries according to <see cref="DuplicateKeyBehavior"/>.
	/// </remarks>
	protected override void BeginCore()
	{
		_addToDictionaryPtr = StoreAddToDictionaryFunction(this.DuplicateKeyBehavior);

		if (this.ParameterSetName.StartsWith(KEY_PROPERTY, StringComparison.Ordinal))
		{
			this.KeySelector = CreatePropertySelector(this.KeyPropertyName);
			this.KeyPropertyName = string.Empty;
		}

		if (this.ValuePropertyName is string s && !string.IsNullOrWhiteSpace(s))
		{
			this.ValueSelector = CreatePropertySelector(s);
			this.ValuePropertyName = string.Empty;
		}
		else
		{
			this.ValueSelector ??= this.ValuePropertyName as ScriptBlock;
		}
	}

	/// <summary>
	/// Creates a selector script block that returns the value of the specified property of its input object.
	/// </summary>
	/// <remarks>
	/// The script block reads the property with PowerShell's member access, as <c>$args[0].'name'</c>. The method escapes
	/// every single quote in <paramref name="propertyName"/>, including the typographic ones that PowerShell also treats
	/// as single quotes, so any name gives a valid script.
	/// </remarks>
	/// <param name="propertyName">The name of the property. This value must not be <see langword="null"/>.</param>
	/// <returns>The new selector <see cref="ScriptBlock"/>.</returns>
	private static ScriptBlock CreatePropertySelector(string propertyName)
	{
		return ScriptBlock.Create(string.Concat("$args[0].'", CodeGeneration.EscapeSingleQuotedStringContent(propertyName), "'"));
	}

	/// <summary>
	/// Runs a selector script block for an input object and returns its first output.
	/// </summary>
	/// <remarks>
	/// The script block gets <paramref name="item"/> as <c>$_</c>, <c>$PSItem</c>, <c>$this</c>, and <c>$args[0]</c>,
	/// the way the other cmdlets pass elements to their script blocks. The method sets these variables instead of
	/// changing the script block's text, so <c>$_</c> works anywhere in it, such as before an operator or in a
	/// double-quoted string, and a nested script block, such as the filter of <c>Where-Object</c>, keeps its own
	/// <c>$_</c>.
	/// </remarks>
	/// <param name="selector">The selector to run. This value must not be <see langword="null"/>.</param>
	/// <param name="item">The input object.</param>
	/// <returns>
	/// The first object that <paramref name="selector"/> outputs, unwrapped from its <see cref="PSObject"/>, or
	/// <see langword="null"/> when it outputs nothing.
	/// </returns>
	/// <exception cref="RuntimeException">Thrown when the selector throws a terminating error.</exception>
	private object? Select(ScriptBlock selector, object item)
	{
		// InsertIntoList adds the same variables on every call, so the list starts empty each time.
		_variables.Clear();
		_current.SetValue(item);
		_current.InsertIntoList(_variables);

		Collection<PSObject> results = selector.InvokeWithContext(null, _variables, [item]);
		return results.Count > 0
			? results[0].GetBaseObject()
			: null;
	}

	/// <summary>
	/// Adds an entry to the dictionary for each object in the current <see cref="InputObject"/>.
	/// </summary>
	/// <remarks>The dictionary is created when the first input object that isn't <see langword="null"/> arrives.</remarks>
	/// <returns><see langword="true"/> to continue processing pipeline input; otherwise, <see langword="false"/>.</returns>
	protected override bool ProcessCore()
	{
		object?[] inputObjects = this.GetInputElements(this.InputObject);
		if (_dictionary is null)
		{
			if (FindFirstObject(inputObjects) is not { } firstObject)
			{
				return true;
			}

			// The first object that AddToDictionary adds is firstObject, so it can use the outputs of the selectors that
			// ran for firstObject while CreateDictionary inferred the key and value types.
			_dictionary = this.CreateDictionary(inputObjects, firstObject);
		}

		unsafe
		{
			return this.AddToDictionary(inputObjects, (delegate*<ConvertToDictionaryCmdlet, object, object?, void>)_addToDictionaryPtr);
		}
	}

	/// <summary>
	/// Returns the first of the specified input objects that isn't <see langword="null"/>.
	/// </summary>
	/// <param name="inputObjects">The input objects to search.</param>
	/// <returns>The first input object that isn't <see langword="null"/>, or <see langword="null"/> when there's none.</returns>
	private static object? FindFirstObject(object?[] inputObjects)
	{
		foreach (object? item in inputObjects)
		{
			if (item is not null)
			{
				return item;
			}
		}

		return null;
	}

	/// <summary>
	/// Creates the dictionary for the key and value types that the method infers from the first input object.
	/// </summary>
	/// <remarks>
	/// The method infers the types with <see cref="InferTypes(object)"/>. When <see cref="KeyComparer"/> is
	/// <see langword="null"/> and the key type is <see cref="string"/>, the method sets it to
	/// <see cref="StringComparer.OrdinalIgnoreCase"/>.
	/// </remarks>
	/// <param name="inputObjects">The input objects that hold <paramref name="firstObject"/>. The method adds them to the error that it writes when the dictionary can't be constructed.</param>
	/// <param name="firstObject">The first input object that isn't <see langword="null"/>.</param>
	/// <returns>The new, empty dictionary.</returns>
	/// <exception cref="RuntimeException">Thrown when a selector fails. The exception reaches PowerShell unchanged.</exception>
	/// <exception cref="PipelineStoppedException">Thrown after a terminating error is written because the dictionary cannot be constructed.</exception>
	[SuppressMessage("Style", "IDE0009", Justification = "Used in nameof()")]
	private IDictionary CreateDictionary(object?[] inputObjects, object firstObject)
	{
		this.InferTypes(firstObject);

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
			dictType = typeof(Dictionary<,>).MakeGenericType(_keyType, this.ValueType);
			return Activator.CreateInstance(dictType, args) as IDictionary
				?? throw new InvalidOperationException("Somehow, Dictionary is not an IDictionary?");
		}
		catch (Exception e)
		{
			ListFunctionsException ex = new($"Failed to instantiate dictionary with the arguments supplied - {e.Message}", e);
			IDictionary data = ex.Data;
			data["KeyType"] = _keyType.FullName ?? _keyType.Name;
			data[nameof(InputObject)] = ObjectCloningExtensions.Clone(inputObjects);
			data[nameof(ValueType)] = this.ValueType.FullName ?? this.ValueType.Name;
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
	/// <para>
	/// <see langword="null"/> objects and objects whose key is <see langword="null"/> are skipped. Without a value
	/// selector, each object is its own value. Otherwise, the selector's output is converted to the value type, even when
	/// it's <see langword="null"/>. A key or value that cannot be converted to the dictionary's type produces the
	/// non-terminating error that <c>New-List</c> writes, and the object is skipped.
	/// </para>
	/// <para>
	/// An error from a selector script block reaches PowerShell unchanged, the way an error from a <c>ForEach-Object</c>
	/// script block does. Any other exception becomes a terminating error.
	/// </para>
	/// <para>
	/// For the first input object, the method uses the outputs of the selectors that <see cref="InferTypes(object)"/>
	/// ran for it, so each selector runs at most once for each input object.
	/// </para>
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
				object? key;
				bool hasSelectedValue = false;
				object? selectedValue = null;
				if (_hasFirstOutputs)
				{
					_hasFirstOutputs = false;
					key = _firstKey;
					hasSelectedValue = _hasFirstValue;
					selectedValue = _firstValue;
				}
				else
				{
					key = this.Select(this.KeySelector, item);
				}

				if (key is null)
					continue;

				// A key that converts to null, as [NullString]::Value does for [string], is skipped like a null key.
				if (!this.TryConvertItem(key, _keyType, out key) || key is null)
				{
					continue;
				}

				if (!hasSelectedValue && this.ValueSelector is not null)
				{
					selectedValue = this.Select(this.ValueSelector, item);
				}

				// A selected null converts the way PowerShell converts null when it calls Add: to an empty string for
				// [string], to 0 for [int], and to null for [object] and most other reference types. InferTypes sets
				// ValueType before the first object is added.
				object? value = item;
				if (this.ValueSelector is not null && !this.TryConvertItem(selectedValue, this.ValueType!, out value))
				{
					continue;
				}

				addToDictionaryAction(this, key, value);
			}
			catch (Exception e) when (!PassesThrough(e))
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
	/// Infers the dictionary's key and value types from the first input object, and keeps the outputs of the selectors
	/// that run for it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The key type comes from the key selector's output. The value type is <see cref="ValueType"/> when it's supplied.
	/// Otherwise, it comes from the value selector's output, or from <paramref name="firstObject"/> itself when there's
	/// no value selector. When <see cref="DuplicateKeyBehavior"/> is <see cref="DuplicateKeyBehavior.Concatenate"/>, the
	/// value type is always <see cref="object"/>, and the method writes a warning if <see cref="ValueType"/> is another
	/// type.
	/// </para>
	/// <para>
	/// The method runs the value selector only when it needs the output for the value type. It keeps the outputs of the
	/// selectors that it runs, so that <see cref="AddToDictionary"/> doesn't run them for
	/// <paramref name="firstObject"/> again.
	/// </para>
	/// </remarks>
	/// <param name="firstObject">The first input object that isn't <see langword="null"/>.</param>
	/// <exception cref="RuntimeException">Thrown when a selector throws a terminating error.</exception>
	[MemberNotNull(nameof(ValueType))]
	private void InferTypes(object firstObject)
	{
		_firstKey = this.Select(this.KeySelector, firstObject);
		_keyType = GetInferredType(_firstKey);

		if (this.DuplicateKeyBehavior == DuplicateKeyBehavior.Concatenate)
		{
			if (this.ValueType is not null && !typeof(object).Equals(this.ValueType))
			{
				this.WriteWarning("ValueType is ignored when 'DuplicateKeyBehavior::Concatenate' is used as the values can either be objects or lists of objects.");
			}

			this.ValueType = typeof(object);
		}
		else if (this.ValueType is null && this.ValueSelector is not null)
		{
			_firstValue = this.Select(this.ValueSelector, firstObject);
			_hasFirstValue = true;
			this.ValueType = GetInferredType(_firstValue);
		}
		else
		{
			this.ValueType ??= GetInferredType(firstObject.GetBaseObject());
		}

		_hasFirstOutputs = true;
	}
	/// <summary>
	/// Returns the type that the dictionary uses for a key or value like the specified one.
	/// </summary>
	/// <param name="value">The key or value, unwrapped from its <see cref="PSObject"/>, or <see langword="null"/>.</param>
	/// <returns>
	/// The runtime type of <paramref name="value"/>, or <see cref="object"/> when <paramref name="value"/> is
	/// <see langword="null"/>, a <see cref="PSObject"/>, or a <see cref="PSCustomObject"/>.
	/// </returns>
	private static Type GetInferredType(object? value)
	{
		Type? type = value?.GetType();
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
				objList = [existingValue];

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
