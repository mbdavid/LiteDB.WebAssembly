using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using LiteDB.Engine;

namespace LiteDB
{
    public interface ILiteDatabase : IAsyncDisposable
    {
        /// <summary>
        /// Get current instance of BsonMapper used in this database instance (can be BsonMapper.Global)
        /// </summary>
        BsonMapper Mapper { get; }

        /// <summary>
        /// Get a collection using a entity class as strong typed document. If collection does not exits, create a new one.
        /// </summary>
        /// <param name="name">Collection name (case insensitive)</param>
        /// <param name="autoId">Define autoId data type (when object contains no id field)</param>
        ILiteCollection<T> GetCollection<T>(string name, BsonAutoId autoId = BsonAutoId.ObjectId);

        /// <summary>
        /// Get a collection using a name based on typeof(T).Name (BsonMapper.ResolveCollectionName function)
        /// </summary>
        ILiteCollection<T> GetCollection<T>();

        /// <summary>
        /// Get a collection using a name based on typeof(T).Name (BsonMapper.ResolveCollectionName function)
        /// </summary>
        ILiteCollection<T> GetCollection<T>(BsonAutoId autoId);

        /// <summary>
        /// Get a collection using a generic BsonDocument. If collection does not exits, create a new one.
        /// </summary>
        /// <param name="name">Collection name (case insensitive)</param>
        /// <param name="autoId">Define autoId data type (when document contains no _id field)</param>
        ILiteCollection<BsonDocument> GetCollection(string name, BsonAutoId autoId = BsonAutoId.ObjectId);

        /// <summary>
        /// Open/Connect to database - load header from disk
        /// </summary>
        Task OpenAsync();

        /// <summary>
        /// Return if current database already open
        /// </summary>
        bool IsOpen { get; }

        /// <summary>
        /// Initialize a new transaction. Transaction are created "per-thread". There is only one single transaction per thread.
        /// Return true if transaction was created or false if current thread already in a transaction.
        /// </summary>
        Task<bool> BeginTransAsync();

        /// <summary>
        /// Commit current transaction
        /// </summary>
        Task<bool> CommitAsync();

        /// <summary>
        /// Rollback current transaction
        /// </summary>
        Task<bool> RollbackAsync();

        /// <summary>
        /// Get all collections name inside this database.
        /// </summary>
        IEnumerable<string> GetCollectionNames();

        /// <summary>
        /// Checks if a collection exists on database. Collection name is case insensitive
        /// </summary>
        bool CollectionExists(string name);

        /// <summary>
        /// Drop a collection and all data + indexes
        /// </summary>
        Task<bool> DropCollectionAsync(string name);

        /// <summary>
        /// Rename a collection. Returns false if oldName does not exists or newName already exists
        /// </summary>
        Task<bool> RenameCollectionAsync(string oldName, string newName);

        /// <summary>
        /// Execute SQL commands and return as data reader.
        /// </summary>
        Task<IBsonDataReader> ExecuteAsync(TextReader commandReader, BsonDocument parameters = null);

        /// <summary>
        /// Execute SQL commands and return as data reader
        /// </summary>
        Task<IBsonDataReader> ExecuteAsync(string command, BsonDocument parameters = null);

        /// <summary>
        /// Execute SQL commands and return as data reader
        /// </summary>
        Task<IBsonDataReader> ExecuteAsync(string command, params BsonValue[] args);

        /// <summary>
        /// Do database checkpoint. Copy all commited transaction from log file into datafile.
        /// </summary>
        Task CheckpointAsync();

        /// <summary>
        /// Get value from internal engine variables
        /// </summary>
        BsonValue Pragma(string name);

        /// <summary>
        /// Set new value to internal engine variables
        /// </summary>
        Task<BsonValue> PragmaAsync(string name, BsonValue value);

        /// <summary>
        /// Get/Set database user version - use this version number to control database change model
        /// </summary>
        int UserVersion { get; }

        /// <summary>
        /// Get/Set database timeout - this timeout is used to wait for unlock using transactions
        /// </summary>
        TimeSpan Timeout { get; }

        /// <summary>
        /// Get/Set if database will deserialize dates in UTC timezone or Local timezone (default: Local)
        /// </summary>
        bool UtcDate { get; }

        /// <summary>
        /// Get/Set database limit size (in bytes). New value must be equals or larger than current database size
        /// </summary>
        long LimitSize { get; }

        /// <summary>
        /// Get/Set in how many pages (8 Kb each page) log file will auto checkpoint (copy from log file to data file). Use 0 to manual-only checkpoint (and no checkpoint on dispose)
        /// Default: 1000 pages
        /// </summary>
        int CheckpointSize { get; }

        /// <summary>
        /// Get database collection (this options can be changed only in rebuild proces)
        /// </summary>
        Collation Collation { get; }
    }
}