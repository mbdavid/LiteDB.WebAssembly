using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using static LiteDB.Constants;

namespace LiteDB.Engine
{
    public partial class LiteEngine
    {
        /// <summary>
        /// Implements delete based on IDs enumerable
        /// </summary>
        public async Task<int> DeleteAsync(string collection, IEnumerable<BsonValue> ids)
        {
            if (collection.IsNullOrWhiteSpace()) throw new ArgumentNullException(nameof(collection));
            if (ids == null) throw new ArgumentNullException(nameof(ids));

            return await this.AutoTransaction(async transaction =>
            {
                var snapshot = await transaction.CreateSnapshot(LockMode.Write, collection, false);
                var collectionPage = snapshot.CollectionPage;
                var data = new DataService(snapshot);
                var indexer = new IndexService(snapshot, _header.Pragmas.Collation);

                if (collectionPage == null) return 0;

                LOG($"delete `{collection}`", "COMMAND");

                var count = 0;
                var pk = collectionPage.PK;

                foreach (var id in ids)
                {
                    var pkNode = await indexer.Find(pk, id, false, LiteDB.Query.Ascending);

                    // if pk not found, continue
                    if (pkNode == null) continue;

                    // remove object data
                    await data.Delete(pkNode.DataBlock);

                    // delete all nodes (start in pk node)
                    await indexer.DeleteAll(pkNode.Position);

                    await transaction.Safepoint();

                    count++;
                }

                return count;
            });
        }

        /// <summary>
        /// Implements delete based on filter expression
        /// </summary>
        public async Task<int> DeleteManyAsync(string collection, BsonExpression predicate)
        {
            if (collection.IsNullOrWhiteSpace()) throw new ArgumentNullException(nameof(collection));

            // do optimization for when using "_id = value" key
            if (predicate != null &&
                predicate.Type == BsonExpressionType.Equal && 
                predicate.Left.Type == BsonExpressionType.Path && 
                predicate.Left.Source == "$._id" && 
                predicate.Right.IsValue)
            {
                var id = predicate.Right.Execute(_header.Pragmas.Collation).First();

                return await this.DeleteAsync(collection, new BsonValue[] { id });
            }
            else
            {
                async IAsyncEnumerable<BsonValue> getIds()
                {
                    // this is intresting: if _id returns an document (like in FileStorage) you can't run direct _id
                    // field because "reader.Current" will return _id document - but not - { _id: [document] }
                    // create inner document to ensure _id will be a document
                    var query = new Query { Select = "{ i: _id }", ForUpdate = true };

                    if(predicate != null)
                    {
                        query.Where.Add(predicate);
                    }

                    await using (var reader = await this.QueryAsync(collection, query))
                    {
                        while (await reader.ReadAsync())
                        {
                            var value = reader.Current["i"];

                            if (value != BsonValue.Null)
                            {
                                yield return value;
                            }
                        }
                    }
                }

                // in next version fix this ToListAsync() to AsyncEnumerable
                return await this.DeleteAsync(collection, await getIds().ToListAsync());
            }
        }
    }
}