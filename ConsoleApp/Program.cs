using LiteDB;
using LiteDB.Engine;

using System;
using System.IO;
using System.Linq;

namespace ConsoleApp
{
    class Program
    {
        private const string DATA_PATH = @"C:\Git\Data\_litedb-async.db";

        static void Main(string[] args)
        {
            MainAsync(args);
        }

        static async void MainAsync(string[] args)
        {
            File.Delete(DATA_PATH);

            using (var stream = new FileStream(DATA_PATH, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 8192, FileOptions.Asynchronous))
            {
                using (var db = new LiteEngine(stream))
                {
                    try
                    {
                        await db.OpenAsync();

                        //await db.InsertAsync("col1", new[] { new BsonDocument { ["name"] = "John" } }, BsonAutoId.Int32);

                        //await stream.FlushAsync();
                        throw new Exception("error aqui");

                    }
                    catch (Exception ex)
                    {
                        ;
                    }
                }

                ;
            }

            /*
            var query = new Query();
            query.Where.Add("_id = 1");

            var docs = await db.QueryAsync("col1", query);

            await foreach(var doc in docs.ToAsyncEnumerable())
            {
                Console.WriteLine(JsonSerializer.Serialize(doc));
            }*/


            Console.WriteLine("End");
            Console.ReadKey();

        }
    }
}
