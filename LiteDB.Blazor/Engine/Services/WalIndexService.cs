using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static LiteDB.Constants;

namespace LiteDB.Engine
{
    /// <summary>
    /// Do all WAL index services based on LOG file - has only single instance per engine
    /// [Singleton - ThreadSafe]
    /// </summary>
    internal class WalIndexService
    {
        private readonly DiskService _disk;

        private readonly Dictionary<uint, List<KeyValuePair<int, long>>> _index = new Dictionary<uint, List<KeyValuePair<int, long>>>();

        private readonly HashSet<uint> _confirmTransactions = new HashSet<uint>();

        private int _currentReadVersion = 0;

        /// <summary>
        /// Store last used transaction ID
        /// </summary>
        private uint _lastTransactionID = 0;

        public WalIndexService(DiskService disk)
        {
            _disk = disk;
        }

        /// <summary>
        /// Get current read version for all new transactions
        /// </summary>
        public int CurrentReadVersion => _currentReadVersion;

        /// <summary>
        /// Get current counter for transaction ID
        /// </summary>
        public uint LastTransactionID => _lastTransactionID;

        /// <summary>
        /// Get new transactionID
        /// </summary>
        public uint NextTransactionID() => ++_lastTransactionID;

        /// <summary>
        /// Checks if a Page/Version are in WAL-index memory. Consider version that are below parameter. Returns PagePosition of this page inside WAL-file or Empty if page doesn't found.
        /// </summary>
        public long GetPageIndex(uint pageID, int version, out int walVersion)
        {
            // wal-index versions must be greater than 0 (version 0 is datafile)
            if (version == 0)
            {
                walVersion = 0;
                return long.MaxValue;
            }

            // get page slot in cache
            if (_index.TryGetValue(pageID, out var list))
            {
                // list are sorted by version number
                var idx = list.Count;
                var position = long.MaxValue;

                walVersion = version;

                // get all page versions in wal-index
                // and then filter only equals-or-less then selected version
                while (idx > 0)
                {
                    idx--;

                    var v = list[idx];

                    if (v.Key <= version)
                    {
                        walVersion = v.Key;

                        position = v.Value;
                        break;
                    }
                }

                return position;
            }

            walVersion = int.MaxValue;

            return long.MaxValue;
        }

        /// <summary>
        /// Add transactionID in confirmed list and update WAL index with all pages positions
        /// </summary>
        public void ConfirmTransaction(uint transactionID, ICollection<PagePosition> pagePositions)
        {
            // increment current version
            _currentReadVersion++;

            // update wal-index
            foreach (var pos in pagePositions)
            {
                if (_index.TryGetValue(pos.PageID, out var slot) == false)
                {
                    slot = new List<KeyValuePair<int, long>>();

                    _index.Add(pos.PageID, slot);
                }

                // add version/position into pageID slot
                slot.Add(new KeyValuePair<int, long>(_currentReadVersion, pos.Position));
            }

            // add transaction as confirmed
            _confirmTransactions.Add(transactionID);
        }

        /// <summary>
        /// Load all confirmed transactions from log file (used only when open datafile)
        /// Don't need lock because it's called on ctor of LiteEngine
        /// Update _disk instance with last log position
        /// </summary>
        public async Task RestoreIndex(HeaderPage header)
        {
            // get all page positions
            var positions = new Dictionary<long, List<PagePosition>>();
            var last = 0L;

            // read all pages to get confirmed transactions (do not read page content, only page header)
            await foreach (var buffer in _disk.ReadLog(true))
            {
                // read direct from buffer to avoid create BasePage structure
                var pageID = buffer.ReadUInt32(BasePage.P_PAGE_ID);
                var isConfirmed = buffer.ReadBool(BasePage.P_IS_CONFIRMED);
                var transactionID = buffer.ReadUInt32(BasePage.P_TRANSACTION_ID);

                if (transactionID == 0 || transactionID == uint.MaxValue) continue;

                var position = new PagePosition(pageID, buffer.Position);

                if (positions.TryGetValue(transactionID, out var list))
                {
                    list.Add(position);
                }
                else
                {
                    positions[transactionID] = new List<PagePosition> { position };
                }

                if (isConfirmed)
                {
                    // update last IsConfirmed page
                    last = buffer.Position;

                    this.ConfirmTransaction(transactionID, positions[transactionID]);

                    var pageType = (PageType)buffer.ReadByte(BasePage.P_PAGE_TYPE);

                    // when a header is modified in transaction, must always be the last page inside log file (per transaction)
                    if (pageType == PageType.Header)
                    {
                        // copy this buffer block into original header block
                        Buffer.BlockCopy(buffer.Array, buffer.Offset, header.Buffer.Array, header.Buffer.Offset, PAGE_SIZE);

                        header.LoadPage();
                    }
                }

                // update last transaction ID
                _lastTransactionID = transactionID;
            }

            // update last log position acording with last IsConfirmed on log
            if (last > 0)
            {
                _disk.LogEndPosition = last + PAGE_SIZE;
            }
        }

        /// <summary>
        /// Do checkpoint operation to copy log pages into data file. Return how many pages as saved into data file
        /// Soft checkpoint try execute only if there is no one using (try exclusive lock - if not possible just exit)
        /// If crop = true, reduce data file removing all log area. If not, keeps file with same size and clean all isConfirmed pages
        /// </summary>
        public async Task<int> Checkpoint()
        {
            LOG($"checkpoint", "WAL");

            var counter = 0;

            // getting all "good" pages from log file to be copied into data file
            async IAsyncEnumerable<PageBuffer> source()
            {
                // collect all isConfirmedPages
                var confirmedPages = new List<long>();
                var finalDataPosition = (_disk.Header.LastPageID + 1) * PAGE_SIZE;

                await foreach (var buffer in _disk.ReadLog(false))
                {
                    // read direct from buffer to avoid create BasePage structure
                    var transactionID = buffer.ReadUInt32(BasePage.P_TRANSACTION_ID);

                    // only confirmed pages can be write on data disk
                    if (_confirmTransactions.Contains(transactionID))
                    {
                        // if page is confirmed page and are after data area, add to list
                        var isConfirmed = buffer.ReadBool(BasePage.P_IS_CONFIRMED);

                        if (isConfirmed && buffer.Position >= finalDataPosition)
                        {
                            confirmedPages.Add(buffer.Position);
                        }

                        // clear isConfirmed/transactionID
                        buffer.Write(uint.MaxValue, BasePage.P_TRANSACTION_ID);
                        buffer.Write(false, BasePage.P_IS_CONFIRMED);

                        // update buffer position to data area position
                        var pageID = buffer.ReadUInt32(BasePage.P_PAGE_ID);

                        buffer.Position = BasePage.GetPagePosition(pageID);

                        counter++;

                        yield return buffer;
                    }
                }
            }

            // write all log pages into data file (sync)
            await _disk.WriteDataPages(source());

            // clear log file, clear wal index, memory cache,
            _confirmTransactions.Clear();
            _index.Clear();

            _lastTransactionID = 0;
            _currentReadVersion = 0;

            // clear cache
            _disk.Cache.Clear();

            _disk.ResetLogPosition(true);

            return counter;
        }
    }
}