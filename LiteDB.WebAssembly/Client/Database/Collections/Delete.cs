using System;
using System.Linq.Expressions;
using System.Threading.Tasks;

using static LiteDB.Constants;

namespace LiteDB
{
    public partial class LiteCollection<T>
    {
        /// <summary>
        /// Delete a single document on collection based on _id index. Returns true if document was deleted
        /// </summary>
        public async Task<bool> DeleteAsync(BsonValue id)
        {
            if (id == null || id.IsNull) throw new ArgumentNullException(nameof(id));

            return (await _engine.DeleteAsync(_collection, new [] { id })) == 1;
        }

        /// <summary>
        /// Delete all documents inside collection. Returns how many documents was deleted. Run inside current transaction
        /// </summary>
        public Task<int> DeleteAllAsync()
        {
            return _engine.DeleteManyAsync(_collection, null);
        }

        /// <summary>
        /// Delete all documents based on predicate expression. Returns how many documents was deleted
        /// </summary>
        public Task<int> DeleteManyAsync(BsonExpression predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            return _engine.DeleteManyAsync(_collection, predicate);
        }

        /// <summary>
        /// Delete all documents based on predicate expression. Returns how many documents was deleted
        /// </summary>
        public Task<int> DeleteManyAsync(string predicate, BsonDocument parameters) => this.DeleteManyAsync(BsonExpression.Create(predicate, parameters));

        /// <summary>
        /// Delete all documents based on predicate expression. Returns how many documents was deleted
        /// </summary>
        public Task<int> DeleteManyAsync(string predicate, params BsonValue[] args) => this.DeleteManyAsync(BsonExpression.Create(predicate, args));

        /// <summary>
        /// Delete all documents based on predicate expression. Returns how many documents was deleted
        /// </summary>
        public Task<int> DeleteManyAsync(Expression<Func<T, bool>> predicate) => this.DeleteManyAsync(_mapper.GetExpression(predicate));
    }
}