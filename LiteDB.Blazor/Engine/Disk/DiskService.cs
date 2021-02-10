using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static LiteDB.Constants;

namespace LiteDB.Engine
{
    /// <summary>
    /// Implement custom fast/in memory mapped disk access
    /// [ThreadSafe]
    /// </summary>
    internal class DiskService : IDisposable
    {
        private readonly MemoryCache _cache;

        private readonly Stream _stream;
        private readonly Collation _collation;
        private HeaderPage _header;

        private long _logStartPosition;
        private long _logEndPosition;

        /// <summary>
        /// Get memory cache instance
        /// </summary>
        public MemoryCache Cache => _cache;

        /// <summary>
        /// Get header page single database instance
        /// </summary>
        public HeaderPage Header => _header;

        /// <summary>
        /// Get log length
        /// </summary>
        public long LogLength => _logEndPosition - _logStartPosition;

        /// <summary>
        /// Get log start position in disk
        /// </summary>
        public long LogStartPosition => _logStartPosition;

        /// <summary>
        /// Get/Set log end position in disk
        /// </summary>
        public long LogEndPosition { get => _logEndPosition; set => _logEndPosition = value; }

        #region Async Constructor

        /// <summary>
        /// Async constructor
        /// </summary>
        public async static Task<DiskService> CreateAsync(Stream stream, Collation collation, int[] memorySegmentSizes, int maxExtends)
        {
            var disk = new DiskService(stream, collation, memorySegmentSizes, maxExtends);
            await disk.InitializeAsync();
            return disk;
        }

        private DiskService(Stream stream, Collation collation, int[] memorySegmentSizes, int maxExtends)
        {
            _stream = stream;
            _collation = collation;

            _cache = new MemoryCache(stream, memorySegmentSizes, maxExtends);
        }

        private async Task InitializeAsync()
        { 
            // checks if is a new file
            var isNew = _stream.Length == 0;

            // create new database if not exist yet
            if (isNew)
            {
                LOG($"creating new database", "DISK");

                _header = await CreateDatabase(_stream, _collation);
            }
            else
            {
                // load header page from position 0 from file
                var buffer = new PageBuffer(new byte[PAGE_SIZE], 0, 0) { Position = 0 };

                _stream.Position = 0;

                await _stream.ReadAsync(buffer.Array, 0, PAGE_SIZE);

                _header = new HeaderPage(buffer);
            }

            // define start/end position for log content
            _logStartPosition = (_header.LastPageID + 1) * PAGE_SIZE;
            _logEndPosition = _logStartPosition; // will be updated/fixed by RestoreIndex
        }

        #endregion

        /// <summary>
        /// Create a new empty database (use synced mode)
        /// </summary>
        private static async Task<HeaderPage> CreateDatabase(Stream stream, Collation collation)
        {
            var buffer = new PageBuffer(new byte[PAGE_SIZE], 0, 0) { Position = 0 };
            var header = new HeaderPage(buffer, 0);

            // update last page ID (when initialSize > 0)
            header.LastPageID = 0;
            header.FreeEmptyPageList = uint.MaxValue;

            // update collation
            header.Pragmas.Set(Pragmas.COLLATION, (collation ?? Collation.Default).ToString(), false);

            // update buffer
            header.UpdateBuffer();

            // update position
            stream.Position = 0;

            // write async header page
            await stream.WriteAsync(buffer.Array.AsMemory(buffer.Offset, PAGE_SIZE));

            await stream.FlushAsync();

            return header;
        }

        public async Task<PageBuffer> ReadPage(long position, bool writable)
        {
            ENSURE(position % PAGE_SIZE == 0, "invalid page position");

            var page = writable ?
                await _cache.GetWritablePage(position) :
                await _cache.GetReadablePage(position);

            return page;
        }

        /// <summary>
        /// Request for a empty, writable non-linked page (same as DiskService.NewPage)
        /// </summary>
        public PageBuffer NewPage()
        {
            return _cache.NewPage();
        }

        /// <summary>
        /// Write log pages inside stream
        /// </summary>
        public async Task<int> WriteLogPages(IEnumerable<PageBuffer> pages)
        {
            var count = 0;

            foreach (var page in pages)
            {
                var dataPosition = BasePage.GetPagePosition(page.ReadInt32(BasePage.P_PAGE_ID));

                do
                {
                    // adding this page into file AS new page (at end of file)
                    // must add into cache to be sure that new readers can see this page
                    page.Position = (Interlocked.Add(ref _logEndPosition, PAGE_SIZE)) - PAGE_SIZE;
                }
                while (dataPosition > page.Position);

                // mark this page as readable and get cached paged to enqueue
                var readable = _cache.MoveToReadable(page);

                _stream.Position = page.Position;

                await _stream.WriteAsync(page.Array, page.Offset, PAGE_SIZE);

                count++;
            }

            return count;
        }

        /// <summary>
        /// Read all log from current log position to end of file. 
        /// This operation are sync and should not be run with any page on queue
        /// Use fullLogArea to read file to end
        /// </summary>
        public async IAsyncEnumerable<PageBuffer> ReadLog(bool fullLogArea)
        {
            // do not use MemoryCache factory - reuse same buffer array (one page per time)
            var buffer = new byte[PAGE_SIZE];

            // get file length
            var endPosition = fullLogArea ? _stream.Length : _logEndPosition;
            var position = _logStartPosition;

            while (position < endPosition)
            {
                _stream.Position = position;

                await _stream.ReadAsync(buffer, 0, PAGE_SIZE);

                yield return new PageBuffer(buffer, 0, 0)
                {
                    Position = position
                };

                position += PAGE_SIZE;
            }
        }

        /// <summary>
        /// Read all pages inside datafile - do not consider in-cache only pages. Returns both Data and Log pages
        /// </summary>
        public async IAsyncEnumerable<PageBuffer> ReadFull()
        {
            var buffer = new byte[PAGE_SIZE];

            _stream.Position = 0;

            while (_stream.Position < _stream.Length)
            {
                var position = _stream.Position;

                await _stream.ReadAsync(buffer, 0, PAGE_SIZE);

                yield return new PageBuffer(buffer, 0, 0)
                {
                    Position = position
                };
            }
        }

        /// <summary>
        /// Write pages direct in disk data area. Used in CHECKPOINT only
        /// </summary>
        public async Task WriteDataPages(IAsyncEnumerable<PageBuffer> pages)
        {
            await foreach (var page in pages)
            {
                _stream.Position = page.Position;

                await _stream.WriteAsync(page.Array, page.Offset, PAGE_SIZE);
            }

            await _stream.FlushAsync();
        }

        /// <summary>
        /// Reset log position at end of file (based on header.LastPageID) and crop file if require
        /// </summary>
        public void ResetLogPosition(bool crop)
        {
            _logStartPosition = _logEndPosition = (_header.LastPageID + 1) * PAGE_SIZE;

            if (crop)
            {
                _stream.SetLength(_logStartPosition);
            }
        }

        public void Dispose()
        {
            // other disposes
            _cache?.Dispose();
        }
    }
}
