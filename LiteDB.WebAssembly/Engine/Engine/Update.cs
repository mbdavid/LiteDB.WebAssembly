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
        /// Implement update command to a document inside a collection. Return number of documents updated
        /// </summary>
        public async Task<int> UpdateAsync(string collection, IEnumerable<BsonDocument> docs)
        {
            if (collection.IsNullOrWhiteSpace()) throw new ArgumentNullException(nameof(collection));
            if (docs == null) throw new ArgumentNullException(nameof(docs));

            return await this.AutoTransaction(async transaction =>
            {
                var snapshot = await transaction.CreateSnapshot(LockMode.Write, collection, false);
                var collectionPage = snapshot.CollectionPage;
                var indexer = new IndexService(snapshot, _header.Pragmas.Collation);
                var data = new DataService(snapshot);
                var count = 0;

                if (collectionPage == null) return 0;

                LOG($"update `{collection}`", "COMMAND");

                foreach (var doc in docs)
                {
                    await transaction.Safepoint();

                    if (await this.UpdateDocument(snapshot, collectionPage, doc, indexer, data))
                    {
                        count++;
                    }
                }

                return count;
            });
        }

        /// <summary>
        /// Update documents using transform expression (must return a scalar/document value) using predicate as filter
        /// </summary>
        public async Task<int> UpdateManyAsync(string collection, BsonExpression transform, BsonExpression predicate)
        {
            if (collection.IsNullOrWhiteSpace()) throw new ArgumentNullException(nameof(collection));
            if (transform == null) throw new ArgumentNullException(nameof(transform));

            // add suport to UpdateAsync(string, IAsyncEnumerable)
            //return await this.UpdateAsync(collection, transformDocs());

            throw new NotSupportedException();

            async IAsyncEnumerable<BsonDocument> transformDocs()
            {
                var q = new Query { Select = "$", ForUpdate = true };

                if (predicate != null)
                {
                    q.Where.Add(predicate);
                }

                await using (var reader = await this.QueryAsync(collection, q))
                {
                    while (await reader.ReadAsync())
                    {
                        var doc = reader.Current.AsDocument;

                        var id = doc["_id"];
                        var value = transform.ExecuteScalar(doc, _header.Pragmas.Collation);

                        if (!value.IsDocument) throw new ArgumentException("Extend expression must return a document", nameof(transform));

                        var result = BsonExpressionMethods.EXTEND(doc, value.AsDocument).AsDocument;

                        // be sure result document will contain same _id as current doc
                        if (result.TryGetValue("_id", out var newId))
                        {
                            if (newId != id) throw LiteException.InvalidUpdateField("_id");
                        }
                        else
                        {
                            result["_id"] = id;
                        }

                        yield return result;
                    }
                }
            }
        }

        /// <summary>
        /// Implement internal update document
        /// </summary>
        private async Task<bool> UpdateDocument(Snapshot snapshot, CollectionPage col, BsonDocument doc, IndexService indexer, DataService data)
        {
            // normalize id before find
            var id = doc["_id"];
            
            // validate id for null, min/max values
            if (id.IsNull || id.IsMinValue || id.IsMaxValue)
            {
                throw LiteException.InvalidDataType("_id", id);
            }
            
            // find indexNode from pk index
            var pkNode = await indexer.Find(col.PK, id, false, LiteDB.Query.Ascending);
            
            // if not found document, no updates
            if (pkNode == null) return false;
            
            // update data storage
            await data.Update(col, pkNode.DataBlock, doc);

            // get all current non-pk index nodes from this data block (slot, key, nodePosition)
            var oldKeys = new List<(byte slot, BsonValue key, PageAddress position)>();

            await foreach(var node in indexer.GetNodeList(pkNode.NextNode))
            {
                oldKeys.Add(new (node.Slot, node.Key, node.Position));
            }                

            // build a list of all new key index keys
            var newKeys = new List<(byte slot, BsonValue key, string name)>();

            foreach (var index in col.GetCollectionIndexes().Where(x => x.Name != "_id"))
            {
                // getting all keys from expression over document
                var keys = index.BsonExpr.GetIndexKeys(doc, _header.Pragmas.Collation);

                foreach (var key in keys)
                {
                    newKeys.Add(new (index.Slot, key, index.Name));
                }
            }

            if (oldKeys.Count == 0 && newKeys.Count == 0) return true;

            // get a list of all nodes that are in oldKeys but not in newKeys (must delete)
            var toDelete = new HashSet<PageAddress>(oldKeys
                .Where(x => newKeys.Any(n => n.Item1 == x.Item1 && n.Item2 == x.Item2) == false)
                .Select(x => x.Item3));

            // get a list of all keys that are not in oldKeys (must insert)
            var toInsert = newKeys
                .Where(x => oldKeys.Any(o => o.Item1 == x.Item1 && o.Item2 == x.Item2) == false)
                .ToArray();

            // if nothing to change, just exit
            if (toDelete.Count == 0 && toInsert.Length == 0) return true;

            // delete nodes and return last keeped node in list
            var last = await indexer.DeleteList(pkNode.Position, toDelete);

            // now, insert all new nodes
            foreach(var elem in toInsert)
            {
                var index = col.GetCollectionIndex(elem.name);

                last = await indexer.AddNode(index, elem.key, pkNode.DataBlock, last);
            }

            return true;
        }
    }
}