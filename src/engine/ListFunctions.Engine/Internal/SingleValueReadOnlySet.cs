using MG.Collections;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ListFunctions.Internal
{
    internal readonly struct SingleValueReadOnlySet<T> : IReadOnlySet<T> where T : notnull
    {
        readonly IEqualityComparer<T> _equality;
        readonly bool _isNotEmpty;
        readonly T _value;

        public int Count => 1;
        internal bool IsEmpty => !_isNotEmpty;
        internal T Value => _value;

        public SingleValueReadOnlySet(T value, IEqualityComparer<T>? equalityComparer)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            _equality = ResolveComparer(equalityComparer);
            _value = value;
            _isNotEmpty = true;
        }

        private static IEqualityComparer<T> ResolveComparer(IEqualityComparer<T>? supplied)
        {
            if (supplied is null)
            {
                supplied = typeof(string).Equals(typeof(T))
                    ? (IEqualityComparer<T>)StringComparer.InvariantCultureIgnoreCase
                    : EqualityComparer<T>.Default;
            }

            return supplied;
        }

        public bool Contains(T item)
        {
            if (this.IsEmpty || item is null)
            {
                return false;
            }

            return _equality.Equals(_value, item);
        }

        public bool IsProperSubsetOf(IEnumerable<T> other)
        {
            Guard.NotNull(other, nameof(other));
            if (this.IsEmpty)
            {
                return other.Any();
            }

            DoubleBool dub = DoubleBool.InitializeNew();
            foreach (T item in other)
            {
                if (dub)
                {
                    return true;
                }

                if (_equality.Equals(_value, item))
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

            return !other.Any();
        }

        public bool IsSubsetOf(IEnumerable<T> other)
        {
            Guard.NotNull(other, nameof(other));
            if (this.IsEmpty)
            {
                return true;
            }

            foreach (T item in other)
            {
                if (!(item is null) && _equality.Equals(_value, item))
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
                return !other.Any();
            }

            foreach (T item in other)
            {
                if (item is null || !_equality.Equals(_value, item))
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

            foreach (T item in other)
            {
                if (!(item is null) && _equality.Equals(_value, item))
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
                return !other.Any();
            }

            if (other.TryGetCount(out int count) && count != 1)
            {
                return false;
            }

            foreach (T item in other)
            {
                count++;
                if (count != 1 || item is null || !_equality.Equals(_value, item))
                {
                    return false;
                }
            }

            return count == 1;
        }

        public IEnumerator<T> GetEnumerator()
        {
            if (_isNotEmpty)
            {
                yield return _value;
            }
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
