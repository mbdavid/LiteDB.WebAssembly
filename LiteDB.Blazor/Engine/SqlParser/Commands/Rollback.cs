using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using LiteDB.Engine;
using static LiteDB.Constants;

namespace LiteDB.Engine
{
    internal partial class SqlParser
    {
        /// <summary>
        /// ROLLBACK [ TRANS | TRANSACTION ]
        /// </summary>
        private async Task<BsonDataReader> ParseRollback()
        {
            _tokenizer.ReadToken().Expect("ROLLBACK");

            var token = _tokenizer.ReadToken().Expect(TokenType.Word, TokenType.EOF, TokenType.SemiColon);

            if (token.Is("TRANS") || token.Is("TRANSACTION"))
            {
                _tokenizer.ReadToken().Expect(TokenType.EOF, TokenType.SemiColon);
            }

            var result = await _engine.RollbackAsync();

            return new BsonDataReader(result);
        }
    }
}