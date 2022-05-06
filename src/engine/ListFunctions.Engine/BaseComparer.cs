using ListFunctions.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;

namespace ListFunctions
{
    public abstract class BaseComparer<T>
    {
        protected IComparer<T> DefaultComparer { get; }
        protected IEqualityComparer<T> DefaultEqualityComparer { get; }

        public bool IsStringType { get; }
        public bool IsValueType { get; }

        public BaseComparer()
        {
            Type tt = typeof(T);
            this.IsStringType = tt.Equals(typeof(string));
            this.IsValueType = tt.IsValueType;

            if (this.IsStringType)
            {
                this.DefaultEqualityComparer = (IEqualityComparer<T>)StringComparer.CurrentCultureIgnoreCase;
                this.DefaultComparer = (IComparer<T>)StringComparer.CurrentCultureIgnoreCase;
            }
            else
            {
                this.DefaultComparer = Comparer<T>.Default;
                this.DefaultEqualityComparer = EqualityComparer<T>.Default;
            }
        }

        [return: MaybeNull]
        protected static TValue GetFirstValue<TValue>(Collection<PSObject> collection, Func<object, TValue> castFunction,
            [MaybeNull] TValue defaultValue = default)
        {
            TValue outVal;
            try
            {
                outVal = collection.GetFirstValue(castFunction);
            }
            catch
            {
                outVal = defaultValue;
            }

            return outVal;
        }
    }
}