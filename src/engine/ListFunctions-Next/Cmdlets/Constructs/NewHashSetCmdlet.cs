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
using System.Management.Automation;
using System.Reflection;

namespace ListFunctions.Cmdlets.Construct
{
    [Cmdlet(VerbsCommon.New, "HashSet", DefaultParameterSetName = "None")]
    [OutputType(typeof(HashSet<>))]
    public sealed class NewHashSetCmdlet : EqualityConstructingCmdlet<object>, IDynamicParameters
    {
        const string DYN_PSET_NAME = "StringSet";

        protected override string CaseSensitiveParameterSetName => DYN_PSET_NAME;

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

        #region PROCESSING

        protected override void Process(object collection, Type collectionType)
        {
            if (!this.HasAddMethod || this.InputObject is null)
            {
                return;
            }

            object?[] args = new object[1];
            foreach (object? item in this.InputObject)
            {
                args[0] = item;
                this.AddToCollection(collection, args, (x, types) => 
                    LanguagePrimitives.ConvertTo(x, types[0]));
            }
        }

        protected override void End(object collection)
        {
            this.WriteObject(collection, false);
        }

        #endregion

        #region BACKEND
        protected override EqualityCollectionCtor GetConstructor(IEqualityComparer? comparer, Type[]? genericTypes)
        {
            return new HashSetCtor(genericTypes is null || genericTypes.Length <= 0
                ? typeof(object)
                : genericTypes[0], comparer)
            {
                IsCaseSensitive = this.CaseSensitive,
            };
        }
        protected override IEqualityComparer? GetCustomEqualityComparer(Type genericType)
        {
            if (!this.ParameterSetName.StartsWith(WITH_CUSTOM_EQUALITY, StringComparison.OrdinalIgnoreCase))
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
            return this.GenericType ??= typeof(object);
        }
        protected override Type[]? GetGenericTypes()
        {
            this.GenericType ??= typeof(object);

            return new Type[] { this.GenericType };
        }

        #endregion
    }
}
