namespace Assets.Scripts.ObjectLifecycle.Actions
{
    public interface IEnableDisableFunctionalityTrigger<T>
        where T : IEnableDisableFunctionalityTrigger<T>
    {
        void EnableFunctionality();

        void DisableFunctionality();
    }
}
