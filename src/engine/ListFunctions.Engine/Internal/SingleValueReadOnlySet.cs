using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using ZLinq;

namespace ListFunctions.Internal
{
    internal static class SingleValueReadOnlySet
    {
        internal static SingleValueReadOnlySet<T> Create<T>(IEnumerable<T> collection, IEqualityComparer<T>? equalityComparer = null) where T : notnull
        {
            return new SingleValueReadOnlySet<T>(collection.AsValueEnumerable().First(), equalityComparer);
        }
        internal static SingleValueReadOnlySet<T> Create<T>(T value, IEqualityComparer<T>? equalityComparer = null) where T : notnull
        {
            return new SingleValueReadOnlySet<T>(value, equalityComparer);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct SingleValueReadOnlySet<T> : IReadOnlySet<T> where T : notnull
    {
        readonly IEqualityComparer<T> _equality;
        readonly T _value;

        public int Count => 1;
        internal bool IsEmpty => _value is null;
        internal T Value => _value;

        public SingleValueReadOnlySet(T value, IEqualityComparer<T>? equalityComparer)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            _equality = ResolveComparer(equalityComparer);
            _value = value;
        }

        private static IEqualityComparer<T> ResolveComparer(IEqualityComparer<T>? supplied)
        {
            if (supplied is null)
            {
                supplied = typeof(string).Equals(typeof(T))
                    ? (IEqualityComparer<T>)StringComparer.OrdinalIgnoreCase
                    : EqualityComparer<T>.Default;
            }

            return supplied;
        }

        public bool Contains(T item)
        {
            return !this.IsEmpty && _equality.Equals(_value, item);
        }

        public bool IsProperSubsetOf(IEnumerable<T> other)
        {
            Guard.NotNull(other, nameof(other));
            if (this.IsEmpty)
            {
                return other.AsValueEnumerable().Any();
            }

            var enumerator = other.AsValueEnumerable().GetEnumerator();
            DoubleBool dub = DoubleBool.InitializeNew();
            while (!dub && enumerator.MoveNext())
            {
                if (_equality.Equals(_value, enumerator.Current))
                {
                    dub.Bool1 = true;
                }
                else
                {
                    dub.Bool2 = true;
                }
            }

            return dub;
        }

        public bool IsProperSupersetOf(IEnumerable<T> other)
        {
            Guard.NotNull(other, nameof(other));
            if (this.IsEmpty)
            {
                return false;
            }

            return !other.AsValueEnumerable().Any();
        }

        public bool IsSubsetOf(IEnumerable<T> other)
        {
            Guard.NotNull(other, nameof(other));
            if (this.IsEmpty)
            {
                return true;
            }

            foreach (T item in other.AsValueEnumerable())
            {
                if (_equality.Equals(_value, item))
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsSupersetOf(IEnumerable<T> other)
        {
            Guard.NotNull(other, nameof(other));
            if (this.IsEmpty)
            {
                return !other.AsValueEnumerable().Any();
            }

            foreach (T item in other.AsValueEnumerable())
            {
                if (!_equality.Equals(_value, item))
                {
                    return false;
                }
            }

            return true;
        }

        public bool Overlaps(IEnumerable<T> other)
        {
            Guard.NotNull(other, nameof(other));
            if (this.IsEmpty)
            {
                return false;
            }

            foreach (T item in other.AsValueEnumerable())
            {
                if (_equality.Equals(_value, item))
                {
                    return true;
                }
            }

            return false;
        }

        public bool SetEquals(IEnumerable<T> other)
        {
            Guard.NotNull(other, nameof(other));
            if (this.IsEmpty)
            {
                return !other.AsValueEnumerable().Any();
            }

            if (other.TryGetCount(out int count) && count != 1)
            {
                return false;
            }

            return other.AsValueEnumerable().SequenceEqual(this, _equality);
        }

        public Enumerator GetEnumerator()
        {
            return new(_value);
        }
        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return this.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public struct Enumerator : IEnumerator<T>
        {
            readonly T _value;
            int _index;
            public readonly T Current => _value;
            readonly object? IEnumerator.Current => this.Current;

            internal Enumerator(T value)
            {
                _value = value;
                _index = -1;
            }

            public bool MoveNext()
            {
                if (_index == -1)
                {
                    _index = 0;
                    return true;
                }

                _index = 1;
                return false;
            }
            public void Reset()
            {
                _index = -1;
            }
            public readonly void Dispose()
            {
            }
        }
    }
}
