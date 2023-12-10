namespace LFPublish.Internal
{
    internal static class SpanExtensions
    {
        [DebuggerStepThrough]
        internal static SplitEnumerator SpanSplit(this ReadOnlySpan<char> value, ReadOnlySpan<char> splitBy)
        {
            return new SplitEnumerator(value, splitBy);
        }

        [DebuggerStepThrough]
        internal static bool StartsWith(this scoped ReadOnlySpan<char> span, in char value)
        {
            return span.StartsWith(
                new ReadOnlySpan<char>(in value),
                StringComparison.InvariantCultureIgnoreCase);
        }
    }
}

