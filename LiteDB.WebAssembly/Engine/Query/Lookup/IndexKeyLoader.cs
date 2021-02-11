using System.Collections.Generic;
using System.Threading.Tasks;

using static LiteDB.Constants;

namespace LiteDB.Engine
{
    /// <summary>
    /// Implement lookup based only in index Key
    /// </summary>
    internal class IndexLookup : IDocumentLookup
    {
        private readonly IndexService _indexer;
        private readonly string _name;

        public IndexLookup(IndexService indexer, string name)
        {
            _indexer = indexer;
            _name = name;
        }

        public Task<BsonDocument> Load(IndexNode node)
        {
            ENSURE(node.DataBlock.IsEmpty == false, "Never should be empty rawid");

            var doc = new BsonDocument
            {
                [_name] = node.Key,
            };

            doc.RawId = node.DataBlock;

            return Task.FromResult(doc);
        }

        public async Task<BsonDocument> Load(PageAddress rawId)
        {
            var node = await _indexer.GetNode(rawId);

            return await this.Load(node);
        }
    }
}