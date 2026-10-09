using ListFunctions.Completion;
using ListFunctions.Components;
using ListFunctions.Extensions;
using ListFunctions.Internal;
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
/// the object itself. A property name or script block in <see cref="ValuePropertyName"/> can't be combined with
/// <see cref="ValueSelector"/>. <see cref="KeySelector"/> and <see cref="ValueSelector"/> run at most once for each
/// input object.
/// </para>
/// <para>
/// The key type is <see cref="KeyType"/>, and the value type is <see cref="ValueType"/>. Each is <see cref="object"/>
/// when it isn't supplied, whatever the input objects are. Every key and value is converted to its type the way
/// PowerShell converts the arguments of the dictionary's <c>Add</c> method, including an input object that is its own
/// value. A key or value that can't be converted produces the non-terminating error that <c>New-List</c> writes, and its
/// object is skipped.
/// </para>
/// <para>
/// Without <see cref="KeyComparer"/>, <see cref="string"/> keys compare with
/// <see cref="StringComparer.OrdinalIgnoreCase"/>, and <see cref="object"/> keys compare with the same comparer: two
/// strings the same way as <see cref="string"/> keys, and any other two keys with their own
/// <see cref="object.Equals(object)"/> method, so <c>1</c> and <c>"1"</c> are different keys.
/// <see cref="DuplicateKeyBehavior"/> controls what happens when a key repeats.
/// </para>
/// <para>
/// A <see langword="null"/> input object is skipped. An object whose key is <see langword="null"/> produces a
/// non-terminating error and is skipped. A <see langword="null"/> value is stored, converted to the value type like any
/// other value.
/// </para>
/// <para>
/// <see cref="KeySelector"/> and <see cref="ValueSelector"/> run under the caller's <c>$ErrorActionPreference</c>, and
/// their errors reach PowerShell unchanged, the way errors from a <c>ForEach-Object</c> script block do. For example, a
/// <c>throw</c> ends the whole script, and <c>break</c> leaves the loop around the cmdlet.
/// </para>
/// <para>
/// The cmdlet creates the dictionary before it reads any input, and writes it as a single object that is not enumerated
/// into the pipeline. When no input objects are received, the dictionary is empty.
/// </para>
/// </remarks>
[OutputType(typeof(Dictionary<object, object>))]
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
	/// elements, and <see langword="null"/> supplies none. The parameter can't be combined with pipeline input.
	/// <see langword="null"/> elements are skipped. An element whose key is <see langword="null"/> produces a
	/// non-terminating error and is skipped.
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
	/// <remarks>
	/// The comparer works with any key type. When it isn't an <see cref="IEqualityComparer{T}"/> of the key type, such as
	/// a <see cref="StringComparer"/> for <see cref="object"/> keys, the dictionary compares its keys through the
	/// comparer's <see cref="IEqualityComparer"/> methods. A <see cref="StringComparer"/> compares two strings as
	/// strings, and any other two keys with their own <see cref="object.Equals(object)"/> method.
	/// </remarks>
	/// <value>
	/// The key equality comparer. When not specified, <see cref="string"/> and <see cref="object"/> keys use
	/// <see cref="StringComparer.OrdinalIgnoreCase"/>, and other key types use their default equality comparer.
	/// </value>
	[Parameter]
	public IEqualityComparer? KeyComparer { get; set; }

	/// <summary>
	/// Gets or sets the name of the property whose value becomes each object's key.
	/// </summary>
	/// <remarks>
	/// The parameter rejects a script block passed to it by name when it binds, because PowerShell would otherwise
	/// convert the script block to a property name made of the script's text. A script block passed by position binds to
	/// <see cref="KeySelector"/>.
	/// </remarks>
	/// <value>The key property name.</value>
	[Parameter(Mandatory = true, Position = 0, ParameterSetName = KEY_PROPERTY), Alias("KeyName", "Key")]
	[RejectScriptBlock(nameof(KeySelector))]
	[ValidateNotNullOrWhiteSpace]
	public string KeyPropertyName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the script block that computes each object's key.
	/// </summary>
	/// <remarks>
	/// The script block receives the current object as <c>$_</c>, <c>$PSItem</c>, and <c>$this</c>, and as its first
	/// argument: <c>$args[0]</c>, or the first parameter of its <c>param()</c> block. It must reference at least one of
	/// them. Parameter validation also rejects a script block that the cmdlet can't run, such as one that has a
	/// <c>begin</c> block. The first object it outputs becomes the key.
	/// </remarks>
	/// <value>The key selector <see cref="ScriptBlock"/>.</value>
	[Parameter(Mandatory = true, Position = 0, ParameterSetName = KEY_SCRIPT)]
	[IsScriptBlock, ValidateScriptVariable(PSThisVariable.Underscore, PSThisVariable.PSItem, PSThisVariable.This, PSThisVariable.FirstArg)]
	public ScriptBlock KeySelector { get; set; } = null!;

	/// <summary>
	/// Gets or sets the type of the dictionary's keys.
	/// </summary>
	/// <remarks>
	/// The parameter accepts a <see cref="Type"/>, a type name, or a script block that contains a type literal such
	/// as <c>{ [int] }</c>. Every key is converted to this type.
	/// </remarks>
	/// <value>The key type, or <see langword="null"/> for <see cref="object"/>.</value>
	[Parameter]
	[ArgumentToTypeTransform]
	[ArgumentCompleter(typeof(TypeNameCompleter))]
	[PSDefaultValue(Value = typeof(object))]
	public Type? KeyType { get; set; }

	/// <summary>
	/// Gets or sets the name of the property whose value becomes each object's value, or a script block that computes
	/// the value.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A string selects a property by name, and a <see cref="ScriptBlock"/> computes the value the way
	/// <see cref="ValueSelector"/> does. Either one can arrive wrapped in a <see cref="PSObject"/>, as a line that
	/// <c>Get-Content</c> reads does. <see langword="null"/>, an empty string, and a string of white space select nothing,
	/// so each object is its own value. The parameter rejects any other argument, such as a number or an array of
	/// names, when it binds, and so does parameter validation for a script block that the cmdlet can't run, such as one
	/// that has a <c>begin</c> block.
	/// </para>
	/// <para>
	/// A property name or script block can't be combined with <see cref="ValueSelector"/>. A property whose value is
	/// <see langword="null"/> gives <see langword="null"/> converted to the value type, as <see cref="ValueSelector"/>
	/// describes.
	/// </para>
	/// </remarks>
	/// <value>
	/// A property name, a <see cref="ScriptBlock"/>, or <see langword="null"/> to use each object as its own value. The
	/// value is unwrapped from its <see cref="PSObject"/> when the parameter binds.
	/// </value>
	[Parameter(Mandatory = false, Position = 1), Alias("ValueName", "Value")]
	[StringOrScriptBlockTransform, IsScriptBlock]
	[AllowEmptyString, PSAllowNull]
	public object? ValuePropertyName { get; set; }

	/// <summary>
	/// Gets or sets the script block that computes each object's value.
	/// </summary>
	/// <remarks>
	/// The script block receives the current object as <c>$_</c>, <c>$PSItem</c>, and <c>$this</c>, and as its first
	/// argument: <c>$args[0]</c>, or the first parameter of its <c>param()</c> block. It must reference at least one of
	/// them. Parameter validation also rejects a script block that the cmdlet can't run, such as one that has a
	/// <c>begin</c> block. The first object it outputs becomes the value, converted to the value type. When it outputs
	/// nothing or <see langword="null"/>, the value is <see langword="null"/> converted to the value type, the way
	/// PowerShell converts it when it calls the dictionary's <c>Add</c> method: an empty string for <see cref="string"/>,
	/// 0 for <see cref="int"/>, and <see langword="null"/> for <see cref="object"/> and most other reference types. The
	/// parameter can't be combined with a property name or script block in <see cref="ValuePropertyName"/>.
	/// </remarks>
	/// <value>The value selector <see cref="ScriptBlock"/>, or <see langword="null"/>.</value>
	[Parameter]
	[PSAllowNull, AllowEmptyString]
	[IsScriptBlock, ValidateScriptVariable(PSThisVariable.Underscore, PSThisVariable.PSItem, PSThisVariable.This, PSThisVariable.FirstArg)]
	public ScriptBlock? ValueSelector { get; set; }

	/// <summary>
	/// Gets or sets the type of the dictionary's values.
	/// </summary>
	/// <remarks>
	/// The parameter accepts a <see cref="Type"/>, a type name, or a script block that contains a type literal such
	/// as <c>{ [int] }</c>. Every value is converted to this type, including an input object that is its own value. The
	/// parameter is ignored when <see cref="DuplicateKeyBehavior"/> is <see cref="DuplicateKeyBehavior.Concatenate"/>,
	/// because the value type is then always <see cref="object"/>.
	/// </remarks>
	/// <value>The value type, or <see langword="null"/> for <see cref="object"/>.</value>
	[Parameter]
	[ArgumentToTypeTransform]
	[ArgumentCompleter(typeof(TypeNameCompleter))]
	[PSDefaultValue(Value = typeof(object))]
	public Type? ValueType { get; set; }

	private DictionaryWrapper _dictionary = null!;
	private Type _keyType = null!;
	private nint _addToDictionaryPtr;
	private readonly PSThisVariable _current = new();
	private readonly List<PSVariable> _variables = [];

	/// <summary>
	/// Prepares the key and value selectors, and creates the dictionary.
	/// </summary>
	/// <remarks>
	/// Property names are turned into selector script blocks, and a script block passed to
	/// <see cref="ValuePropertyName"/> becomes the value selector. The method also chooses the function that adds
	/// entries according to <see cref="DuplicateKeyBehavior"/>, and creates the empty dictionary before any input
	/// arrives.
	/// </remarks>
	/// <exception cref="ArgumentException">Thrown when <see cref="ValuePropertyName"/> is a property name or a script block, and <see cref="ValueSelector"/> is supplied too; or when the key type or the value type can't be a type argument of <see cref="Dictionary{TKey, TValue}"/>.</exception>
	protected override void BeginCore()
	{
		_addToDictionaryPtr = StoreAddToDictionaryFunction(this.DuplicateKeyBehavior);

		if (this.ParameterSetName.StartsWith(KEY_PROPERTY, StringComparison.Ordinal))
		{
			this.KeySelector = CreatePropertySelector(this.KeyPropertyName);
		}

		// The transformation attribute on ValuePropertyName leaves only null, a string, or a script block.
		ScriptBlock? selectorFromName = this.ValuePropertyName switch
		{
			string name when !string.IsNullOrWhiteSpace(name) => CreatePropertySelector(name),
			ScriptBlock block => block,
			_ => null,
		};

		if (selectorFromName is not null)
		{
			if (this.ValueSelector is not null)
			{
				throw new ArgumentException(
					"Cannot use -ValuePropertyName and -ValueSelector together, because both select each object's value. Use "
					+ "only one of them. A second positional argument binds to -ValuePropertyName.");
			}

			this.ValueSelector = selectorFromName;
		}

		_dictionary = this.CreateDictionary();
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
	/// Creates the empty dictionary for the key and value types.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The key type is <see cref="KeyType"/>, and the value type is <see cref="ValueType"/>. Each is
	/// <see cref="object"/> when it isn't supplied. When <see cref="DuplicateKeyBehavior"/> is
	/// <see cref="DuplicateKeyBehavior.Concatenate"/>, the value type is always <see cref="object"/>, and the method
	/// writes a warning if <see cref="ValueType"/> is another type.
	/// </para>
	/// <para>
	/// The dictionary compares its keys with <see cref="KeyComparer"/>, which is wrapped in an adapter when it isn't an
	/// <see cref="IEqualityComparer{T}"/> of the key type, or with the default comparer for the key type.
	/// </para>
	/// <para>
	/// The dictionary is created through a <see cref="DictionaryWrapper"/>, which converts each value to the value type
	/// and adds each entry with typed calls. The method sets the wrapper's callback that writes the non-terminating error
	/// that <c>New-List</c> writes for a value that can't be converted.
	/// </para>
	/// </remarks>
	/// <returns>The wrapper over the new, empty dictionary.</returns>
	/// <exception cref="ArgumentException">Thrown when the key type or the value type can't be a type argument of <see cref="Dictionary{TKey, TValue}"/>, such as a pointer type.</exception>
	private DictionaryWrapper CreateDictionary()
	{
		_keyType = this.KeyType ?? typeof(object);
		Type valueType = this.ValueType ?? typeof(object);

		if (this.DuplicateKeyBehavior == DuplicateKeyBehavior.Concatenate)
		{
			if (!typeof(object).Equals(valueType))
			{
				this.WriteWarning("ValueType is ignored when 'DuplicateKeyBehavior::Concatenate' is used as the values can either be objects or lists of objects.");
			}

			valueType = typeof(object);
		}

		DictionaryWrapper dictionary = DictionaryWrapper.CreateTyped(_keyType, valueType, 0, this.KeyComparer);
		dictionary.ConversionFailed = (item, type, exception) => this.WriteConversionError(exception, item, type);
		return dictionary;
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
	/// <returns><see langword="true"/> to continue processing pipeline input; otherwise, <see langword="false"/>.</returns>
	protected override bool ProcessCore()
	{
		object?[] inputObjects = this.GetInputElements(this.InputObject);

		unsafe
		{
			return this.AddToDictionary(inputObjects, (delegate*<ConvertToDictionaryCmdlet, object, object?, void>)_addToDictionaryPtr);
		}
	}

	/// <summary>
	/// Selects a key and value from each input object and adds them to the dictionary.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <see langword="null"/> objects are skipped. An object whose key is <see langword="null"/>, or converts to
	/// <see langword="null"/>, produces a non-terminating error and is skipped, and its value selector doesn't run.
	/// </para>
	/// <para>
	/// Each key is converted to the key type, and each value to the value type: the value selector's output, even when
	/// it's <see langword="null"/>, or, without a value selector, the object itself, unwrapped from its
	/// <see cref="PSObject"/> unless it's a custom object. A key or value that cannot be converted produces the
	/// non-terminating error that <c>New-List</c> writes, and the object is skipped.
	/// </para>
	/// <para>
	/// An error from a selector script block reaches PowerShell unchanged, the way an error from a <c>ForEach-Object</c>
	/// script block does. Any other exception becomes a terminating error.
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
				object? key = this.Select(this.KeySelector, item);
				if (key is null)
				{
					this.WriteNullKeyError(item, afterConversion: false);
					continue;
				}

				if (!this.TryConvertItem(key, _keyType, out key))
				{
					continue;
				}

				// A key can convert to null, as [NullString]::Value does for [string].
				if (key is null)
				{
					this.WriteNullKeyError(item, afterConversion: true);
					continue;
				}

				// The dictionary converts each value the way PowerShell converts the arguments of Add, whether the value
				// selector gives it or the object is its own value. A selected null converts to an empty string for
				// [string], to 0 for [int], and to null for [object] and most other reference types. PowerShell also
				// unwraps an argument from its PSObject, which the conversion to [object] doesn't do, so the object is
				// unwrapped first.
				object? value = this.ValueSelector is null
					? item.GetBaseObject()
					: this.Select(this.ValueSelector, item);

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
	/// Writes a non-terminating error for an input object whose key is <see langword="null"/>.
	/// </summary>
	/// <remarks>
	/// A dictionary can't hold a <see langword="null"/> key, so the caller skips the object. The error wraps an
	/// <see cref="System.ArgumentNullException"/> whose message says where the key came from: the property that
	/// <see cref="KeyPropertyName"/> names, <see cref="KeySelector"/>, or the conversion to the key type. The error's
	/// category is <see cref="ErrorCategory.InvalidData"/>, and its target object is <paramref name="item"/>.
	/// </remarks>
	/// <param name="item">The input object whose key is <see langword="null"/>.</param>
	/// <param name="afterConversion">
	/// <see langword="true"/> when the key became <see langword="null"/> in the conversion to the key type;
	/// <see langword="false"/> when the property or the selector gave <see langword="null"/>.
	/// </param>
	private void WriteNullKeyError(object item, bool afterConversion)
	{
		string reason;
		if (afterConversion)
		{
			reason = $"its key converts to $null as a [{_keyType.GetTypeName()}]";
		}
		else if (string.IsNullOrEmpty(this.KeyPropertyName))
		{
			reason = "-KeySelector returned nothing, or $null, for it";
		}
		else
		{
			reason = $"it has no '{this.KeyPropertyName}' property, or the property's value is $null";
		}

		var exception = new ArgumentNullException(paramName: null, $"Cannot add the object to the dictionary, because {reason}. A dictionary key can't be $null.");
		this.WriteError(exception.ToRecord(ErrorCategory.InvalidData, item));
	}

	/// <summary>
	/// Writes the dictionary to the pipeline as a single object.
	/// </summary>
	/// <remarks>When no input objects were received, the dictionary is empty.</remarks>
	/// <param name="state">The run state of the cmdlet. When <see cref="CmdletRunState.FoundMatch"/> is <see langword="true"/>, nothing is written.</param>
	private protected override void EndCore(CmdletRunState state)
	{
		if (state.FoundMatch)
			return;

		this.WriteObject(_dictionary.AsDictionary(), enumerateCollection: false);
	}

	/// <summary>
	/// Adds an entry, or appends the value to the existing entry's <see cref="ObjectList"/> when the key exists.
	/// </summary>
	/// <remarks>
	/// The first duplicate replaces the existing value with an <see cref="ObjectList"/> that contains it and the new
	/// value, and the method writes a verbose message for each duplicate. A value that can't be converted is skipped
	/// with the error that the dictionary's conversion callback writes.
	/// </remarks>
	/// <param name="cmdlet">The cmdlet that owns the dictionary.</param>
	/// <param name="key">The converted key to add.</param>
	/// <param name="value">The value to convert and add.</param>
	private static void AddConcat(ConvertToDictionaryCmdlet cmdlet, object key, object? value)
	{
		if (cmdlet._dictionary.AddOrAppend(key, value) == DictionaryAddResult.Appended)
		{
			cmdlet.WriteVerbose("Key exists, concatenating next value.");
		}
	}
	/// <summary>
	/// Adds an entry, or writes a warning and keeps the existing value when the key exists.
	/// </summary>
	/// <remarks>
	/// A value that can't be converted is skipped with the error that the dictionary's conversion callback writes.
	/// </remarks>
	/// <param name="cmdlet">The cmdlet that owns the dictionary.</param>
	/// <param name="key">The converted key to add.</param>
	/// <param name="value">The value to convert and add.</param>
	private static void AddSkip(ConvertToDictionaryCmdlet cmdlet, object key, object? value)
	{
		if (cmdlet._dictionary.TryAdd(key, value) == DictionaryAddResult.KeyExists)
		{
			cmdlet.WriteWarning("Key already exists, skipping value.");
		}
	}
	/// <summary>
	/// Adds an entry, or writes a non-terminating error when the entry cannot be added.
	/// </summary>
	/// <remarks>
	/// The method catches only the <see cref="ArgumentException"/> that the dictionary throws for a key that it already
	/// holds, and writes the error with <paramref name="key"/> as its target. A value that can't be converted is skipped
	/// with the error that the dictionary's conversion callback writes.
	/// </remarks>
	/// <param name="cmdlet">The cmdlet that owns the dictionary.</param>
	/// <param name="key">The converted key to add.</param>
	/// <param name="value">The value to convert and add.</param>
	private static void AddVolatile(ConvertToDictionaryCmdlet cmdlet, object key, object? value)
	{
		try
		{
			_ = cmdlet._dictionary.Add(key, value);
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
