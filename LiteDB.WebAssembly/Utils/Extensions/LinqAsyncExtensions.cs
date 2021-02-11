using System;
using System.Collections.Generic;
using System.Linq;

using static LiteDB.Constants;

namespace LiteDB
{
    internal static class LinqAsyncExtensions
    {
        public static async IAsyncEnumerable<T> Skip<T>(this IAsyncEnumerable<T> source, int count)
        {
            await foreach (var item in source)
            {
                if (--count < 0)
                {
                    yield return item;
                }
            }
        }

        public static async IAsyncEnumerable<T> Take<T>(this IAsyncEnumerable<T> source, int count)
        {
            await foreach (var item in source)
            {
                if (--count >= 0)
                {
                    yield return item;
                }
                else
                {
                    yield break;
                }
            }
        }
    }
}