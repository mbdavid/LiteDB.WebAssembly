using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static LiteDB.Constants;

namespace LiteDB.Engine
{
    internal class CollectionService
    {
        private readonly HeaderPage _header;
        private readonly Snapshot _snapshot;
        private readonly TransactionPages _transPages;

        public CollectionService(HeaderPage header, Snapshot snapshot, TransactionPages transPages)
        {
            _snapshot = snapshot;
            _header = header;
            _transPages = transPages;
        }

        /// <summary>
        /// Check collection name if is valid (and fit on header)
        /// Throw correct message error if not valid name or not fit on header page
        /// </summary>
        public static void CheckName(string name, HeaderPage header)
        {
            if (Encoding.UTF8.GetByteCount(name) > header.GetAvailableCollectionSpace()) throw LiteException.InvalidCollectionName(name, "There is no space in header this collection name");
            if (!name.IsWord()) throw LiteException.InvalidCollectionName(name, "Use only [a-Z$_]");
            if (name.StartsWith("$")) throw LiteException.InvalidCollectionName(name, "Collection can't starts with `$` (reserved for system collections)");
        }

        /// <summary>
        /// Get collection page instance (or create a new one). Returns null if not found and not created
        /// </summary>
        public async Task<CollectionPage> Get(string name, bool addIfNotExists)
        {
            // get collection pageID from header
            var pageID = _header.GetCollectionPageID(name);

            if (pageID != uint.MaxValue)
            {
                return await _snapshot.GetPage<CollectionPage>(pageID);
            }
            else if (addIfNotExists)
            {
                return await this.Add(name);
            }

            return null;
        }

        /// <summary>
        /// Add a new collection. Check if name the not exists. Create only in transaction page - will update header only in commit
        /// </summary>
        private async Task<CollectionPage> Add(string name)
        {
            // checks for collection name/size
            CheckName(name, _header);

            // create new collection page
            var collectionPage = await _snapshot.NewPage<CollectionPage>();

            _snapshot.CollectionPage = collectionPage;

            var pageID = collectionPage.PageID;

            // insert collection name/pageID in header only in commit operation
            _transPages.Commit += (h) => h.InsertCollection(name, pageID);

            // create first index (_id pk) (must pass collectionPage because snapshot contains null in CollectionPage prop)
            var indexer = new IndexService(_snapshot, _header.Pragmas.Collation);

            await indexer.CreateIndex("_id", "$._id", true);

            return collectionPage;
        }
    }
}