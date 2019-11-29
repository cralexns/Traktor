using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Traktor.Core.Extensions
{
    public static class CollectionExtensions
    {
        public static V GetValueByKey<K,V>(this IDictionary<K,V> dic, K key)
        {
            if (dic.TryGetValue(key, out V value))
                return value;
            return default;
        }

        public static List<T> AddOrReplaceRange<T>(this List<T> list, IEnumerable<T> items, Func<T, T, bool> equalCondition)
        {
            foreach (var item in items)
            {
                var existingItem = list.FirstOrDefault(x=>equalCondition(x, item));
                if (existingItem != null)
                    list.Remove(existingItem);

                list.Add(item);
            }

            return list;
        }
    }
}
