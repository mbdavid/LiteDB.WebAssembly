using LiteDB.Engine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using static LiteDB.Constants;

namespace LiteDB
{
    /// <summary>
    /// Implement some Enumerable methods to IBsonDataReader
    /// </summary>
    public static class BsonDataReaderExtensions
    {
        public static async IAsyncEnumerable<BsonValue> ToAsyncEnumerable(this IBsonDataReader reader)
        {
            try
            {
                while (await reader.ReadAsync())
                {
                    yield return reader.Current;
                }
            }
            finally
            {
                await reader.DisposeAsync();
            }
        }

        //public static Task<BsonValue[]> ToArrayAsync(this IBsonDataReader reader) => ToAsyncEnumerable(reader).ToArray();
        //
        //public static Task<IList<BsonValue>> ToListAsync(this IBsonDataReader reader) => ToEnumerable(reader).ToList();
        //
        //public static Task<BsonValue> FirstAsync(this IBsonDataReader reader) => ToEnumerable(reader).First();
        //
        //public static Task<BsonValue> FirstOrDefaultAsync(this IBsonDataReader reader) => ToEnumerable(reader).FirstOrDefault();
        //
        //public static Task<BsonValue> SingleAsync(this IBsonDataReader reader) => ToEnumerable(reader).Single();
        //
        //public static Task<BsonValue> SingleOrDefaultAsync(this IBsonDataReader reader) => ToAsyncEnumerable(reader).SingleOrDefault();
    }
}