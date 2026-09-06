using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Extensions
{
    public static class ListExtensions
    {
        public static void ShuffleInPlace<T>(this IList<T> list)
        {
            if (list == null)
            {
                return;
            }

            int count = list.Count;
            while (count > 1)
            {
                count--;
                int randomIndex = Random.Range(0, count + 1);
                (list[randomIndex], list[count]) = (list[count], list[randomIndex]);
            }
        }

        public static List<T> Shuffle<T>(this IList<T> list)
        {
            if (list == null)
            {
                return new List<T>();
            }

            List<T> shuffled = new(list);
            shuffled.ShuffleInPlace();
            return shuffled;
        }

        public static List<T> Shuffle<T>(this IEnumerable<T> collection)
        {
            if (collection == null)
            {
                return new List<T>();
            }

            List<T> list = new(collection);
            return list.Shuffle();
        }
    }
}
