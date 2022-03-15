using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Text;

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

        protected static TValue GetFirstValue<TValue>(Collection<PSObject> collection, Func<object, TValue> castFunction,
            TValue defaultValue = default(TValue))
        {
            if (!(collection is null) && collection.Count > 0 && !(collection[0] is null))
            {
                try
                {
                    return castFunction(collection[0]);
                }
                catch
                {
                    return defaultValue;
                }
            }
            else
            {
                return defaultValue;
            }
        }
    }
}