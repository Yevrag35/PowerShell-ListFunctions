using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace ListFunctions.Internal
{
    public readonly struct PipelineItem : IList
    {
        readonly bool _isNotEmpty;
        //readonly Type? _type;
        readonly object? _value;

        object? IList.this[int index]
        {
            get => index == 0 ? _value : throw new ArgumentOutOfRangeException("index");
            set => throw new NotSupportedException();
        }

        int ICollection.Count => _isNotEmpty ? 1 : 0;
        bool ICollection.IsSynchronized => false;
        bool IList.IsReadOnly => true;
        bool IList.IsFixedSize => true;
        object ICollection.SyncRoot => this;
        //public Type Type => _type ?? typeof(object);


#if NET6_0_OR_GREATER
        [MemberNotNullWhen(false, nameof(_value), nameof(Value))]
#endif
        public bool IsEmpty => !_isNotEmpty;
        public object? Value => _value;

        public PipelineItem(object? item)
        {
            _value = item;
            _isNotEmpty = !(item is null);
            //_type = item?.GetType() ?? typeof(object);
        }

        int IList.Add(object? value) => throw new NotSupportedException();
        public void AddToList(ref IList list)
        {
            list.Add(_value);
        }
        void IList.Clear() => throw new NotSupportedException();
        public bool Contains(object? value)
        {
            return (value is null && this.IsEmpty)
                   ||
                   (_isNotEmpty && _value!.Equals(value));
        }
        public void CopyTo(Array array, int index)
        {
            if (this.IsEmpty)
            {
                return;
            }

            array.SetValue(_value, index);
        }
        int IList.IndexOf(object? value)
        {
            if (this.IsEmpty && value is null)
            {
                return 0;
            }
            else if (_isNotEmpty && _value!.Equals(value))
            {
                return 0;
            }
            else
            {
                return -1;
            }
        }
        void IList.Insert(int index, object value) => throw new NotSupportedException();
        void IList.Remove(object? value) => throw new NotSupportedException();
        void IList.RemoveAt(int index) => throw new NotSupportedException();
        public IEnumerator GetEnumerator()
        {
            yield return _value;
        }
    }
}

