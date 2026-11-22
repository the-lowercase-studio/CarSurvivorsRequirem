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
    }
}
