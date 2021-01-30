using Microsoft.JSInterop;

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace LiteDB.Blazor
{
    public interface ILiteDatabaseFactory
    {
        Task<ILiteDatabase> GetDatabaseAsync(BsonMapper mapper = null);
    }
}
