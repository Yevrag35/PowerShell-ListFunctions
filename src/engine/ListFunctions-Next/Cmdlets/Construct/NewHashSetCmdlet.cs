using ListFunctions.Modern;
using ListFunctions.Modern.Variables;
using ListFunctions.Validation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;
using System.Reflection;

namespace ListFunctions.Cmdlets.Construct
{
    [Cmdlet(VerbsCommon.New, "HashSet", DefaultParameterSetName = "None")]
    [OutputType(typeof(HashSet<>))]
    public sealed class NewHashSetCmdlet : ListFunctionCmdletBase, IDynamicParameters
    {
        static readonly Type _hashType = typeof(HashSet<>);
        RuntimeDefinedParameterDictionary _dict = null!;
        RuntimeDefinedParameter _caseSensitive = null!;
        const string CASE_SENSE = "CaseSensitive";
        const string WITH_CUSTOM_EQUALITY = "WithCustomEquality";

        object _set = null!;
        Type _setType = null!;
        MethodInfo? _addMethod;
        Type _type = null!;
        object[] _args = null!;

#if NET5_0_OR_GREATER
        [MemberNotNullWhen(true, nameof(_addMethod))]
#endif
        private bool HasAddMethod { get; set; }

        [Parameter]
        [ValidateRange(0, int.MaxValue)]
        [PSDefaultValue(Value = 0)]
        public int Capacity { get; set; }

        [Parameter(Mandatory = false, Position = 0)]
        [ArgumentToTypeTransform]
        [PSDefaultValue(Value = typeof(object))]
        [Alias("Type")]
        public Type GenericType
        {
            get => _type ??= typeof(object);
            set => _type = value;
        }

        [Parameter(ValueFromPipeline = true)]
        [ValidateNotNull]
        public object[] InputObject { get; set; } = null!;

        [Parameter(Mandatory = true, ParameterSetName = WITH_CUSTOM_EQUALITY)]
        [ValidateScriptVariable(PSComparingVariable.X, PSComparingVariable.LEFT)]
        [ValidateScriptVariable(PSComparingVariable.Y, PSComparingVariable.RIGHT)]
        public ScriptBlock EqualityScript { get; set; } = null!;

        [Parameter(Mandatory = true, ParameterSetName = WITH_CUSTOM_EQUALITY)]
        [ValidateScriptVariable(PSThisVariable.UNDERSCORE_NAME, PSThisVariable.THIS_NAME, PSThisVariable.PSITEM_NAME)]
        public ScriptBlock HashCodeScript { get; set; } = null!;

        [Parameter(ParameterSetName = WITH_CUSTOM_EQUALITY)]
        [PSDefaultValue(Value = ActionPreference.Stop)]
        public ActionPreference ScriptBlockErrorAction { get; set; } = ActionPreference.Stop;

        public object? GetDynamicParameters()
        {
            if (typeof(string).Equals(this.GenericType))
            {
                _caseSensitive ??= new RuntimeDefinedParameter(CASE_SENSE, typeof(SwitchParameter),
                    new Collection<Attribute>()
                    {
                        new ParameterAttribute()
                        {
                            Mandatory = true,
                            ParameterSetName = "StringSet"
                        }
                    });

                _dict ??= new RuntimeDefinedParameterDictionary();

#if NET5_0_OR_GREATER
                _dict.TryAdd(CASE_SENSE, _caseSensitive);
#else
                if (!_dict.ContainsKey(CASE_SENSE))
                {
                    _dict.Add(CASE_SENSE, _caseSensitive);
                }
#endif
                return _dict;
            }

            return null;
        }

        protected override void BeginProcessing()
        {
            _set = this.CreateNewSet(this.Capacity, this.GenericType, out _setType);

            if (TryGetAddMethod(_setType, out MethodInfo? addMethod))
            {
                _args = new object[1];
                _addMethod = addMethod;
                this.HasAddMethod = true;
            }
        }

        private object CreateNewSet(int capacity, Type genericType, out Type genHashType)
        {
            genHashType = _hashType.MakeGenericType(genericType);
            object[] args;
            if (WITH_CUSTOM_EQUALITY.Equals(this.ParameterSetName, StringComparison.InvariantCultureIgnoreCase))
            {
                IEqualityBlock equality = CreateCustomEqualityComparer(
                    genericType, this.HashCodeScript, this.EqualityScript, ActionPreference.Stop);

                args = new object[] { capacity, equality };
            }
            else
            {
                IEqualityComparer? defEquality = GetDefaultComparer(genericType, caseSensitive: (_caseSensitive?.Value as SwitchParameter?).GetValueOrDefault());
                args = new object[] { capacity, defEquality! };
            }

            return Activator.CreateInstance(genHashType, args)!;
        }

        private static IEqualityBlock CreateCustomEqualityComparer(Type genericType, ScriptBlock hashCodeScript, ScriptBlock equalityScript, ActionPreference errorPreference)
        {
            IHashCodeBlock hashBlock = HashCodeBlock.CreateBlock(genericType, hashCodeScript);
            PSVariable[] additional = new PSVariable[] { new PSVariable(ERROR_ACTION_PREFERENCE, errorPreference) };

            return EqualityBlock.CreateBlock(genericType, hashBlock, equalityScript, additional);
        }

        static readonly Type _defComparerType = typeof(EqualityComparer<>);
        private static IEqualityComparer? GetDefaultComparer(Type genericType, bool caseSensitive)
        {
            if (typeof(string).Equals(genericType))
            {
                return !caseSensitive
                    ? StringComparer.InvariantCultureIgnoreCase
                    : StringComparer.InvariantCulture;
            }

            var genStaticType = _defComparerType.MakeGenericType(genericType);

            PropertyInfo? defaultProp = genStaticType.GetProperty(
                nameof(EqualityComparer<object>.Default), BindingFlags.Static | BindingFlags.Public);

            return (IEqualityComparer?)defaultProp?.GetValue(null);
        }

        protected override void ProcessRecord()
        {
            if (!this.HasAddMethod || this.InputObject is null)
            {
                return;
            }

            foreach (object? item in this.InputObject)
            {
                this.AddItemToSet(item, _addMethod!);
            }
        }

        private void AddItemToSet(object? item, MethodInfo addMethod)
        {
            if (!(item is null) && LanguagePrimitives.TryConvertTo(item, _type, out object? converted))
            {
                _args[0] = converted;
                try
                {
                    addMethod.Invoke(_set, _args);
                }
                catch (Exception e)
                {
                    Debug.Fail(e.Message);
                    this.ThrowTerminatingError(new ErrorRecord(e, e.GetType().FullName, ErrorCategory.InvalidOperation, item));
                }
            }
        }

        protected override void EndProcessing()
        {
            this.WriteObject(_set, false);
        }

#if NET5_0_OR_GREATER
        private static bool TryGetAddMethod(Type setType, [NotNullWhen(true)] out MethodInfo? addMethod)
#else
        private static bool TryGetAddMethod(Type setType, out MethodInfo? addMethod)
#endif
        {
            addMethod = null;
            try
            {
                addMethod = setType.GetMethod("Add", BindingFlags.Public | BindingFlags.Instance);
            }
            catch (Exception e)
            {
                Debug.Fail(e.Message);
            }

            return !(addMethod is null);
        }
    }
}
