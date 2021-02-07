using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using LiteDB.Engine;
using static LiteDB.Constants;

namespace LiteDB.Engine
{
    /// <summary>
    /// Internal class to parse and execute sql-like commands
    /// </summary>
    internal partial class SqlParser
    {
        private readonly ILiteEngine _engine;
        private readonly Tokenizer _tokenizer;
        private readonly BsonDocument _parameters;
        private readonly Lazy<Collation> _collation;

        public SqlParser(ILiteEngine engine, Tokenizer tokenizer, BsonDocument parameters)
        {
            _engine = engine;
            _tokenizer = tokenizer;
            _parameters = parameters ?? new BsonDocument();
            _collation = new Lazy<Collation>(() => new Collation(_engine.Pragma(Pragmas.COLLATION)));
        }

        public async Task<IBsonDataReader> Execute()
        {
            var ahead = _tokenizer.LookAhead().Expect(TokenType.Word);

            LOG($"executing `{ahead.Value.ToUpper()}`", "SQL");

            switch (ahead.Value.ToUpper())
            {
                case "SELECT": 
                case "EXPLAIN":
                    return await this.ParseSelect();
                case "INSERT": return await this.ParseInsert();
                case "DELETE": return await this.ParseDelete();
                case "UPDATE": return await this.ParseUpdate();
                case "DROP": return await this.ParseDrop();
                case "RENAME": return await this.ParseRename();
                case "CREATE": return await this.ParseCreate();

                case "CHECKPOINT": return await this.ParseCheckpoint();

                case "BEGIN": return await this.ParseBegin();
                case "ROLLBACK": return await this.ParseRollback();
                case "COMMIT": return await this.ParseCommit();

                case "PRAGMA": return await this.ParsePragma();

                default:  throw LiteException.UnexpectedToken(ahead);
            }
        }
    }
}