using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using static LiteDB.Constants;

namespace LiteDB.Engine
{
    internal class DataService
    {
        /// <summary>
        /// Get maximum data bytes[] that fit in 1 page = 8150
        /// </summary>
        public const int MAX_DATA_BYTES_PER_PAGE =
            PAGE_SIZE - // 8192
            PAGE_HEADER_SIZE - // [32 bytes]
            BasePage.SLOT_SIZE - // [4 bytes]
            DataBlock.DATA_BLOCK_FIXED_SIZE; // [6 bytes];

        private Snapshot _snapshot;

        public DataService(Snapshot snapshot)
        {
            _snapshot = snapshot;
        }

        /// <summary>
        /// Insert BsonDocument into new data pages
        /// </summary>
        public async Task<PageAddress> Insert(BsonDocument doc)
        {
            var bytesLeft = doc.GetBytesCount(true);

            if (bytesLeft > MAX_DOCUMENT_SIZE) throw new LiteException(0, "Document size exceed {0} limit", MAX_DOCUMENT_SIZE);

            var firstBlock = PageAddress.Empty;

            async IAsyncEnumerable<BufferSlice> source()
            {
                var blockIndex = 0;
                DataBlock lastBlock = null;

                while (bytesLeft > 0)
                {
                    var bytesToCopy = Math.Min(bytesLeft, MAX_DATA_BYTES_PER_PAGE);
                    var dataPage = await _snapshot.GetFreeDataPage(bytesToCopy + DataBlock.DATA_BLOCK_FIXED_SIZE);
                    var dataBlock = dataPage.InsertBlock(bytesToCopy, blockIndex++ > 0);

                    if (lastBlock != null)
                    {
                        lastBlock.SetNextBlock(dataBlock.Position);
                    }

                    if (firstBlock.IsEmpty) firstBlock = dataBlock.Position;

                    await _snapshot.AddOrRemoveFreeDataList(dataPage);

                    yield return dataBlock.Buffer;

                    lastBlock = dataBlock;

                    bytesLeft -= bytesToCopy;
                }
            }

            // consume all source bytes to write BsonDocument direct into PageBuffer
            // must be fastest as possible
            await using (var writer = await BufferWriterAsync.CreateAsync(source()))
            {
                // already bytes count calculate at method start
                await writer.WriteDocument(doc, false);
                await writer.Consume();
            }

            return firstBlock;
        }

        /// <summary>
        /// Update document using same page position as reference
        /// </summary>
        public async Task Update(CollectionPage col, PageAddress blockAddress, BsonDocument doc)
        {
            var bytesLeft = doc.GetBytesCount(true);

            if (bytesLeft > MAX_DOCUMENT_SIZE) throw new LiteException(0, "Document size exceed {0} limit", MAX_DOCUMENT_SIZE);

            DataBlock lastBlock = null;
            var updateAddress = blockAddress;

            async IAsyncEnumerable <BufferSlice> source()
            {
                var bytesToCopy = 0;

                while (bytesLeft > 0)
                {
                    // if last block contains new block sequence, continue updating
                    if (updateAddress.IsEmpty == false)
                    {
                        var dataPage = await _snapshot.GetPage<DataPage>(updateAddress.PageID);
                        var currentBlock = dataPage.GetBlock(updateAddress.Index);

                        // try get full page size content (do not add DATA_BLOCK_FIXED_SIZE because will be added in UpdateBlock)
                        bytesToCopy = Math.Min(bytesLeft, dataPage.FreeBytes + currentBlock.Buffer.Count);

                        var updateBlock = dataPage.UpdateBlock(currentBlock, bytesToCopy);

                        await _snapshot.AddOrRemoveFreeDataList(dataPage);

                        yield return updateBlock.Buffer;

                        lastBlock = updateBlock;

                        // go to next address (if exists)
                        updateAddress = updateBlock.NextBlock;
                    }
                    else
                    {
                        bytesToCopy = Math.Min(bytesLeft, MAX_DATA_BYTES_PER_PAGE);
                        var dataPage = await _snapshot.GetFreeDataPage(bytesToCopy + DataBlock.DATA_BLOCK_FIXED_SIZE);
                        var insertBlock = dataPage.InsertBlock(bytesToCopy, true);

                        if (lastBlock != null)
                        {
                            lastBlock.SetNextBlock(insertBlock.Position);
                        }

                        await _snapshot.AddOrRemoveFreeDataList(dataPage);

                        yield return insertBlock.Buffer;

                        lastBlock = insertBlock;
                    }

                    bytesLeft -= bytesToCopy;
                }

                // old document was bigger than current, must delete extend blocks
                if (lastBlock.NextBlock.IsEmpty == false)
                {
                    var nextBlockAddress = lastBlock.NextBlock;

                    lastBlock.SetNextBlock(PageAddress.Empty);

                    await this.Delete(nextBlockAddress);
                }
            }

            // consume all source bytes to write BsonDocument direct into PageBuffer
            await using (var writer = await BufferWriterAsync.CreateAsync(source()))
            {
                // already bytes count calculate at method start
                await writer.WriteDocument(doc, false);
                await writer.Consume();
            }
        }

        /// <summary>
        /// Get all buffer slices that address block contains. Need use BufferReader to read document
        /// </summary>
        public async IAsyncEnumerable<BufferSlice> Read(PageAddress address)
        {
            while (address != PageAddress.Empty)
            {
                var dataPage = await _snapshot.GetPage<DataPage>(address.PageID);

                var block = dataPage.GetBlock(address.Index);

                yield return block.Buffer;

                address = block.NextBlock;
            }
        }

        /// <summary>
        /// Delete all datablock that contains a document (can use multiples data blocks)
        /// </summary>
        public async Task Delete(PageAddress blockAddress)
        {
            // delete all document blocks
            while(blockAddress != PageAddress.Empty)
            {
                var page = await _snapshot.GetPage<DataPage>(blockAddress.PageID);
                var block = page.GetBlock(blockAddress.Index);

                // delete block inside page
                page.DeleteBlock(blockAddress.Index);

                // fix page empty list (or delete page)
                await _snapshot.AddOrRemoveFreeDataList(page);

                blockAddress = block.NextBlock;
            }
        }
    }
}