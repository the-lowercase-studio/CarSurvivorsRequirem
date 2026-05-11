using System;

namespace Assets.Scripts.ObjectLifecycle.Actions
{
    public interface INeedToCompleteBeforeDisable
    {
        public event EventHandler OnCompleted;
    }
}
