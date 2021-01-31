using LiteDB;
using LiteDB.Engine;

using System;
using System.Linq;

namespace ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            MainAsync(args);
        }

        static async void MainAsync(string[] args)
        {
            using var db = new LiteEngine();

            await db.OpenAsync();

            db.Insert("col1", new[] { new BsonDocument { ["name"] = "John" } }, BsonAutoId.Int32);

            var query = new Query();
            query.Where.Add("_id = 1");

            var doc = db.Query("col1", query).ToArray().First();

            Console.WriteLine(JsonSerializer.Serialize(doc));

            Console.ReadKey();

        }
    }
}
