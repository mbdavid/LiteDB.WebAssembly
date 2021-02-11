using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using static LiteDB.Constants;

namespace LiteDB
{
    internal static class LinqAsyncExtensions
    {
        public static async IAsyncEnumerable<T> SkipAsync<T>(this IAsyncEnumerable<T> source, int count)
        {
            await foreach (var item in source)
            {
                if (--count < 0)
                {
                    yield return item;
                }
            }
        }

        public static async IAsyncEnumerable<T> TakeAsync<T>(this IAsyncEnumerable<T> source, int count)
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

        public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source)
        {
            var result = new List<T>();

            await foreach (var item in source)
            {
                result.Add(item);
            }

            return result;
        }

        public static async Task<T[]> ToArrayAsync<T>(this IAsyncEnumerable<T> source)
        {
            return (await ToListAsync(source)).ToArray();
        }

        public static async Task<T> FirstAsync<T>(this IAsyncEnumerable<T> source)
        {
            await using var enumerator = source.GetAsyncEnumerator();

            if (await enumerator.MoveNextAsync())
            {
                return (T)enumerator.Current;
            }
            else
            {
                var e = new List<int>();

                throw new Exception("Element not found");
            }
        }

        public static async Task<T> FirstOrDefaultAsync<T>(this IAsyncEnumerable<T> source)
        {
            await using var enumerator = source.GetAsyncEnumerator();

            if (await enumerator.MoveNextAsync())
            {
                return (T)enumerator.Current;
            }
            else
            {
                return default(T);
            }
        }

        public static async Task<T> SingleAsync<T>(this IAsyncEnumerable<T> source)
        {
            await using var enumerator = source.GetAsyncEnumerator();

            if (await enumerator.MoveNextAsync())
            {
                var item = (T)enumerator.Current;

                if (await enumerator.MoveNextAsync()) throw new Exception("More than one element found");

                return item;
            }
            else
            {
                throw new Exception();
            }
        }

        public static async Task<T> SingleOrDefaultAsync<T>(this IAsyncEnumerable<T> source)
        {
            await using var enumerator = source.GetAsyncEnumerator();

            if (await enumerator.MoveNextAsync())
            {
                var item = (T)enumerator.Current;

                if (await enumerator.MoveNextAsync()) throw new Exception("More than one element found");

                return item;
            }
            else
            {
                return default(T);
            }
        }

        public static async Task<int> CountAsync<T>(this IAsyncEnumerable<T> source)
        {
            int count = 0;

            await foreach (var item in source)
            {
                count++;
            }

            return count;
        }

        public static async Task<long> LongCountAsync<T>(this IAsyncEnumerable<T> source)
        {
            long count = 0;

            await foreach (var item in source)
            {
                count++;
            }

            return count;
        }

        public static async Task<bool> AnyAsync<T>(this IAsyncEnumerable<T> source)
        {
            await using var enumerator = source.GetAsyncEnumerator();

            return (await enumerator.MoveNextAsync());
        }
    }
}