using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Extensions
{
    public static class ListExtensions
    {
        public static List<T> Shuffle<T>(this IList<T> list)
        {
            if (list == null)
            {
                return new List<T>();
            }

            List<T> shuffled = new(list);
            int count = shuffled.Count;
            while (count > 1)
            {
                count--;
                int randomIndex = Random.Range(0, count + 1);
                (shuffled[randomIndex], shuffled[count]) = (shuffled[count], shuffled[randomIndex]);
            }

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
