namespace LFPublish.Internal
{
    [DebuggerStepThrough]
    internal ref struct SplitEnumerator
    {
        ReadOnlySpan<char> _str;
        readonly ReadOnlySpan<char> _splitBy;

        public SplitEntry Current { get; private set; }

        internal SplitEnumerator(ReadOnlySpan<char> str, ReadOnlySpan<char> splitBy)
        {
            _str = str;
            _splitBy = splitBy;
        }

        public readonly SplitEnumerator GetEnumerator() => this;

        public bool MoveNext()
        {
            ReadOnlySpan<char> span = _str;
            if (span.Length <= 0)
            {
                return false;
            }

            int index = span.IndexOf(_splitBy);
            if (index < 0)
            {
                _str = ReadOnlySpan<char>.Empty;
                this.Current = new SplitEntry(span, _splitBy);
                return true;
            }

            this.Current = new SplitEntry(span.Slice(0, index), span.Slice(index, _splitBy.Length));
            _str = span.Slice(index + _splitBy.Length);
            return true;
        }
    }

    [DebuggerStepThrough]
    internal readonly ref struct SplitEntry
    {
        internal ReadOnlySpan<char> Chars { get; }
        internal ReadOnlySpan<char> Separator { get; }

        internal SplitEntry(ReadOnlySpan<char> chars, ReadOnlySpan<char> separator)
        {
            this.Chars = chars;
            this.Separator = separator;
        }

        public void Deconstruct(out ReadOnlySpan<char> chars, out ReadOnlySpan<char> separator)
        {
            chars = this.Chars;
            separator = this.Separator;
        }

        public static implicit operator ReadOnlySpan<char>(SplitEntry entry)
        {
            return entry.Chars;
        }
    }

    [DebuggerStepThrough]
    internal ref struct DoubleSplitEnumerator
    {
        ReadOnlySpan<char> _str;
        readonly ReadOnlySpan<char> _splitBy1;
        readonly ReadOnlySpan<char> _splitBy2;

        public SplitEntry Current { get; private set; }

        internal DoubleSplitEnumerator(ReadOnlySpan<char> str, ReadOnlySpan<char> splitBy1, ReadOnlySpan<char> splitBy2)
        {
            _str = str;
            _splitBy1 = splitBy1;
            _splitBy2 = splitBy2;
        }

        public readonly DoubleSplitEnumerator GetEnumerator() => this;

        public bool MoveNext()
        {
            ReadOnlySpan<char> span = _str;
            if (span.Length <= 0)
            {
                return false;
            }

            int index = span.IndexOf(_splitBy1);
            if (index > -1)
            {
                this.Current = new SplitEntry(span.Slice(0, index), span.Slice(index, _splitBy1.Length));
                _str = span.Slice(index + _splitBy1.Length);
                return true;
            }

            index = span.IndexOf(_splitBy2);
            if (index > -1)
            {
                this.Current = new SplitEntry(span.Slice(0, index), span.Slice(index, _splitBy2.Length));
                _str = span.Slice(index + _splitBy2.Length);
                return true;
            }

            _str = ReadOnlySpan<char>.Empty;
            this.Current = new SplitEntry(span, default);
            return true;
        }
    }
}

