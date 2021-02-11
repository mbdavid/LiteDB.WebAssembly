using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using LiteDB.Engine;
using static LiteDB.Constants;

namespace LiteDB
{
    /// <summary>
    /// The LiteDB database. Used for create a LiteDB instance and use all storage resources. It's the database connection
    /// </summary>
    public partial class LiteDatabase : ILiteDatabase
    {
        #region Properties

        private readonly ILiteEngine _engine;
        private readonly BsonMapper _mapper;

        /// <summary>
        /// Get current instance of BsonMapper used in this database instance (can be BsonMapper.Global)
        /// </summary>
        public BsonMapper Mapper => _mapper;

        #endregion

        #region Ctor

        /// <summary>
        /// Create new instance of LiteDatabase using an external Stream source as database file
        /// </summary>
        public LiteDatabase(Stream stream, Collation collation = null, BsonMapper mapper = null)
        {
            _engine = new LiteEngine(stream, collation);
            _mapper = mapper ?? BsonMapper.Global;
        }

        #endregion

        #region Collections

        /// <summary>
        /// Get a collection using a entity class as strong typed document. If collection does not exits, create a new one.
        /// </summary>
        /// <param name="name">Collection name (case insensitive)</param>
        /// <param name="autoId">Define autoId data type (when object contains no id field)</param>
        public ILiteCollection<T> GetCollection<T>(string name, BsonAutoId autoId = BsonAutoId.ObjectId)
        {
            return new LiteCollection<T>(name, autoId, _engine, _mapper);
        }

        /// <summary>
        /// Get a collection using a name based on typeof(T).Name (BsonMapper.ResolveCollectionName function)
        /// </summary>
        public ILiteCollection<T> GetCollection<T>()
        {
            return this.GetCollection<T>(null);
        }

        /// <summary>
        /// Get a collection using a name based on typeof(T).Name (BsonMapper.ResolveCollectionName function)
        /// </summary>
        public ILiteCollection<T> GetCollection<T>(BsonAutoId autoId)
        {
            return this.GetCollection<T>(null, autoId);
        }

        /// <summary>
        /// Get a collection using a generic BsonDocument. If collection does not exits, create a new one.
        /// </summary>
        /// <param name="name">Collection name (case insensitive)</param>
        /// <param name="autoId">Define autoId data type (when document contains no _id field)</param>
        public ILiteCollection<BsonDocument> GetCollection(string name, BsonAutoId autoId = BsonAutoId.ObjectId)
        {
            if (name.IsNullOrWhiteSpace()) throw new ArgumentNullException(nameof(name));

            return new LiteCollection<BsonDocument>(name, autoId, _engine, _mapper);
        }

        #endregion

        #region Transaction

        /// <summary>
        /// Initialize a new transaction. Transaction are created "per-thread". There is only one single transaction per thread.
        /// Return true if transaction was created or false if current thread already in a transaction.
        /// </summary>
        public Task<bool> BeginTransAsync() => _engine.BeginTransAsync();

        /// <summary>
        /// Commit current transaction
        /// </summary>
        public Task<bool> CommitAsync() => _engine.CommitAsync();

        /// <summary>
        /// Rollback current transaction
        /// </summary>
        public Task<bool> RollbackAsync() => _engine.RollbackAsync();

        #endregion

        #region Shortcut

        /// <summary>
        /// Get all collections name inside this database.
        /// </summary>
        public IEnumerable<string> GetCollectionNames()
        {
            throw new NotImplementedException();
            //// use $cols system collection with type = user only
            //var cols = this.GetCollection("$cols")
            //    .Query()
            //    .Where("type = 'user'")
            //    .ToDocuments()
            //    .Select(x => x["name"].AsString)
            //    .ToArray();

            //return cols;
        }

        /// <summary>
        /// Checks if a collection exists on database. Collection name is case insensitive
        /// </summary>
        public bool CollectionExists(string name)
        {
            if (name.IsNullOrWhiteSpace()) throw new ArgumentNullException(nameof(name));

            return this.GetCollectionNames().Contains(name, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Drop a collection and all data + indexes
        /// </summary>
        public async Task<bool> DropCollectionAsync(string name)
        {
            if (name.IsNullOrWhiteSpace()) throw new ArgumentNullException(nameof(name));

            return await _engine.DropCollectionAsync(name);
        }

        /// <summary>
        /// Rename a collection. Returns false if oldName does not exists or newName already exists
        /// </summary>
        public async Task<bool> RenameCollectionAsync(string oldName, string newName)
        {
            if (oldName.IsNullOrWhiteSpace()) throw new ArgumentNullException(nameof(oldName));
            if (newName.IsNullOrWhiteSpace()) throw new ArgumentNullException(nameof(newName));

            return await _engine.RenameCollectionAsync(oldName, newName);
        }

        #endregion

        #region Execute SQL

        /// <summary>
        /// Execute SQL commands and return as data reader.
        /// </summary>
        public async Task<IBsonDataReader> ExecuteAsync(TextReader commandReader, BsonDocument parameters = null)
        {
            if (commandReader == null) throw new ArgumentNullException(nameof(commandReader));

            var tokenizer = new Tokenizer(commandReader);
            var sql = new SqlParser(_engine, tokenizer, parameters);
            var reader = await sql.Execute();

            return reader;
        }

        /// <summary>
        /// Execute SQL commands and return as data reader
        /// </summary>
        public Task<IBsonDataReader> ExecuteAsync(string command, BsonDocument parameters = null)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            var tokenizer = new Tokenizer(command);
            var sql = new SqlParser(_engine, tokenizer, parameters);

            return sql.Execute();
        }

        /// <summary>
        /// Execute SQL commands and return as data reader
        /// </summary>
        public Task<IBsonDataReader> ExecuteAsync(string command, params BsonValue[] args)
        {
            var p = new BsonDocument();
            var index = 0;

            foreach (var arg in args)
            {
                p[index.ToString()] = arg;
                index++;
            }

            return this.ExecuteAsync(command, p);
        }

        #endregion

        #region Checkpoint/Open

        public Task OpenAsync() => _engine.OpenAsync();

        public bool IsOpen => _engine.IsOpen;

        /// <summary>
        /// Do database checkpoint. Copy all commited transaction from log file into datafile.
        /// </summary>
        public Task CheckpointAsync() => _engine.CheckpointAsync();

        #endregion

        #region Pragmas

        /// <summary>
        /// Get value from internal engine variables
        /// </summary>
        public BsonValue Pragma(string name)
        {
            return _engine.Pragma(name);
        }

        /// <summary>
        /// Set new value to internal engine variables
        /// </summary>
        public async Task<BsonValue> PragmaAsync(string name, BsonValue value)
        {
            return await _engine.PragmaAsync(name, value);
        }

        /// <summary>
        /// Get database user version - use this version number to control database change model
        /// </summary>
        public int UserVersion => _engine.Pragma(Pragmas.USER_VERSION);

        /// <summary>
        /// Get/Set database timeout - this timeout is used to wait for unlock using transactions
        /// </summary>
        public TimeSpan Timeout => TimeSpan.FromSeconds(_engine.Pragma(Pragmas.TIMEOUT).AsInt32);

        /// <summary>
        /// Get if database will deserialize dates in UTC timezone or Local timezone (default: Local)
        /// </summary>
        public bool UtcDate => _engine.Pragma(Pragmas.UTC_DATE);

        /// <summary>
        /// Get database limit size (in bytes). New value must be equals or larger than current database size
        /// </summary>
        public long LimitSize => _engine.Pragma(Pragmas.LIMIT_SIZE);

        /// <summary>
        /// Get in how many pages (8 Kb each page) log file will auto checkpoint (copy from log file to data file). Use 0 to manual-only checkpoint (and no checkpoint on dispose)
        /// </summary>
        public int CheckpointSize => _engine.Pragma(Pragmas.CHECKPOINT);

        /// <summary>
        /// Get database collection (this options can be changed only in rebuild proces)
        /// </summary>
        public Collation Collation => new Collation(_engine.Pragma(Pragmas.COLLATION).AsString);

        #endregion

        public ValueTask DisposeAsync() => _engine.DisposeAsync();
    }
}
