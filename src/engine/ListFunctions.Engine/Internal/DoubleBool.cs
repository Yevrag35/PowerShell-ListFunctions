namespace ListFunctions.Internal
{
    internal ref struct DoubleBool
    {
        internal bool Bool1;
        internal bool Bool2;

        private DoubleBool(bool initialize)
        {
            Bool1 = initialize;
            Bool2 = initialize;
        }

        internal static DoubleBool InitializeNew() => new DoubleBool(false);

        public static implicit operator bool(DoubleBool dub) => dub.Bool1 && dub.Bool2;
    }
}
