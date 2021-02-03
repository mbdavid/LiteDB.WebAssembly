using System;
using System.Threading.Tasks;

namespace LiteDB
{
    public interface IBsonDataReader : IAsyncDisposable
    {
        BsonValue this[string field] { get; }

        string Collection { get; }
        BsonValue Current { get; }
        bool HasValues { get; }

        Task<bool> ReadAsync();
    }
}