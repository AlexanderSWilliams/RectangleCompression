using RectangleCompression.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RectangleCompression.IEnumerableExtensions
{
    public static class IEnumerableExtensions
    {
        public static int IndexOf<TSource>(this IEnumerable<TSource> source,
            Func<TSource, bool> predicate)
        {
            int i = 0;

            foreach (TSource element in source)
            {
                if (predicate(element))
                    return i;

                i++;
            }

            return -1;
        }

        public static IGrouping<TKey, TSource> MaxBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> selector, IComparer<TKey> comparer = null)
        {
            comparer = comparer ?? Comparer<TKey>.Default;
            using (var sourceIterator = source.GetEnumerator())
            {
                if (!sourceIterator.MoveNext())
                    return null;

                var MaximumElement = sourceIterator.Current;
                var MaximumKey = selector(MaximumElement);
                var MaximumElements = new List<TSource> { MaximumElement };

                while (sourceIterator.MoveNext())
                {
                    var CurrentElement = sourceIterator.Current;
                    var CurrentKey = selector(CurrentElement);
                    var CompareValue = comparer.Compare(CurrentKey, MaximumKey);
                    if (CompareValue > 0)
                    {
                        MaximumKey = CurrentKey;
                        MaximumElements = new List<TSource> { CurrentElement };
                    }
                    else if (CompareValue == 0)
                    {
                        MaximumElements.Add(CurrentElement);
                    }
                }
                return Grouping.Create(MaximumKey, MaximumElements);
            }
        }

        public static IGrouping<TKey, TSource> MinBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> selector, IComparer<TKey> comparer = null)
        {
            comparer = comparer ?? Comparer<TKey>.Default;
            using (var sourceIterator = source.GetEnumerator())
            {
                if (!sourceIterator.MoveNext())
                    return null;

                var MinimumElement = sourceIterator.Current;
                var MinimumKey = selector(MinimumElement);
                var MinimumElements = new List<TSource> { MinimumElement };
                while (sourceIterator.MoveNext())
                {
                    var CurrentElement = sourceIterator.Current;
                    var CurrentKey = selector(CurrentElement);
                    var CompareValue = comparer.Compare(CurrentKey, MinimumKey);
                    if (CompareValue < 0)
                    {
                        MinimumKey = CurrentKey;
                        MinimumElements = new List<TSource> { CurrentElement };
                    }
                    else if (CompareValue == 0)
                    {
                        MinimumElements.Add(CurrentElement);
                    }
                }
                return Grouping.Create(MinimumKey, MinimumElements);
            }
        }
    }
}