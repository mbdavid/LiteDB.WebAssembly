using LiteDB;
using LiteDB.Engine;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ConsoleApp
{
    class Program
    {
        private const string DATA_PATH = @"C:\Git\Data\_litedb-async.db";

        static void Main(string[] args)
        {
            MainAsync(args).Wait();

            Console.WriteLine("End");
            Console.ReadKey();
        }

        static async Task MainAsync(string[] args)
        {
            //File.Delete(DATA_PATH);
            /*
            using (var stream = new FileStream(DATA_PATH, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 8192, FileOptions.Asynchronous))
            using (var db = new LiteEngine(stream))
            {
                await db.OpenAsync();

                var count = await db.InsertAsync("col1", new[]
                {
                    new BsonDocument { ["name"] = "John" },
                    new BsonDocument { ["name"] = "Doe" },
                },
                BsonAutoId.Int32);

                Console.WriteLine("Inserted: " + count);
            }*/

            using (var stream = new FileStream(DATA_PATH, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 8192, FileOptions.Asynchronous))
            using (var db = new LiteEngine(stream))
            {
                await db.OpenAsync();

                var q = new Query();
                //q.Where.Add("_id = 1");

                var dados = await db.QueryAsync("col1", q);

                await foreach(var doc in dados.ToAsyncEnumerable())
                {
                    Console.WriteLine(doc.ToString());
                }

            }




        }
    }
}
