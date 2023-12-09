using ListFunctions.Extensions;
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
    [Cmdlet(VerbsCommon.New, "Dictionary", DefaultParameterSetName = "None")]
    [OutputType(typeof(Dictionary<,>), typeof(Hashtable))]
    public sealed class NewDictionaryCmdlet : EqualityConstructingCmdlet<IDictionary>, IDynamicParameters
    {
        static readonly Type _baseType = typeof(Dictionary<,>);
        RuntimeDefinedParameterDictionary _dict = null!;
        RuntimeDefinedParameter _caseSensitive = null!;

        protected override Type BaseType => _baseType;

        [Parameter]
        [ValidateRange(0, int.MaxValue)]
        [PSDefaultValue(Value = 0)]
        public override int Capacity
        {
            get => base.Capacity;
            set => base.Capacity = value;
        }

        [Parameter(Position = 0)]
        [ArgumentToTypeTransform]
        public Type KeyType { get; set; } = null!;

        [Parameter(Position = 1)]
        [ArgumentToTypeTransform]
        [PSDefaultValue(Value = typeof(object))]
        public Type ValueType { get; set; } = null!;

        [Parameter(ValueFromPipeline = true)]
        [Alias("CopyFrom")]
        public Hashtable InputObject { get; set; } = null!;

        [Parameter(Mandatory = true, ParameterSetName = WITH_CUSTOM_EQUALITY)]
        [ValidateScriptVariable(PSComparingVariable.X, PSComparingVariable.LEFT)]
        [ValidateScriptVariable(PSComparingVariable.Y, PSComparingVariable.RIGHT)]
        public ScriptBlock EqualityScript { get; set; } = null!;

        [Parameter(Mandatory = true, ParameterSetName = WITH_CUSTOM_EQUALITY)]
        [ValidateScriptVariable(PSThisVariable.UNDERSCORE_NAME, PSThisVariable.THIS_NAME, PSThisVariable.PSITEM_NAME)]
        public ScriptBlock HashCodeScript { get; set; } = null!;

        [Parameter(ParameterSetName = WITH_CUSTOM_EQUALITY)]
        [PSDefaultValue(Value = ActionPreference.Stop)]
        public override ActionPreference ScriptBlockErrorAction { get; set; } = ActionPreference.Stop;

        protected override void Process(IDictionary collection)
        {
            return;
        }
        protected override void End(IDictionary collection)
        {
            this.WriteObject(collection, false);
        }

        #region BACKEND
        protected override IDictionary ConstructOnTypesMissing(IEqualityComparer? comparer)
        {
            comparer ??= StringComparer.InvariantCultureIgnoreCase;

            return new Hashtable(this.Capacity, comparer);
        }
        protected override object[]? GetConstructorArguments(Type[] genericTypes, IEqualityComparer? comparer)
        {
            return new object[] { this.Capacity, comparer! };
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

        public object? GetDynamicParameters()
        {
            if (typeof(string).Equals(this.KeyType))
            {
                _caseSensitive ??= new RuntimeDefinedParameter(CASE_SENSE, typeof(SwitchParameter),
                    new Collection<Attribute>()
                    {
                        new ParameterAttribute()
                        {
                            Mandatory = true,
                            ParameterSetName = "StringDictionary"
                        }
                    });

                _dict ??= new RuntimeDefinedParameterDictionary();

                _dict.TryAdd(CASE_SENSE, _caseSensitive);
                return _dict;
            }

            return null;
        }

        protected override Type GetEqualityForType()
        {
            return this.KeyType;
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

        //private static


        #endregion
    }
}
