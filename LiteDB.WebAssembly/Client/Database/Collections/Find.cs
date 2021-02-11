using LiteDB.Engine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

using static LiteDB.Constants;

namespace LiteDB
{
    public partial class LiteCollection<T>
    {
        /// <summary>
        /// Return a new LiteQueryable to build more complex queries
        /// </summary>
        public ILiteQueryable<T> Query()
        {
            return new LiteQueryable<T>(_engine, _mapper, _collection, new Query()).Include(_includes);
        }

        #region Find

        /// <summary>
        /// Find documents inside a collection using predicate expression.
        /// </summary>
        public IAsyncEnumerable<T> FindAsync(BsonExpression predicate, int skip = 0, int limit = int.MaxValue)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            return this.Query()
                .Include(_includes)
                .Where(predicate)
                .Skip(skip)
                .Limit(limit)
                .ToAsyncEnumerable();
        }

        /// <summary>
        /// Find documents inside a collection using query definition.
        /// </summary>
        public IAsyncEnumerable<T> FindAsync(Query query, int skip = 0, int limit = int.MaxValue)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));

            if (skip != 0) query.Offset = skip;
            if (limit != int.MaxValue) query.Limit = limit;

            return new LiteQueryable<T>(_engine, _mapper, _collection, query)
                .ToAsyncEnumerable();
        }

        /// <summary>
        /// Find documents inside a collection using predicate expression.
        /// </summary>
        public IAsyncEnumerable<T> FindAsync(Expression<Func<T, bool>> predicate, int skip = 0, int limit = int.MaxValue) => this.FindAsync(_mapper.GetExpression(predicate), skip, limit);

        #endregion

        #region FindById + One + All

        /// <summary>
        /// Find a document using Document Id. Returns null if not found.
        /// </summary>
        public Task<T> FindByIdAsync(BsonValue id)
        {
            if (id == null || id.IsNull) throw new ArgumentNullException(nameof(id));

            return this.FindAsync(BsonExpression.Create("_id = @0", id)).FirstOrDefaultAsync();
        }

        /// <summary>
        /// Find the first document using predicate expression. Returns null if not found
        /// </summary>
        public Task<T> FindOneAsync(BsonExpression predicate) => this.FindAsync(predicate).FirstOrDefaultAsync();

        /// <summary>
        /// Find the first document using predicate expression. Returns null if not found
        /// </summary>
        public Task<T> FindOneAsync(string predicate, BsonDocument parameters) => this.FindOneAsync(BsonExpression.Create(predicate, parameters));

        /// <summary>
        /// Find the first document using predicate expression. Returns null if not found
        /// </summary>
        public Task<T> FindOneAsync(BsonExpression predicate, params BsonValue[] args) => this.FindOneAsync(BsonExpression.Create(predicate, args));

        /// <summary>
        /// Find the first document using predicate expression. Returns null if not found
        /// </summary>
        public Task<T> FindOneAsync(Expression<Func<T, bool>> predicate) => this.FindOneAsync(_mapper.GetExpression(predicate));

        /// <summary>
        /// Find the first document using defined query structure. Returns null if not found
        /// </summary>
        public Task<T> FindOneAsync(Query query) => this.FindAsync(query).FirstOrDefaultAsync();

        /// <summary>
        /// Returns all documents inside collection order by _id index.
        /// </summary>
        public IAsyncEnumerable<T> FindAllAsync() => this.Query().Include(_includes).ToAsyncEnumerable();

        #endregion
    }
}