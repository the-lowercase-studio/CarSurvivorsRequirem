using System;
using UnityEngine;

namespace Assets.Scripts.Common.Types
{
    public interface IValueRange<T>
        where T : struct, IComparable<T>, IConvertible
    {
        public T GetRandomValueInRange();
    }

    [Serializable]
    public class ValueRange<T> : IValueRange<T>, ICloneable
        where T : struct, IComparable<T>, IConvertible
    {
        [field: SerializeField] public T Min { get; protected set; }
        [field: SerializeField] public T Max { get; protected set; }

        public ValueRange(T min, T max)
        {
            Min = min;
            Max = max;
        }

        public virtual ValueRange<T> Clone()
        {
            return new ValueRange<T>(Min, Max);
        }

        object ICloneable.Clone()
        {
            return Clone();
        }

        public T GetRandomValueInRange()
        {
            if (typeof(T) == typeof(float))
            {
                return (T)(object)(float)Math.Round(UnityEngine.Random.Range((float)(object)Min, (float)(object)Max), 2);
            }
            else if (typeof(T) == typeof(int))
            {
                return (T)(object)UnityEngine.Random.Range((int)(object)Min, (int)(object)Max + 1);
            }
            else
            {
                throw new InvalidOperationException("Unsupported type for random value generation.");
            }
        }
    }

    // For unity serialization we need to use nongeneric class.

    [Serializable]
    public class FloatValueRange : ValueRange<float>
    {
        public FloatValueRange(float min, float max) : base(min, max)
        {
        }

        public override ValueRange<float> Clone()
        {
            return new FloatValueRange(Min, Max);
        }
    }

    [Serializable]
    public class IntValueRange : ValueRange<int>
    {
        public IntValueRange(int min, int max) : base(min, max)
        {
        }

        public override ValueRange<int> Clone()
        {
            return new IntValueRange(Min, Max);
        }
    }
}
