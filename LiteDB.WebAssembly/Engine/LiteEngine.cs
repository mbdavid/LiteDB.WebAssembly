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

        private SemaphoreSlim _locker = new SemaphoreSlim(1, 1);

        private DiskService _disk;

        private WalIndexService _walIndex;

        private HeaderPage _header;

        private readonly Stream _stream;

        private readonly Collation _collation;

        private bool _disposed = true;

        #endregion

        #region Ctor

        /// <summary>
        /// Create new LiteEngine using in-memory database
        /// </summary>
        public LiteEngine()
            : this(new MemoryStream())
        {
        }

        /// <summary>
        /// Create new LiteEngine instance using custom Stream
        /// </summary>
        public LiteEngine(Stream stream, Collation collation = null)
        {
            _stream = stream;
            _collation = collation ?? Collation.Default;
        }

        /// <summary>
        /// Initialize database
        /// </summary>
        public async Task OpenAsync()
        { 
            LOG("start initializing", "ENGINE");

            try
            {
                // open async stream
                if (_stream is IAsyncInitialize s) await s.InitializeAsync();

                // initialize disk service (will create database if needed)
                _disk = await DiskService.CreateAsync(_stream, _collation, MEMORY_SEGMENT_SIZES, MAX_EXTENDS);

                // get header page from disk service
                _header = _disk.Header;
                
                // test for same collation
                if (_collation.ToString() != _header.Pragmas.Collation.ToString())
                {
                    throw new LiteException(0, $"Datafile collation '{_header.Pragmas.Collation}' is different from engine settings. Use Rebuild database to change collation.");
                }

                // initialize wal-index service
                _walIndex = new WalIndexService(_disk);

                // restore wal index references, if exists
                await _walIndex.RestoreIndex(_header);

                // register system collections
                // this.InitializeSystemCollections();
                _disposed = false;

                LOG("initialization completed", "ENGINE");
            }
            catch (Exception ex)
            {
                LOG(ex.Message, "ERROR");

                // explicit dispose (but do not run shutdown operation)
                await this.DisposeAsync();

                throw;
            }
        }

        #endregion

        /// <summary>
        /// Return if engine already open
        /// </summary>
        public bool IsOpen => _disposed == false;

        /// <summary>
        /// Run checkpoint command to copy log file into data file
        /// </summary>
        public Task<int> CheckpointAsync()
        {
            if (_disposed == true) throw LiteException.DatabaseClosed();

            return _walIndex.Checkpoint();
        }

        /// <summary>
        /// Shutdown process:
        /// - [[[[DESCRIBE]]]]
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            // this method can be called from Ctor, so many 
            // of this members can be null yet (even if are readonly). 
            if (_disposed) return;

            _disposed = true;

            // do a soft checkpoint (only if exclusive lock is possible)
            if (_header?.Pragmas.Checkpoint > 0) await _walIndex?.Checkpoint();

            // close all disk streams (and delete log if empty)
            _disk?.Dispose();

            // dispose lockers
            _locker.Dispose();

            LOG("engine disposed", "ENGINE");

        }

    }
}