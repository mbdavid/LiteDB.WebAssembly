using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using static LiteDB.Constants;

namespace LiteDB.Engine
{
    /// <summary>
    /// A public class that take care of all engine data structure access - it´s basic implementation of a NoSql database
    /// Its isolated from complete solution - works on low level only (no linq, no poco... just BSON objects)
    /// [ThreadSafe]
    /// </summary>
    public partial class LiteEngine : ILiteEngine
    {
        #region Services instances

        private LockService _locker;

        private DiskService _disk;

        private WalIndexService _walIndex;

        private HeaderPage _header;

        private TransactionMonitor _monitor;

        private SortDisk _sortDisk;

        // immutable settings
        private readonly EngineSettings _settings;

        private bool _disposed = false;

        #endregion

        #region Ctor

        /// <summary>
        /// Create new LiteEngine using in-memory database
        /// </summary>
        public LiteEngine()
            : this(new EngineSettings { DataStream = new MemoryStream() })
        {
        }

        /// <summary>
        /// Create new LiteEngine using datafile filename
        /// </summary>
        public LiteEngine(string filename)
            : this (new EngineSettings { Filename = filename })
        {
        }

        /// <summary>
        /// Use full engine settings to create new LiteEngine instance
        /// </summary>
        public LiteEngine(EngineSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// Initialize database
        /// </summary>
        public async Task OpenAsync()
        { 
            LOG($"start initializing{(_settings.ReadOnly ? " (readonly)" : "")}", "ENGINE");

            try
            {
                // initialize disk service (will create database if needed)
                _disk = new DiskService(_settings, MEMORY_SEGMENT_SIZES);

                // get header page from disk service
                _header = _disk.Header;
                
                // test for same collation
                if (_settings.Collation != null && _settings.Collation.ToString() != _header.Pragmas.Collation.ToString())
                {
                    throw new LiteException(0, $"Datafile collation '{_header.Pragmas.Collation}' is different from engine settings. Use Rebuild database to change collation.");
                }

                // initialize locker service
                _locker = new LockService(_header.Pragmas);

                // initialize wal-index service
                _walIndex = new WalIndexService(_disk, _locker);

                // restore wal index references, if exists
                _walIndex.RestoreIndex(_header);

                // initialize sort temp disk
                _sortDisk = new SortDisk(_settings.CreateTempFactory(), CONTAINER_SORT_SIZE, _header.Pragmas);

                // initialize transaction monitor as last service
                _monitor = new TransactionMonitor(_header, _settings, _locker, _disk, _walIndex);

                // register system collections
                this.InitializeSystemCollections();

                LOG("initialization completed", "ENGINE");
            }
            catch (Exception ex)
            {
                LOG(ex.Message, "ERROR");

                // explicit dispose (but do not run shutdown operation)
                this.Dispose(true);
                throw;
            }
        }

        #endregion

#if DEBUG
        // exposes for unit tests
        internal TransactionMonitor GetMonitor() => _monitor;
#endif

        /// <summary>
        /// Run checkpoint command to copy log file into data file
        /// </summary>
        public int Checkpoint() => _walIndex.Checkpoint(false, true);

        public void Dispose()
        {
            // dispose data file
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~LiteEngine()
        {
            this.Dispose(false);
        }

        /// <summary>
        /// Shutdown process:
        /// - Stop any new transaction
        /// - Stop operation loops over database (throw in SafePoint)
        /// - Wait for writer queue
        /// - Close disks
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            // this method can be called from Ctor, so many 
            // of this members can be null yet (even if are readonly). 
            if (_disposed) return;

            if (disposing)
            {
                // stop running all transactions
                _monitor?.Dispose();

                // do a soft checkpoint (only if exclusive lock is possible)
                if (_header?.Pragmas.Checkpoint > 0 && _settings.ReadOnly == false) _walIndex?.Checkpoint(true, true);

                // close all disk streams (and delete log if empty)
                _disk?.Dispose();

                // delete sort temp file
                _sortDisk?.Dispose();

                // dispose lockers
                _locker?.Dispose();
            }

            LOG("engine disposed", "ENGINE");

            _disposed = true;
        }

    }
}