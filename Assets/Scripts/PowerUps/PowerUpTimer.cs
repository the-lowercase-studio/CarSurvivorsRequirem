namespace Assets.Scripts.PowerUps
{
    /// <summary>
    /// Scaled-time countdown shared by timed power-ups. The owner supplies the delta time.
    /// </summary>
    public class PowerUpTimer
    {
        public float Duration { get; private set; }
        public float Remaining { get; private set; }
        public bool IsRunning { get; private set; }

        public void Start(float duration)
        {
            Duration = duration;
            Remaining = duration;
            IsRunning = duration > 0f;
        }

        public void Stop()
        {
            Remaining = 0f;
            IsRunning = false;
        }

        /// <returns>True exactly once, on the tick that expires the timer.</returns>
        public bool Tick(float deltaTime)
        {
            if (!IsRunning)
            {
                return false;
            }

            Remaining -= deltaTime;
            if (Remaining > 0f)
            {
                return false;
            }

            Remaining = 0f;
            IsRunning = false;
            return true;
        }

        public bool IsInWindow(float window)
        {
            return IsRunning && Remaining <= window;
        }
    }
}
