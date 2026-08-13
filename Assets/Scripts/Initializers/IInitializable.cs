namespace Assets.Scripts.Initializers
{
    public interface IInitializable
    {
        void Initialize();

        bool IsInitialized();
    }

    public interface IInitializable<T>
    {
        void Initialize(T input);

        bool IsInitialized();
    }
}

