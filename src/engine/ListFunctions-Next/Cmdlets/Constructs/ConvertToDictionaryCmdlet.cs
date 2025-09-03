using ListFunctions.Extensions;
using ListFunctions.Modern.Variables;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using ZLinq;

#nullable enable

namespace ListFunctions.Cmdlets.Constructs
{
    [Cmdlet(VerbsData.ConvertTo, "Dictionary", DefaultParameterSetName = "None")]
    public sealed class ConvertToDictionaryCmdlet : ListFunctionCmdletBase
    {
        [Parameter(Mandatory = true, ValueFromPipeline = true)]
        [AllowEmptyCollection, AllowNull]
        public object[] InputObject { get; set; } = null!;

        [Parameter(Mandatory = false)]
        public IEqualityComparer? KeyComparer { get; set; }

        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "KeyProperty")]
        [Alias("KeyName", "Key")]
        public string KeyPropertyName { get; set; } = string.Empty;

        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "KeyScript")]
        public ScriptBlock KeySelector { get; set; } = null!;

        [Parameter(Mandatory = false, Position = 1)]
        [Alias("ValueName", "Value")]
        [AllowEmptyString, AllowNull]
        public string ValuePropertyName { get; set; } = string.Empty;

        [Parameter(Mandatory = false)]
        [AllowNull]
        public ScriptBlock ValueSelector { get; set; } = null!;

        private IDictionary _dictionary = null!;
        private Type _keyType = null!;
        private IEqualityComparer _keyComparer = null!;
        private Type _valueType = null!;

        protected override void BeginProcessing()
        {
            if (this.ParameterSetName.StartsWith("KeyProperty", StringComparison.Ordinal))
            {
                this.KeySelector = ScriptBlock.Create(string.Concat("$args[0].'", this.KeyPropertyName, "'"));
                this.KeyPropertyName = string.Empty;
            }
            else if (!(this.KeySelector?.Ast.Find(x => x is VariableExpressionAst variable && PSThisVariable.UNDERSCORE_NAME.Equals(variable.VariablePath.UserPath, StringComparison.Ordinal), searchNestedScriptBlocks: false) is null))
            {
                string sc = this.KeySelector.ToString().Trim();
                if ("$_".Equals(sc, StringComparison.Ordinal))
                {
                    throw new ArgumentException("When using a script block for the key selector, you cannot use '$_' as the entire script block. Use '$args[0]' or just reference properties directly.");
                }
                else
                {
                    this.KeySelector = ScriptBlock.Create(sc.Replace("$_", "$args[0]"));
                }
            }

            if (!string.IsNullOrWhiteSpace(this.ValuePropertyName))
            {
                this.ValueSelector = ScriptBlock.Create(string.Concat("$args[0].'", this.ValuePropertyName, "'"));
                this.ValuePropertyName = string.Empty;
            }
            else if (!(this.ValueSelector?.Ast.Find(x => x is VariableExpressionAst variable && PSThisVariable.UNDERSCORE_NAME.Equals(variable.VariablePath.UserPath, StringComparison.Ordinal), searchNestedScriptBlocks: false) is null))
            {
                string sc = this.ValueSelector.ToString().Trim();
                if ("$_".Equals(sc, StringComparison.Ordinal))
                {
                    this.ValueSelector = null!;
                }
                else
                {
                    this.ValueSelector = ScriptBlock.Create(sc.Replace("$_", "$args[0]"));
                }
            }

            if (!(this.InputObject is null) && this.InputObject.Length > 0)
            {
                _keyType = GetTypeForElement(this.InputObject, this.KeySelector);
                _valueType = GetTypeForElement(this.InputObject, this.ValueSelector);
            }
        }
        protected override void ProcessRecord()
        {
            if (this.InputObject is null || this.InputObject.Length == 0)
                return;

            if (_dictionary is null)
            {
                if (_keyType is null || _valueType is null)
                {
                    _keyType = GetTypeForElement(this.InputObject, this.KeySelector);
                    _valueType = GetTypeForElement(this.InputObject, this.ValueSelector);
                }

                if (this.KeyComparer is null && (_keyType.Equals(typeof(string)) || _keyType.Equals(typeof(object))))
                {
                    this.KeyComparer = StringComparer.OrdinalIgnoreCase;
                }

                Type dictType = typeof(Dictionary<,>).MakeGenericType(_keyType, _valueType);
                _dictionary = (IDictionary)Activator.CreateInstance(dictType, new object?[] { this.KeyComparer })!;
            }

            foreach (object item in this.InputObject.AsValueEnumerable().Where(x => !(x is null)))
            {
                try
                {
                    object? key = this.KeySelector.Invoke(item).FirstOrDefault()?.BaseObject;
                    if (key is null)
                        continue;

                    key = LanguagePrimitives.ConvertTo(key, _keyType);

                    object? value = !(this.ValueSelector is null) && this.ValueSelector.Invoke(item).FirstOrDefault()?.BaseObject is object o
                        ? LanguagePrimitives.ConvertTo(o, _valueType)
                        : item;

                    _dictionary.Add(key, value);
                }
                catch (Exception e)
                {
                    var rec = e.ToRecord(ErrorCategory.InvalidArgument, item);
                    this.WriteError(rec);
                }
            }
        }
        protected override void EndProcessing()
        {
            if (_dictionary is null)
            {
                this.WriteObject(new Hashtable(StringComparer.OrdinalIgnoreCase));
                return;
            }

            this.WriteObject(_dictionary, enumerateCollection: false);
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
    }
}
