using LiteDB;
using LiteDB.Engine;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ConsoleApp
{
    class Program
    {
        private const string DATA_PATH = @"C:\Git\Data\_litedb-async.db";
        private static readonly Random _rnd = new Random();

        static void Main(string[] args)
        {
            MainAsync(args).Wait();

            Console.WriteLine("End");
            Console.ReadKey();
        }

        static async Task MainAsync(string[] args)
        {
            //File.Delete(DATA_PATH);

            //using (var stream = new FileStream(DATA_PATH, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 8192, FileOptions.Asynchronous))
            using (var stream = new MemoryStream())
            await using (var db = new LiteEngine(stream))
            {
                await db.OpenAsync();

                var docs = Enumerable.Range(1, 1000).Select(i => new BsonDocument()
                {
                    ["Name"] = "Bulk " + i,
                    ["Salary"] = _rnd.NextDouble() * 10000
                });

                var dt = Stopwatch.StartNew();

                await db.InsertAsync("col1", docs, BsonAutoId.Guid);

                Console.WriteLine("Tempo: " + dt.ElapsedMilliseconds);

                /*

                await db.PragmaAsync("CHECKPOINT", 0);

                var count = await db.InsertAsync("col1", new[]
                {
                    new BsonDocument { ["name"] = "John" },
                    new BsonDocument { ["name"] = "Doe" },
                },
                BsonAutoId.Int32);

                await db.CheckpointAsync();
                */

                //Console.WriteLine("Inserted: " + count);
            }
            

            //using (var stream = new FileStream(DATA_PATH, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 8192, FileOptions.Asynchronous))
            //await using (var db = new LiteEngine(stream))
            //{
            //    await db.OpenAsync();
            //    /*
            //
            //    var q = new Query();
            //    //q.Where.Add("_id = 1");
            //
            //    var dados = await db.QueryAsync("col1", q);
            //
            //    await foreach(var doc in dados.ToAsyncEnumerable())
            //    {
            //        Console.WriteLine(doc.ToString());
            //    }
            //    */
            //    await db.CheckpointAsync();
            //}




        }
    }
}
