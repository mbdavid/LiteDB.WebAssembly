using LiteDB.Engine;

using Microsoft.JSInterop;

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace LiteDB.Blazor
{
    public class LiteDatabaseFactory : ILiteDatabaseFactory
    {
        private readonly IJSRuntime _runtime;

        public LiteDatabaseFactory(IJSRuntime runtime)
        {
            _runtime = runtime;
        }

        public async Task<ILiteDatabase> GetDatabaseAsync(BsonMapper mapper = null)
        {
            var length = await _runtime.InvokeAsync<long>("localStorageDb.getLength");

            var settings = new EngineSettings
            {
                DataStream = new LocalStorageStream(_runtime, length)
            };

            var engine = new LiteEngine(settings);

            var db = new LiteDatabase(engine, mapper);

            return db;
        }
    }
}
