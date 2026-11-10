namespace Assets.Scripts.Indicators
{
    public interface ITelegraphIndicator
    {
        void ContractAndDismiss();
        void Dismiss();
        void SetFreezable(bool isFreezable);
    }
}
