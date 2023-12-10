using ListFunctions.Extensions;
using ListFunctions.Modern;
using ListFunctions.Modern.Constructors;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Management.Automation;
using System.Reflection;

namespace ListFunctions.Cmdlets.Construct
{
    public abstract class EqualityConstructingCmdlet<T> : ListFunctionCmdletBase
    {
        protected const string CASE_SENSE = "CaseSensitive";
        protected const string JUST_COPY = "JustCopy";
        protected const string WITH_CUSTOM_EQUALITY = "WithCustomEquality";
        protected const string AND_COPY = WITH_CUSTOM_EQUALITY + "AndCopy";
        
        static readonly Type _stringType = typeof(string);
        static readonly string _addName = nameof(ICollection<object>.Add);

        private MethodInfo? _addMethod;
        private RuntimeDefinedParameter _caseSensitive = null!;
        private T _collection = default!;
        private Type _collectionType = null!;
        private RuntimeDefinedParameterDictionary? _dict = null!;
        private Type[] _genericTypes = null!;

        protected MethodInfo? AddMethod
        {
            get => _addMethod;
            set
            {
                _addMethod = value;
                this.HasAddMethod = !(_addMethod is null);
            }
        }
#if NET5_0_OR_GREATER
        [MemberNotNullWhen(true, nameof(_addMethod))]
#endif
        protected bool HasAddMethod { get; private set; }
        private RuntimeDefinedParameterDictionary DynParamLib
        {
            get => _dict ??= new RuntimeDefinedParameterDictionary();
        }
        protected abstract string CaseSensitiveParameterSetName { get; }

        public virtual int Capacity { get; set; }
        protected bool CaseSensitive => RetrieveCaseSensitiveSetting(_caseSensitive);
        public virtual ActionPreference ScriptBlockErrorAction { get; set; }

        public object? GetDynamicParameters()
        {
            this.DynParamLib.Clear();
            bool hasCase = this.TryGetDynamicCaseParam(this.GetEqualityForType(), this.CaseSensitiveParameterSetName);

            return this.TryGetDynamicParameters(this.DynParamLib, hasCase)
                ? this.DynParamLib
                : null;
        }

        #region PROCESSING
        protected sealed override void BeginProcessing()
        {
            Type[]? genericTypes = this.GetGenericTypes();
            IEqualityComparer? comparer = this.GetCustomEqualityComparer(this.GetEqualityForType());

            var ctor = this.GetConstructor(comparer, genericTypes);
            _collection = (T)ctor.Construct();

            _collectionType = ctor.ConstructingGenericType;
            this.Begin(_collection, _collectionType);
        }
        protected virtual void Begin(T collection, Type genericBaseType)
        {
            return;
        }

        protected sealed override void ProcessRecord()
        {
            this.Process(_collection, _collectionType);
        }
        protected virtual void Process(T collection, Type collectionType)
        {
            return;
        }

        protected sealed override void EndProcessing()
        {
            this.End(_collection);
        }
        protected virtual void End(T collection)
        {
            return;
        }

        #endregion

        #region BACKEND
        protected abstract EqualityCollectionCtor GetConstructor(IEqualityComparer? comparer, Type[]? genericTypes);

        protected virtual bool TryGetDynamicParameters(RuntimeDefinedParameterDictionary paramDict, bool hasCaseSensitive)
        {
            return hasCaseSensitive;
        }
        private bool TryGetDynamicCaseParam(Type genericType, string parameterSetName)
        {
            bool returnLib = false;
            if (EqualityCollectionCtor.IsTypeObjectOrString(genericType))
            {
                _caseSensitive ??= new RuntimeDefinedParameter(CASE_SENSE, typeof(SwitchParameter),
                    new Collection<Attribute>()
                    {
                        new ParameterAttribute()
                        {
                            Mandatory = true,
                            ParameterSetName = parameterSetName,
                        }
                    });

                returnLib = this.DynParamLib.TryAdd(CASE_SENSE, _caseSensitive);
            }

            return returnLib;
        }

        protected void AddToCollection(T collection, object?[]? items, Func<object?, Type[], object?> conversion)
        {
            if (_addMethod is null || items is null || items.Length < 1 || items[0] is null)
            {
                return;
            }

            for (int i = items.Length - 1; i >= 0; i--)
            {
                items[i] = conversion(items[i], _genericTypes);
            }

            try
            {
                _addMethod.Invoke(collection, items);
            }
            catch (Exception e)
            {
                this.WriteError(e.ToRecord(ErrorCategory.InvalidOperation, items));
            }
        }
        protected void AddToCollection(T collection, object?[]? item, bool addIfNull)
        {
            if (_addMethod is null || (item is null && !addIfNull))
            {
                return;
            }

            try
            {
                _addMethod.Invoke(collection, item);
            }
            catch (Exception e)
            {
                this.WriteError(e.ToRecord(ErrorCategory.InvalidOperation, item));
            }
        }

        private static MethodInfo? GetAddMethod(Type baseType, Type[] genericTypes)
        {
            try
            {
                return ReflectionResolver.GetAddMethod(baseType, genericTypes);
            }
            catch (Exception e)
            {
                Debug.Fail(e.Message);
                return null;
            }
        }
        
        protected virtual IEqualityComparer? GetCustomEqualityComparer(Type genericType)
        {
            return null;
        }
        protected abstract Type[]? GetGenericTypes();
        protected abstract Type GetEqualityForType();

        private static bool RetrieveCaseSensitiveSetting(RuntimeDefinedParameter? parameter)
        {
            return !(parameter is null) && parameter.Value is SwitchParameter swParam && swParam.ToBool();
        }

        #endregion
    }
}
