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

#nullable enable

namespace ListFunctions.Cmdlets.Constructs
{
    [Cmdlet(VerbsCommon.New, "HashSet", DefaultParameterSetName = SPECIFIED_TYPE)]
    [OutputType(typeof(HashSet<>))]
    public sealed class NewHashSetCmdlet : EqualityConstructingCmdlet<object>, IDynamicParameters
    {
        private const string DYN_PSET_NAME = "StringSet";
        private const string SPECIFIED_TYPE = "SpecifiedType";

        protected override string CaseSensitiveParameterSetName => DYN_PSET_NAME;

        [Parameter, ValidateRange(0, int.MaxValue), PSDefaultValue(Value = 0)]
        public override int Capacity { get; set; }

        [Parameter(Mandatory = false, Position = 0, ParameterSetName = SPECIFIED_TYPE)]
        [ArgumentToTypeTransform, Alias("Type")]
        [PSDefaultValue(Value = typeof(object))]
        public Type GenericType { get; set; } = null!;

        [Parameter(ValueFromPipeline = true)]
        public object[] InputObject { get; set; } = null!;

        [Parameter(Mandatory = true, ParameterSetName = WITH_CUSTOM_EQUALITY), IsScriptBlock]
        [ValidateScriptVariable(PSComparingVariable.X, PSComparingVariable.LEFT, PSThisVariable.ARGS_FIRST)]
        [ValidateScriptVariable(PSComparingVariable.Y, PSComparingVariable.RIGHT, PSThisVariable.ARGS_SECOND)]
        public ScriptBlock EqualityScript { get; set; } = null!;

        [Parameter(Mandatory = true, ParameterSetName = WITH_CUSTOM_EQUALITY)]
        [IsScriptBlock, ValidateScriptVariable(PSThisVariable.UNDERSCORE_NAME, PSThisVariable.THIS_NAME, PSThisVariable.PSITEM_NAME, PSThisVariable.ARGS_FIRST)]
        public ScriptBlock HashCodeScript { get; set; } = null!;

        [Parameter(ParameterSetName = WITH_CUSTOM_EQUALITY)]
        [PSDefaultValue(Value = ActionPreference.Stop)]
        public override ActionPreference ScriptBlockErrorAction { get; set; } = ActionPreference.Stop;

        #region PROCESSING

        protected override bool Process(object collection, Type collectionType)
        {
            bool flag = true;
            if (this.InputObject is null || this.InputObject.Length == 0)
            {
                return flag;
            }

            if (collection is ICollection<object> objCol)
            {
                foreach (object item in this.InputObject)
                {
                    try
                    {
                        objCol.Add(item);
                    }
                    catch (Exception e)
                    {
                        var rec = e.ToRecord(ErrorCategory.InvalidOperation, item);
                        this.WriteError(rec);
                        flag = false;
                    }
                }
            }
            else
            {
                object?[] args = new object[1];
                foreach (object? item in this.InputObject)
                {
                    try
                    {
                        args[0] = item;
                        this.AddToCollection(collection, args, (x, types) =>
                            LanguagePrimitives.ConvertTo(x, types[0]));
                    }
                    catch (PSInvalidCastException e)
                    {
                        var rec = e.ToRecord(ErrorCategory.InvalidArgument, item);
                        this.WriteError(rec);
                    }
                    catch (Exception e)
                    {
                        var rec = e.ToRecord(ErrorCategory.InvalidOperation, item);
                        this.WriteError(rec);
                        flag = false;
                    }
                }

            }

            return flag;
        }

        protected override void End(object collection, bool wantsToStop)
        {
            if (wantsToStop)
                return;

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
            if (!WITH_CUSTOM_EQUALITY.Equals(this.ParameterSetName, StringComparison.OrdinalIgnoreCase))
            {
                return base.GetCustomEqualityComparer(genericType);
            }

            HashBlock hashBlock = new(this.HashCodeScript);
            ActionPreference errorPreference = this.ScriptBlockErrorAction;

            PSVariable variable = new(ERROR_ACTION_PREFERENCE, errorPreference);
#if NET9_0_OR_GREATER
            return new EqualityBlock(this.EqualityScript, hashBlock, variable);
#else
            return new EqualityBlock(this.EqualityScript, hashBlock, [variable]);
#endif
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
