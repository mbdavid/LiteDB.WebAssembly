using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using static LiteDB.Constants;

namespace LiteDB.Engine
{
    /// <summary>
    /// Manage linear memory segments to avoid re-create array buffer in heap memory
    /// [NO ThreadSafe]
    /// </summary>
    internal class MemoryCache : IDisposable
    {
        /// <summary>
        /// Contains free ready-to-use pages in memory
        /// - All pages here MUST be IsWritable = false
        /// - All pages here MUST be Position = MaxValue
        /// - All bytes must be 0
        /// </summary>
        private readonly Queue<PageBuffer> _free = new Queue<PageBuffer>();

        /// <summary>
        /// Contains only clean pages - support page concurrency use
        /// - MUST:
        /// - Contains only 1 instance per Position
        /// - Contains only pages with IsWritable = false
        /// </summary>
        private readonly Dictionary<long, PageBuffer> _readable = new Dictionary<long, PageBuffer>();

        /// <summary>
        /// Get how many extends was made in this store
        /// </summary>
        private int _extends = 0;

        /// <summary>
        /// Get how many extends cache will extend memory before try reuse old readed pages. 
        /// </summary>
        private readonly int _maxExtends;

        /// <summary>
        /// Get memory segment sizes
        /// </summary>
        private readonly int[] _segmentSizes;

        /// <summary>
        /// Get datafile stream
        /// </summary>
        private readonly Stream _stream;

        public MemoryCache(Stream stream, int[] memorySegmentSizes, int maxExtends)
        {
            _stream = stream;
            _segmentSizes = memorySegmentSizes;
            _maxExtends = maxExtends;

            this.Extend();
        }

        #region Readable Pages

        /// <summary>
        /// Get page from clean cache (readable). If page not exits, create this new page and load data using factory fn
        /// </summary>
        public async Task<PageBuffer> GetReadablePage(long position)
        {
            // try get from _readble dict or create new
            if (!_readable.TryGetValue(position, out var page))
            {
                // get new page from _free pages (or extend)
                page = this.GetFreePage();

                page.Position = position;
                page.Timestamp = DateTime.UtcNow.Ticks;

                // set stream position 
                _stream.Position = position;

                // read async from stream
                await _stream.ReadAsync(page.Array, page.Offset, PAGE_SIZE);
            }

            return page;
        }

        #endregion

        #region Writable Pages

        /// <summary>
        /// Request for a writable page - no other can read this page and this page has no reference
        /// Writable pages can be MoveToReadable() or DiscardWritable() - but never Released()
        /// </summary>
        public async Task<PageBuffer> GetWritablePage(long position)
        {
            // write pages always contains a new buffer array
            var writable = this.NewPage(position);

            // if requested page already in cache, just copy buffer and avoid load from stream
            if (_readable.TryGetValue(position, out var clean))
            {
                Buffer.BlockCopy(clean.Array, clean.Offset, writable.Array, writable.Offset, PAGE_SIZE);
            }
            else
            {
                // set stream position 
                _stream.Position = position;

                // read async from stream
                await _stream.ReadAsync(writable.Array, writable.Offset, PAGE_SIZE);
            }

            return writable;
        }

        /// <summary>
        /// Create new page using an empty buffer block. Mark this page as writable.
        /// </summary>
        public PageBuffer NewPage()
        {
            return this.NewPage(long.MaxValue);
        }

        /// <summary>
        /// Create new page using an empty buffer block. Mark this page as writable.
        /// </summary>
        private PageBuffer NewPage(long position)
        {
            var page = this.GetFreePage();

            // set page position and page as writable
            page.Position = position;

            // define as writable
            page.IsWritable = true;

            // Timestamp = 0 means this page was never used (do not clear)
            if (page.Timestamp > 0)
            {
                page.Clear();
            }

            DEBUG(page.All(0), "new page must be full zero empty before return");

            page.Timestamp = DateTime.UtcNow.Ticks;

            return page;
        }

        /// <summary>
        /// Try move this page to readable list (if not alrady in readable list)
        /// Returns true if was moved
        /// </summary>
        public bool TryMoveToReadable(PageBuffer page)
        {
            ENSURE(page.Position != long.MaxValue, "page must have a position");
            ENSURE(page.IsWritable, "page must be writable");

            var added = _readable.TryAdd(page.Position, page);

            if (added)
            {
                page.IsWritable = false;
            }

            return added;
        }

        /// <summary>
        /// Move a writable page to readable list - if already exisits, override content
        /// Used after write operation that must mark page as readable becase page content was changed
        /// This method runs BEFORE send to write disk queue - but new page request must read this new content
        /// Returns readable page
        /// </summary>
        public PageBuffer MoveToReadable(PageBuffer page)
        {
            ENSURE(page.Position != long.MaxValue, "page must have position to be readable");
            ENSURE(page.IsWritable, "page must be writable before from to readable dict");

            // mark page as readble
            page.IsWritable = false;

            if (_readable.TryAdd(page.Position, page))
            {
                return page;
            }
            else
            {
                // if page already exists, update content 
                var current = _readable[page.Position];

                // if page already in cache, this is a duplicate page in memory
                // must update cached page with new page content
                Buffer.BlockCopy(page.Array, page.Offset, current.Array, current.Offset, PAGE_SIZE);

                // discard current into a free page
                this.DiscardPage(page);

                // return page that are in _readble list
                return current;
            }
        }

        #endregion

        #region DiscardPages

        /// <summary>
        /// When a page are requested as Writable but not saved in disk, must be discard before release
        /// </summary>
        public void DiscardDirtyPages(IEnumerable<PageBuffer> pages)
        {
            // only for ROLLBACK action
            foreach (var page in pages)
            {
                // complete discard page and content
                this.DiscardPage(page);
            }
        }

        /// <summary>
        /// Discard pages that contains valid data and was not modified
        /// </summary>
        public void DiscardCleanPages(IEnumerable<PageBuffer> pages)
        {
            foreach (var page in pages)
            {
                // if page was not modified, try move to readable list
                if (this.TryMoveToReadable(page) == false)
                {
                    // if already in readable list, just discard
                    this.DiscardPage(page);
                }
            }
        }

        /// <summary>
        /// Complete discard a writable page - clean content and move to free list
        /// </summary>
        public void DiscardPage(PageBuffer page)
        {
            // clear page controls
            page.IsWritable = false;
            page.Position = long.MaxValue;

            // clear content
            page.Fill(0);

            // added into free list
            _free.Enqueue(page);
        }

        #endregion

        #region Cache managment

        /// <summary>
        /// Get a clean, re-usable page from store. If store are empty, can extend buffer segments
        /// </summary>
        private PageBuffer GetFreePage()
        {
            if (_free.TryDequeue(out var page))
            {
                return page;
            }
            else
            {
                this.Extend();

                return this.GetFreePage();
            }
        }

        /// <summary>
        /// Check if it's possible move readable pages to free list - if not possible, extend memory
        /// </summary>
        private void Extend()
        {
            // get segmentSize
            var segmentSize = _segmentSizes[Math.Min(_segmentSizes.Length - 1, _extends)];

            if (_extends <= _maxExtends)
            {
                // create big linear array in heap memory (LOH => 85Kb)
                var buffer = new byte[PAGE_SIZE * segmentSize];

                // split linear array into many array slices
                for (var i = 0; i < segmentSize; i++)
                {
                    var uniqueID = (_extends * segmentSize) + i + 1;

                    _free.Enqueue(new PageBuffer(buffer, i * PAGE_SIZE, uniqueID));
                }

                _extends++;
            }
            else
            {
                // try get clean pages from readbles
                var readables = _readable
                    .OrderBy(x => x.Value.Timestamp)
                    .Select(x => x.Key)
                    .Take(segmentSize)
                    .ToArray();

                // move pages from readable list to free list
                foreach (var key in readables)
                {
                    _readable.Remove(key, out var page);

                    this.DiscardPage(page);
                }

                // memory overflow
                if (readables.Length == 0)
                {
                    var limit = FileHelper.FormatFileSize(this.ExtendPages * PAGE_SIZE);

                    throw new LiteException(0, $"LiteDB has reached the maximum memory limit ({limit}). Try to use smaller transactions to avoid this error");
                }
            }
        }

        /// <summary>
        /// Return how many pages are available completed free
        /// </summary>
        public int FreePages => _free.Count;

        /// <summary>
        /// Return how many segments already loaded in memory
        /// </summary>
        public int ExtendSegments => _extends;

        /// <summary>
        /// Get how many pages this cache extends in memory
        /// </summary>
        public int ExtendPages => Enumerable.Range(0, _extends).Select(x => _segmentSizes[Math.Min(_segmentSizes.Length - 1, x)]).Sum();

        /// <summary>
        /// Get how many pages are used as Writable at this moment
        /// </summary>
        public int WritablePages => this.ExtendPages - // total memory
            _free.Count - _readable.Count; // allocated pages

        /// <summary>
        /// Get all readable pages
        /// </summary>
        public ICollection<PageBuffer> GetPages() => _readable.Values;

        /// <summary>
        /// Clean all cache memory - moving back all readable pages into free list
        /// This command must be called inside a exclusive lock
        /// </summary>
        public int Clear()
        {
            var counter = 0;

            foreach (var page in _readable.Values)
            {
                this.DiscardPage(page);

                counter++;
            }

            _readable.Clear();

            return counter;
        }

        #endregion

        public void Dispose()
        {
        }
    }
}