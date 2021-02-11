using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LiteDB.Engine
{
    public interface ILiteEngine : IAsyncDisposable
    {
        Task OpenAsync();
        bool IsOpen { get; }

        Task<int> CheckpointAsync();

        Task<bool> BeginTransAsync();
        Task<bool> CommitAsync();
        Task<bool> RollbackAsync();

        Task<IBsonDataReader> QueryAsync(string collection, Query query);

        Task<int> InsertAsync(string collection, IEnumerable<BsonDocument> docs, BsonAutoId autoId);

        Task<int> UpdateAsync(string collection, IEnumerable<BsonDocument> docs);
        Task<int> UpdateManyAsync(string collection, BsonExpression transform, BsonExpression predicate);

        Task<int> UpsertAsync(string collection, IEnumerable<BsonDocument> docs, BsonAutoId autoId);

        Task<int> DeleteAsync(string collection, IEnumerable<BsonValue> ids);
        Task<int> DeleteManyAsync(string collection, BsonExpression predicate);

        Task<bool> DropCollectionAsync(string name);
        Task<bool> RenameCollectionAsync(string name, string newName);

        Task<bool> EnsureIndexAsync(string collection, string name, BsonExpression expression, bool unique);
        Task<bool> DropIndexAsync(string collection, string name);

        BsonValue Pragma(string name);
        Task<bool> PragmaAsync(string name, BsonValue value);
    }
}