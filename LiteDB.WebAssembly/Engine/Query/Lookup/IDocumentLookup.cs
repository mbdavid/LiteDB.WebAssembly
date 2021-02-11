using System;
using System.Threading.Tasks;

namespace LiteDB.Engine
{
    /// <summary>
    /// Interface for abstract document lookup that can be direct from datafile or by virtual collections
    /// </summary>
    internal interface IDocumentLookup
    {
        Task<BsonDocument> Load(IndexNode node);
        Task<BsonDocument> Load(PageAddress rawId);
    }
}