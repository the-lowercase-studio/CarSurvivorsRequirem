using System;

namespace Assets.Scripts.Common.EventArgs
{
    public class ValueEventArgs<T> : System.EventArgs
    {
        public T Value { get; }

        public ValueEventArgs(T value)
        {
            Value = value;
        }
    }
}
