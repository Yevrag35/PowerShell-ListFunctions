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
    [Cmdlet(VerbsCommon.New, "HashSet", DefaultParameterSetName = "None")]
    [OutputType(typeof(HashSet<>))]
    public sealed class NewHashSetCmdlet : EqualityConstructingCmdlet<object>, IDynamicParameters
    {
        private const string DYN_PSET_NAME = "StringSet";
        static readonly Type _hashType = typeof(HashSet<>);

        protected override Type BaseType => _hashType;

        [Parameter]
        [ValidateRange(0, int.MaxValue)]
        [PSDefaultValue(Value = 0)]
        public override int Capacity { get; set; }

        [Parameter(Mandatory = false, Position = 0)]
        [ArgumentToTypeTransform]
        [PSDefaultValue(Value = typeof(object))]
        [Alias("Type")]
        public Type GenericType { get; set; } = null!;

        [Parameter(ValueFromPipeline = true)]
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
        public override ActionPreference ScriptBlockErrorAction { get; set; } = ActionPreference.Stop;

        public object? GetDynamicParameters()
        {
            return this.GetDynamicCaseParam(this.GenericType, DYN_PSET_NAME);
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
        protected override Type[]? GetGenericTypes()
        {
            this.GenericType ??= typeof(object);

            return new Type[] { this.GenericType };
        }

        protected override Type GetEqualityForType()
        {
            return this.GenericType;
        }

        protected override void Process(object collection)
        {
            if (!this.HasAddMethod || this.InputObject is null)
            {
                return;
            }

            this.AddToCollection(collection, this.InputObject);
        }

        protected override void End(object collection)
        {
            this.WriteObject(collection, false);
        }

        protected override object ConstructOnTypesMissing(IEqualityComparer? comparer)
        {
            IEqualityComparer<object> genComparer = comparer is IEqualityComparer<object> gc
                ? gc
                : EqualityComparer<object>.Default;

            return new HashSet<object>(genComparer);
        }
    }
}
