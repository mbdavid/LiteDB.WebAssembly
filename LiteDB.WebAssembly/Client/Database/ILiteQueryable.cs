using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace LiteDB
{
    public interface ILiteQueryable<T> : ILiteQueryableResult<T>
    {
        ILiteQueryable<T> Include(BsonExpression path);
        ILiteQueryable<T> Include(List<BsonExpression> paths);
        ILiteQueryable<T> Include<K>(Expression<Func<T, K>> path);

        ILiteQueryable<T> Where(BsonExpression predicate);
        ILiteQueryable<T> Where(string predicate, BsonDocument parameters);
        ILiteQueryable<T> Where(string predicate, params BsonValue[] args);
        ILiteQueryable<T> Where(Expression<Func<T, bool>> predicate);

        ILiteQueryable<T> OrderBy(BsonExpression keySelector, int order = 1);
        ILiteQueryable<T> OrderBy<K>(Expression<Func<T, K>> keySelector, int order = 1);
        ILiteQueryable<T> OrderByDescending(BsonExpression keySelector);
        ILiteQueryable<T> OrderByDescending<K>(Expression<Func<T, K>> keySelector);

        ILiteQueryable<T> GroupBy(BsonExpression keySelector);
        ILiteQueryable<T> Having(BsonExpression predicate);

        ILiteQueryableResult<BsonDocument> Select(BsonExpression selector);
        ILiteQueryableResult<K> Select<K>(Expression<Func<T, K>> selector);
    }

    public interface ILiteQueryableResult<T>
    {
        ILiteQueryableResult<T> Limit(int limit);
        ILiteQueryableResult<T> Skip(int offset);
        ILiteQueryableResult<T> Offset(int offset);
        ILiteQueryableResult<T> ForUpdate();

        Task<BsonDocument> GetPlanAsync();
        Task<IBsonDataReader> ExecuteReaderAsync();
        IAsyncEnumerable<BsonDocument> ToDocumentsAsync();
        IAsyncEnumerable<T> ToAsyncEnumerable();
        Task<List<T>> ToListAsync();
        Task<T[]> ToArrayAsync();

        Task<T> FirstAsync();
        Task<T> FirstOrDefaultAsync();
        Task<T> SingleAsync();
        Task<T> SingleOrDefaultAsync();

        Task<int> CountAsync();
        Task<long> LongCountAsync();
        Task<bool> ExistsAsync();
    }
}