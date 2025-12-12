using ListFunctions.Exceptions;
using ListFunctions.Extensions;
using ListFunctions.Modern;
using ListFunctions.Modern.Variables;
using ListFunctions.Validation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Management.Automation;
using ZLinq;

using PSAllowNull = System.Management.Automation.AllowNullAttribute;

#nullable enable

namespace ListFunctions.Cmdlets.Constructs
{
    /// <summary>
    /// Provides a PowerShell cmdlet that converts a collection of input objects into a dictionary, using specified keys
    /// and values derived from object properties or script blocks.
    /// </summary>
    /// <remarks>Use this cmdlet to transform lists or arrays of objects into a dictionary, where keys and
    /// values can be selected via property names or custom script blocks. The behavior for handling duplicate keys is
    /// configurable via the DuplicateKeyBehavior property, allowing you to choose whether to throw an error, skip
    /// duplicates, or concatenate values. Key comparison can be customized with the KeyComparer property. This cmdlet
    /// supports both property-based and script-based key/value selection, and is suitable for scenarios where you need
    /// to group or index objects by a specific attribute or computed value.</remarks>
    [Cmdlet(VerbsData.ConvertTo, "Dictionary", DefaultParameterSetName = "None")]
    public sealed class ConvertToDictionaryCmdlet : ListFunctionCmdletBase
    {
        [Parameter(Mandatory = true, ValueFromPipeline = true)]
        [AllowEmptyCollection, PSAllowNull, AllowEmptyString]
        public object?[]? InputObject { get; set; }

        [Parameter]
        public DuplicateKeyBehavior DuplicateKeyBehavior { get; set; }

        [Parameter]
        public IEqualityComparer? KeyComparer { get; set; }

        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "KeyProperty"), Alias("KeyName", "Key")]
        [ValidateNotNullOrWhiteSpace]
        public string KeyPropertyName { get; set; } = string.Empty;

        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "KeyScript")]
        [ValidateScriptVariable(PSThisVariable.UNDERSCORE_NAME, PSThisVariable.PSITEM_NAME, PSThisVariable.THIS_NAME, PSThisVariable.ARGS_FIRST)]
        public ScriptBlock KeySelector { get; set; } = null!;

        [Parameter(Mandatory = false, Position = 1), Alias("ValueName", "Value")]
        [AllowEmptyString, PSAllowNull]
        public object? ValuePropertyName { get; set; }

        [Parameter]
        [PSAllowNull, AllowEmptyString]
        [ValidateScriptVariable(PSThisVariable.UNDERSCORE_NAME, PSThisVariable.PSITEM_NAME, PSThisVariable.THIS_NAME, PSThisVariable.ARGS_FIRST)]
        public ScriptBlock? ValueSelector { get; set; }

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

        protected override bool ProcessCore()
        {
            bool flag = true;
            object?[]? inputObjects = this.InputObject;
            if (inputObjects is null || inputObjects.Length == 0)
                return flag;

            _dictionary ??= this.CreateDictionary(inputObjects);

            unsafe
            {
                return this.AddToDictionary(inputObjects, (delegate*< ConvertToDictionaryCmdlet, object, object ?, void >)_addToDictionaryPtr);
            }
        }

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
                data["KeyType"] = _keyType;
                data[nameof(InputObject)] = inputObjects.DeepClone();
                data[nameof(ValueType)] = _valueType;
                data["DictionaryType"] = dictType;

                var rec = ex.ToRecord(ErrorCategory.InvalidOperation, targetObj: null);
                
                this.ThrowTerminatingError(rec);
                throw;
            }
        }

        private unsafe bool AddToDictionary(object?[] inputObjects, delegate*<ConvertToDictionaryCmdlet, object, object?, void> addToDictionaryAction)
        {
            foreach (object item in inputObjects.AsValueEnumerable().Where(x => x is not null)!)
            {
                try
                {
                    object? key = this.KeySelector.Invoke(item).FirstOrDefault()?.BaseObject;
                    if (key is null)
                        continue;

                    key = LanguagePrimitives.ConvertTo(key, _keyType);

                    object? value = this.ValueSelector?.Invoke(item).FirstOrDefault()?.BaseObject is object o
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

        protected override void EndCore(bool wantsToStop)
        {
            if (wantsToStop)
                return;

            if (_dictionary is null)
            {
                this.WriteObject(new Hashtable(StringComparer.OrdinalIgnoreCase));
                return;
            }

            this.WriteObject(_dictionary, enumerateCollection: false);
        }

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
        private static Type GetTypeForElement(object?[] inputObj, ScriptBlock? selector)
        {
            Type? type;
            if (selector is null)
            {
                type = inputObj[0].GetBaseObject()?.GetType();
            }
            else
            {
                var firstObj = selector.Invoke(inputObj).FirstOrDefault();
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
        private static void AddSkip(ConvertToDictionaryCmdlet cmdlet, object key, object? value)
        {
            if (cmdlet._dictionary.Contains(key))
            {
                cmdlet.WriteWarning("Key already exists, skipping value.");
                return;
            }

            cmdlet._dictionary.Add(key, value);
        }
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
}
