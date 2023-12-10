using ListFunctions.Extensions;
using ListFunctions.Modern;
using ListFunctions.Modern.Constructors;
using ListFunctions.Modern.Variables;
using ListFunctions.Validation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Management.Automation;
using System.Reflection;

namespace ListFunctions.Cmdlets.Construct
{
    [Cmdlet(VerbsCommon.New, "Dictionary", DefaultParameterSetName = "None")]
    [OutputType(typeof(Dictionary<,>), typeof(Hashtable))]
    public sealed class NewDictionaryCmdlet : EqualityConstructingCmdlet<IDictionary>, IDynamicParameters
    {
        const string CLONE_VALUES = "CloneValues";
        const string STR_DICT = "StringDict";

        bool _cloneValues;

        protected override string CaseSensitiveParameterSetName => STR_DICT;

        [Parameter]
        [ValidateRange(0, int.MaxValue)]
        [PSDefaultValue(Value = 0)]
        public override int Capacity
        {
            get => base.Capacity;
            set => base.Capacity = value;
        }

        [Parameter(ParameterSetName = JUST_COPY)]
        [Parameter(ParameterSetName = AND_COPY)]
        public SwitchParameter CloneValues
        {
            get => _cloneValues;
            set => _cloneValues = value;
        }

        [Parameter(Position = 0)]
        [ArgumentToTypeTransform]
        public Type KeyType { get; set; } = null!;

        [Parameter(Position = 1)]
        [ArgumentToTypeTransform]
        [PSDefaultValue(Value = typeof(object))]
        public Type ValueType { get; set; } = null!;

        [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = JUST_COPY)]
        [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = AND_COPY)]
        [Alias("CopyFrom")]
        public Hashtable InputObject { get; set; } = null!;

        [Parameter(Mandatory = true, ParameterSetName = WITH_CUSTOM_EQUALITY)]
        [Parameter(Mandatory = true, ParameterSetName = AND_COPY)]
        [ValidateScriptVariable(PSComparingVariable.X, PSComparingVariable.LEFT)]
        [ValidateScriptVariable(PSComparingVariable.Y, PSComparingVariable.RIGHT)]
        public ScriptBlock EqualityScript { get; set; } = null!;

        [Parameter(Mandatory = true, ParameterSetName = WITH_CUSTOM_EQUALITY)]
        [Parameter(Mandatory = true, ParameterSetName = AND_COPY)]
        [ValidateScriptVariable(PSThisVariable.UNDERSCORE_NAME, PSThisVariable.THIS_NAME, PSThisVariable.PSITEM_NAME)]
        public ScriptBlock HashCodeScript { get; set; } = null!;

        [Parameter(ParameterSetName = WITH_CUSTOM_EQUALITY)]
        [PSDefaultValue(Value = ActionPreference.Stop)]
        public override ActionPreference ScriptBlockErrorAction { get; set; } = ActionPreference.Stop;

        protected override void Process(IDictionary collection, Type collectionType)
        {
            if (null != this.InputObject && this.InputObject.Count > 0)
            {
                object?[] args = new object?[2];
                foreach (DictionaryEntry de in this.InputObject)
                {
                    args[0] = LanguagePrimitives.ConvertTo(de.Key, this.KeyType);
                    args[1] = CloneValue(de.Value, _cloneValues);

                    this.AddToCollection(collection, args, false);
                }
            }
        }
        protected override void End(IDictionary collection)
        {
            this.WriteObject(collection, false);
        }

        #region BACKEND
        protected override EqualityCollectionCtor GetConstructor(IEqualityComparer? comparer, Type[]? genericTypes)
        {
            return new DictionaryCtor(comparer, this.KeyType, this.ValueType)
            {
                IsCaseSensitive = this.CaseSensitive,
            };
        }

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

        private static MethodInfo GetAddMethod(Type genericBaseType)
        {
            return genericBaseType.GetMethod(nameof(Dictionary<object, object>.Add),
                bindingAttr: BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                types: genericBaseType.GetGenericArguments(),
                modifiers: null)!;
        }
        protected override IEqualityComparer? GetCustomEqualityComparer(Type genericType)
        {
            if (!WITH_CUSTOM_EQUALITY.Equals(this.ParameterSetName, StringComparison.InvariantCultureIgnoreCase))
            {
                return base.GetCustomEqualityComparer(genericType);
            }

            IHashCodeBlock hashBlock = HashCodeBlock.CreateBlock(genericType, this.HashCodeScript);
            ActionPreference errorPreference = this.ScriptBlockErrorAction;

            PSVariable[] additional = new PSVariable[] {
                new PSVariable(ERROR_ACTION_PREFERENCE, errorPreference) };

            return EqualityBlock.CreateBlock(genericType, hashBlock, this.EqualityScript, additional);
        }

        protected override Type GetEqualityForType()
        {
            return this.KeyType ??= typeof(object);
        }
        protected override Type[]? GetGenericTypes()
        {
            Type objType = typeof(object);
            this.KeyType ??= objType;
            this.ValueType ??= objType;

            return !objType.Equals(this.KeyType) || !objType.Equals(this.ValueType)
                ? new Type[] { this.KeyType, this.ValueType }
                : null;
        }
        private static MethodInfo GetHashtableAddMethod(Expression<Action<Hashtable>> addExpression)
        {
            return addExpression.Body is MethodCallExpression methodCall
                ? methodCall.Method
                : throw new ArgumentException("What the hell? That's not a method call...");
        }

        #endregion
    }
}
