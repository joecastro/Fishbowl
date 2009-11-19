
namespace Standard
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Further LINQ extensions
    /// </summary>
    internal static class Enumerable2
    {
        public static IEnumerable<TResult> Zip<TFirst, TSecond, TResult>(this IEnumerable<TFirst> first, IEnumerable<TSecond> second, Func<TFirst, TSecond, TResult> func)
        {
            Verify.IsNotNull(first, "first");
            Verify.IsNotNull(second, "second");

            return _Zip(first, second, func);
        }

        private static IEnumerable<TResult> _Zip<TFirst, TSecond, TResult>(this IEnumerable<TFirst> first, IEnumerable<TSecond> second, Func<TFirst, TSecond, TResult> func)
        {
            IEnumerator<TFirst> ie1 = first.GetEnumerator();
            IEnumerator<TSecond> ie2 = second.GetEnumerator();
            while (ie1.MoveNext() && ie2.MoveNext())
            {
                yield return func(ie1.Current, ie2.Current);
            }
        }

        public static IEnumerable<T> Sublist<T>(this IEnumerable<T> enumerable, int startIndex, int endIndex)
        {
            Verify.IsNotNull(enumerable, "enumerable");
            Verify.BoundedInteger(0, startIndex, int.MaxValue, "startIndex");
            Verify.BoundedInteger(startIndex, endIndex, int.MaxValue, "endIndex");

            return _Sublist(enumerable, startIndex, endIndex);
        }

        private static IEnumerable<T> _Sublist<T>(this IEnumerable<T> enumerable, int startIndex, int endIndex)
        {
            int currentIndex = 0;
            IEnumerator<T> enumerator = enumerable.GetEnumerator();
            while (currentIndex < startIndex && enumerator.MoveNext())
            {
                ++currentIndex;
            }

            while (currentIndex < endIndex && enumerator.MoveNext())
            {
                yield return enumerator.Current;
                ++currentIndex;
            }
        }

        public static bool AreSorted<T>(this IEnumerable<T> enumerable)
        {
            return _AreSorted(enumerable);
        }

        public static bool AreSorted<T>(this IEnumerable<T> enumerable, Comparison<T> comparison)
        {
            Verify.IsNotNull(enumerable, "enumerable");
            if (comparison == null)
            {
                if (typeof(T).GetInterface(typeof(IComparable<T>).Name) == null)
                {
                    // Not comparable for a sort.
                    return true;
                }

                return _AreSorted(enumerable);
            }

            return _AreSorted(enumerable, comparison);
        }

        private static bool _AreSorted<T>(IEnumerable<T> enumerable, Comparison<T> comparison)
        {
            T last = default(T);
            bool isFirst = true;
            foreach (var item in enumerable)
            {
                if (isFirst)
                {
                    last = item;
                    isFirst = false;
                }
                else
                {
                    if (comparison(last, item) > 0)
                    {
                        return false;
                    }
                    last = item;
                } 
            }

            return true;
        }

        private static bool _AreSorted<T>(IEnumerable<T> enumerable)
        {
            IComparable<T> last = null;
            bool isFirstNonNull = true;
            foreach (var item in enumerable)
            {
                if (isFirstNonNull)
                {
                    last = (IComparable<T>)item;
                    if (last != null)
                    {
                        isFirstNonNull = false;
                    }
                }
                else
                {
                    if (last.CompareTo(item) > 0)
                    {
                        return false;
                    }
                    last = (IComparable<T>)item;
                    if (last == null)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

    }
}
