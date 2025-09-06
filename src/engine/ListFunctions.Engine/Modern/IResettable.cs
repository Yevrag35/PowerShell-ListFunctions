namespace ListFunctions.Modern
{
    public interface IPoolable : IResettable
    {
        void Initialize();
    }

    public interface IResettable
    {
        bool TryReset();
    }
}

