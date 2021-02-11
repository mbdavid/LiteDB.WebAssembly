using System;
using System.Collections.Generic;
using System.Linq;
using static LiteDB.Constants;

namespace LiteDB.Engine
{
    /// <summary>
    /// Basic query pipe workflow - support filter, includes and orderby
    /// </summary>
    internal class QueryPipe : BasePipe
    {
        public QueryPipe(TransactionService transaction, IDocumentLookup loader, EnginePragmas pragmas)
            : base(transaction, loader, pragmas)
        {
        }

        /// <summary>
        /// Query Pipe order
        /// - LoadDocument
        /// - IncludeBefore
        /// - Filter
        /// - OrderBy
        /// - OffSet
        /// - Limit
        /// - IncludeAfter
        /// - Select
        /// </summary>
        public override async IAsyncEnumerable<BsonDocument> Pipe(IAsyncEnumerable<IndexNode> nodes, QueryPlan query)
        {
            // starts pipe loading document
            var source = this.LoadDocument(nodes);

            // do includes in result before filter
            foreach (var path in query.IncludeBefore)
            {
                source = this.Include(source, path);
            }

            // filter results according expressions
            foreach (var expr in query.Filters)
            {
                source = this.Filter(source, expr);
            }

            if (query.OrderBy != null)
            {
                // pipe: orderby with offset+limit
                source = this.OrderBy(source, query.OrderBy.Expression, query.OrderBy.Order, query.Offset, query.Limit);
            }
            else
            {
                // pipe: apply offset (no orderby)
                if (query.Offset > 0) source = source.SkipAsync(query.Offset);

                // pipe: apply limit (no orderby)
                if (query.Limit < int.MaxValue) source = source.TakeAsync(query.Limit);
            }

            // do includes in result after filter
            foreach (var path in query.IncludeAfter)
            {
                source = this.Include(source, path);
            }

            var result = query.Select.All ?
                this.SelectAll(source, query.Select.Expression) :
                this.Select(source, query.Select.Expression);

            await foreach(var doc in result)
            {
                yield return doc;
            }
        }

        /// <summary>
        /// Pipe: Transaform final result appling expressin transform. Can return document or simple values
        /// </summary>
        private async IAsyncEnumerable<BsonDocument> Select(IAsyncEnumerable<BsonDocument> source, BsonExpression select)
        {
            var defaultName = select.DefaultFieldName();

            await foreach (var doc in source)
            {
                var value = select.ExecuteScalar(doc, _pragmas.Collation);

                if (value.IsDocument)
                {
                    yield return value.AsDocument;
                }
                else
                {
                    yield return new BsonDocument { [defaultName] = value };
                }
            }
        }

        /// <summary>
        /// Pipe: Run select expression over all recordset
        /// </summary>
        private IAsyncEnumerable<BsonDocument> SelectAll(IAsyncEnumerable<BsonDocument> source, BsonExpression select)
        {
            throw new NotImplementedException();
            /*
            var cached = new DocumentCacheEnumerable(source, _lookup);

            var defaultName = select.DefaultFieldName();
            var result = select.Execute(cached, _pragmas.Collation);

            foreach (var value in result)
            {
                if (value.IsDocument)
                {
                    yield return value.AsDocument;
                }
                else
                {
                    yield return new BsonDocument { [defaultName] = value };
                }
            }
            */
        }
    }
}