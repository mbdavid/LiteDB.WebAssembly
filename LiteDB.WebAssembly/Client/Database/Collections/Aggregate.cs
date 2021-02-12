using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

using static LiteDB.Constants;

namespace LiteDB
{
    public partial class LiteCollection<T>
    {
        #region Count

        /// <summary>
        /// Get document count in collection
        /// </summary>
        public Task<int> CountAsync()
        {
            // do not use indexes - collections has DocumentCount property
            return this.Query().CountAsync();
        }

        /// <summary>
        /// Get document count in collection using predicate filter expression
        /// </summary>
        public Task<int> CountAsync(BsonExpression predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            return this.Query().Where(predicate).CountAsync();
        }

        /// <summary>
        /// Get document count in collection using predicate filter expression
        /// </summary>
        public Task<int> CountAsync(string predicate, BsonDocument parameters) => this.CountAsync(BsonExpression.Create(predicate, parameters));

        /// <summary>
        /// Get document count in collection using predicate filter expression
        /// </summary>
        public Task<int> CountAsync(string predicate, params BsonValue[] args) => this.CountAsync(BsonExpression.Create(predicate, args));

        /// <summary>
        /// Count documents matching a query. This method does not deserialize any documents. Needs indexes on query expression
        /// </summary>
        public Task<int> CountAsync(Expression<Func<T, bool>> predicate) => this.CountAsync(_mapper.GetExpression(predicate));

        /// <summary>
        /// Get document count in collection using predicate filter expression
        /// </summary>
        public Task<int> CountAsync(Query query) => new LiteQueryable<T>(_engine, _mapper, _collection, query).CountAsync();

        #endregion

        #region LongCount

        /// <summary>
        /// Get document count in collection
        /// </summary>
        public Task<long> LongCountAsync()
        {
            return this.Query().LongCountAsync();
        }

        /// <summary>
        /// Get document count in collection using predicate filter expression
        /// </summary>
        public Task<long> LongCountAsync(BsonExpression predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            return this.Query().Where(predicate).LongCountAsync();
        }

        /// <summary>
        /// Get document count in collection using predicate filter expression
        /// </summary>
        public Task<long> LongCountAsync(string predicate, BsonDocument parameters) => this.LongCountAsync(BsonExpression.Create(predicate, parameters));

        /// <summary>
        /// Get document count in collection using predicate filter expression
        /// </summary>
        public Task<long> LongCountAsync(string predicate, params BsonValue[] args) => this.LongCountAsync(BsonExpression.Create(predicate, args));

        /// <summary>
        /// Get document count in collection using predicate filter expression
        /// </summary>
        public Task<long> LongCountAsync(Expression<Func<T, bool>> predicate) => this.LongCountAsync(_mapper.GetExpression(predicate));

        /// <summary>
        /// Get document count in collection using predicate filter expression
        /// </summary>
        public Task<long> LongCountAsync(Query query) => new LiteQueryable<T>(_engine, _mapper, _collection, query).LongCountAsync();

        #endregion

        #region Exists

        /// <summary>
        /// Get true if collection contains at least 1 document that satisfies the predicate expression
        /// </summary>
        public Task<bool> ExistsAsync(BsonExpression predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            return this.Query().Where(predicate).ExistsAsync();
        }

        /// <summary>
        /// Get true if collection contains at least 1 document that satisfies the predicate expression
        /// </summary>
        public Task<bool> ExistsAsync(string predicate, BsonDocument parameters) => this.ExistsAsync(BsonExpression.Create(predicate, parameters));

        /// <summary>
        /// Get true if collection contains at least 1 document that satisfies the predicate expression
        /// </summary>
        public Task<bool> ExistsAsync(string predicate, params BsonValue[] args) => this.ExistsAsync(BsonExpression.Create(predicate, args));

        /// <summary>
        /// Get true if collection contains at least 1 document that satisfies the predicate expression
        /// </summary>
        public Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate) => this.ExistsAsync(_mapper.GetExpression(predicate));

        /// <summary>
        /// Get true if collection contains at least 1 document that satisfies the predicate expression
        /// </summary>
        public Task<bool> ExistsAsync(Query query) => new LiteQueryable<T>(_engine, _mapper, _collection, query).ExistsAsync();

        #endregion

        #region Min/Max

        /// <summary>
        /// Returns the min value from specified key value in collection
        /// </summary>
        public async Task<BsonValue> MinAsync(BsonExpression keySelector)
        {
            if (string.IsNullOrEmpty(keySelector)) throw new ArgumentNullException(nameof(keySelector));

            var docs = this.Query()
                .OrderBy(keySelector)
                .Select(keySelector)
                .ToDocumentsAsync();

            var doc = await docs.FirstAsync();

            // return first field of first document
            return doc[doc.Keys.First()];
        }

        /// <summary>
        /// Returns the min value of _id index
        /// </summary>
        public Task<BsonValue> MinAsync() => this.MinAsync("_id");

        /// <summary>
        /// Returns the min value from specified key value in collection
        /// </summary>
        public async Task<K> MinAsync<K>(Expression<Func<T, K>> keySelector)
        {
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            var expr = _mapper.GetExpression(keySelector);

            var value = await this.MinAsync(expr);

            return (K)_mapper.Deserialize(typeof(K), value);
        }

        /// <summary>
        /// Returns the max value from specified key value in collection
        /// </summary>
        public async Task<BsonValue> MaxAsync(BsonExpression keySelector)
        {
            if (string.IsNullOrEmpty(keySelector)) throw new ArgumentNullException(nameof(keySelector));

            var docs = this.Query()
                .OrderByDescending(keySelector)
                .Select(keySelector)
                .ToDocumentsAsync();

            var doc = await docs.FirstAsync();

            // return first field of first document
            return doc[doc.Keys.First()];
        }

        /// <summary>
        /// Returns the max _id index key value
        /// </summary>
        public Task<BsonValue> MaxAsync() => this.MaxAsync("_id");

        /// <summary>
        /// Returns the last/max field using a linq expression
        /// </summary>
        public async Task<K> MaxAsync<K>(Expression<Func<T, K>> keySelector)
        {
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            var expr = _mapper.GetExpression(keySelector);

            var value = await this.MaxAsync(expr);

            return (K)_mapper.Deserialize(typeof(K), value);
        }

        #endregion
    }
}