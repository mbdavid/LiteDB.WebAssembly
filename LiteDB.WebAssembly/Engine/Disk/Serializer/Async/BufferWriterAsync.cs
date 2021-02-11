using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using static LiteDB.Constants;

namespace LiteDB.Engine
{
    /// <summary>
    /// Write data types/BSON data into byte[]. It's forward only and support multi buffer slice as source
    /// </summary>
    internal class BufferWriterAsync : IAsyncDisposable
    {
        private readonly IAsyncEnumerator<BufferSlice> _source;

        private BufferSlice _current;
        private int _currentPosition = 0; // position in _current
        private int _position = 0; // global position

        private bool _isEOF = false;

        /// <summary>
        /// Current global cursor position
        /// </summary>
        public int Position => _position;

        /// <summary>
        /// Indicate position are at end of last source array segment
        /// </summary>
        public bool IsEOF => _isEOF;

        /// <summary>
        /// Async constructor
        /// </summary>
        public static async Task<BufferWriterAsync> CreateAsync(IAsyncEnumerable<BufferSlice> source)
        {
            var writer = new BufferWriterAsync(source);
            await writer.InitializeAsync();
            return writer;
        }

        private BufferWriterAsync(IAsyncEnumerable<BufferSlice> source)
        {
            _source = source.GetAsyncEnumerator();
        }

        private async Task InitializeAsync()
        {
            var read = await _source.MoveNextAsync();
            if (!read) _isEOF = true;
            _current = _source.Current;
        }

        #region Basic Write

        /// <summary>
        /// Move forward in current segment. If array segment finish, open next segment
        /// Returns true if move to another segment - returns false if continue in same segment
        /// </summary>
        private async Task<bool> MoveForward(int count)
        {
            // do not move forward if source finish
            if (_isEOF) return false;

            ENSURE(_currentPosition + count <= _current.Count, "forward is only for current segment");

            _currentPosition += count;
            _position += count;

            // request new source array if _current all consumed
            if (_currentPosition == _current.Count)
            {
                if (_source == null || await _source.MoveNextAsync() == false)
                {
                    _isEOF = true;
                }
                else
                {
                    _current = _source.Current;
                    _currentPosition = 0;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Write bytes from buffer into segmentsr. Return how many bytes was write
        /// </summary>
        public async Task<int> Write(byte[] buffer, int offset, int count)
        {
            var bufferPosition = 0;

            while (bufferPosition < count)
            {
                var bytesLeft = _current.Count - _currentPosition;
                var bytesToCopy = Math.Min(count - bufferPosition, bytesLeft);

                // fill buffer
                if (buffer != null)
                {
                    Buffer.BlockCopy(buffer, 
                        offset + bufferPosition,
                        _current.Array,
                        _current.Offset + _currentPosition, 
                        bytesToCopy);
                }

                bufferPosition += bytesToCopy;

                // move position in current segment (and go to next segment if finish)
                await this.MoveForward(bytesToCopy);

                if (_isEOF) break;
            }

            ENSURE(count == bufferPosition, "current value must fit inside defined buffer");

            return bufferPosition;
        }

        /// <summary>
        /// Write bytes from buffer into segmentsr. Return how many bytes was write
        /// </summary>
        public async Task<int> Write(byte[] buffer) => await this.Write(buffer, 0, buffer.Length);

        /// <summary>
        /// Skip bytes (same as Write but with no array copy)
        /// </summary>
        public async Task<int> Skip(int count) => await this.Write(null, 0, count);

        /// <summary>
        /// Consume all data source until finish
        /// </summary>
        public async Task Consume()
        {
            if(_source != null)
            {
                while (await _source.MoveNextAsync())
                {
                }
            }
        }

        #endregion

        #region String

        /// <summary>
        /// Write String with \0 at end
        /// </summary>
        public async Task WriteCString(string value)
        {
            if (value.IndexOf('\0') > -1) throw LiteException.InvalidNullCharInString();

            var bytesCount = Encoding.UTF8.GetByteCount(value);
            var available = _current.Count - _currentPosition; // avaiable in current segment

            // can write direct in current segment (use < because need +1 \0)
            if (bytesCount < available)
            {
                Encoding.UTF8.GetBytes(value, 0, value.Length, _current.Array, _current.Offset + _currentPosition);

                _current[_currentPosition + bytesCount] = 0x00;

                await this.MoveForward(bytesCount + 1); // +1 to '\0'
            }
            else
            {
                var buffer = BufferPool.Rent(bytesCount);

                Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, 0);

                await this.Write(buffer, 0, bytesCount);

                _current[_currentPosition] = 0x00;

                await this.MoveForward(1);

                BufferPool.Return(buffer);
            }
        }

        /// <summary>
        /// Write string into output buffer. 
        /// Support direct string (with no length information) or BSON specs: with (legnth + 1) [4 bytes] before and '\0' at end = 5 extra bytes
        /// </summary>
        public async Task WriteString(string value, bool specs)
        {
            var count = Encoding.UTF8.GetByteCount(value);

            if (specs)
            {
                await this.Write(count + 1); // write Length + 1 (for \0)
            }

            if (count <= _current.Count - _currentPosition)
            {
                Encoding.UTF8.GetBytes(value, 0, value.Length, _current.Array, _current.Offset + _currentPosition);

                await this.MoveForward(count);
            }
            else
            {
                // rent a buffer to be re-usable
                var buffer = BufferPool.Rent(count);

                Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, 0);

                await this.Write(buffer, 0, count);

                BufferPool.Return(buffer);
            }

            if (specs)
            {
                await this.Write((byte)0x00);
            }
        }

        #endregion

        #region Numbers

        private async Task WriteNumber<T>(T value, Action<T, byte[], int> toBytes, int size)
        {
            if (_currentPosition + size <= _current.Count)
            {
                toBytes(value, _current.Array, _current.Offset + _currentPosition);

                await this.MoveForward(size);
            }
            else
            {
                var buffer = BufferPool.Rent(size);

                toBytes(value, buffer, 0);

                await this.Write(buffer, 0, size);

                BufferPool.Return(buffer);
            }
        }

        public async Task Write(Int32 value) => await this.WriteNumber(value, BufferExtensions.ToBytes, 4);
        public async Task Write(Int64 value) => await this.WriteNumber(value, BufferExtensions.ToBytes, 8);
        public async Task Write(UInt32 value) => await this.WriteNumber(value, BufferExtensions.ToBytes, 4);
        public async Task Write(Double value) => await this.WriteNumber(value, BufferExtensions.ToBytes, 8);

        public async Task Write(Decimal value)
        {
            var bits = Decimal.GetBits(value);
            await this.Write(bits[0]);
            await this.Write(bits[1]);
            await this.Write(bits[2]);
            await this.Write(bits[3]);
        }

        #endregion

        #region Complex Types

        /// <summary>
        /// Write DateTime as UTC ticks (not BSON format)
        /// </summary>
        public async Task Write(DateTime value)
        {
            var utc = (value == DateTime.MinValue || value == DateTime.MaxValue) ? value : value.ToUniversalTime();

            await this.Write(utc.Ticks);
        }

        /// <summary>
        /// Write Guid as 16 bytes array
        /// </summary>
        public async Task Write(Guid value)
        {
            // there is no avaiable value.TryWriteBytes (TODO: implement conditional compile)?
            var bytes = value.ToByteArray();

            await this.Write(bytes, 0, 16);
        }

        /// <summary>
        /// Write ObjectId as 12 bytes array
        /// </summary>
        public async Task Write(ObjectId value)
        {
            if (_currentPosition + 12 <= _current.Count)
            {
                value.ToByteArray(_current.Array, _current.Offset + _currentPosition);

                await this.MoveForward(12);
            }
            else
            {
                var buffer = BufferPool.Rent(12);

                value.ToByteArray(buffer, 0);

                await this.Write(buffer, 0, 12);

                BufferPool.Return(buffer);
            }
        }

        /// <summary>
        /// Write a boolean as 1 byte (0 or 1)
        /// </summary>
        public async Task Write(bool value)
        {
            _current[_currentPosition] = value ? (byte)0x01 : (byte)0x00;
            await this.MoveForward(1);
        }

        /// <summary>
        /// Write single byte
        /// </summary>
        public async Task Write(byte value)
        {
            _current[_currentPosition] = value;
            await this.MoveForward(1);
        }

        /// <summary>
        /// Write PageAddress as PageID, Index
        /// </summary>
        internal async Task Write(PageAddress address)
        {
            await this.Write(address.PageID);
            await this.Write(address.Index);
        }

        #endregion

        #region BsonDocument as SPECS

        /// <summary>
        /// Write BsonArray as BSON specs. Returns array bytes count
        /// </summary>
        public async Task<int> WriteArray(BsonArray value, bool recalc)
        {
            var bytesCount = value.GetBytesCount(recalc);

            await this.Write(bytesCount);

            for (var i = 0; i < value.Count; i++)
            {
                await this.WriteElement(i.ToString(), value[i]);
            }

            await this.Write((byte)0x00);

            return bytesCount;
        }

        /// <summary>
        /// Write BsonDocument as BSON specs. Returns document bytes count
        /// </summary>
        public async Task<int> WriteDocument(BsonDocument value, bool recalc)
        {
            var bytesCount = value.GetBytesCount(recalc);

            await this.Write(bytesCount);

            foreach (var el in value.GetElements())
            {
                await this.WriteElement(el.Key, el.Value);
            }

            await this.Write((byte)0x00);

            return bytesCount;
        }

        private async Task WriteElement(string key, BsonValue value)
        {
            // cast RawValue to avoid one if on As<Type>
            switch (value.Type)
            {
                case BsonType.Double:
                    await this.Write((byte)0x01);
                    await this.WriteCString(key);
                    await this.Write(value.AsDouble);
                    break;

                case BsonType.String:
                    await this.Write((byte)0x02);
                    await this.WriteCString(key);
                    await this.WriteString(value.AsString, true); // true = BSON Specs (add LENGTH at begin + \0 at end)
                    break;

                case BsonType.Document:
                    await this.Write((byte)0x03);
                    await this.WriteCString(key);
                    await this.WriteDocument(value.AsDocument, false);
                    break;

                case BsonType.Array:
                    await this.Write((byte)0x04);
                    await this.WriteCString(key);
                    await this.WriteArray(value.AsArray, false);
                    break;

                case BsonType.Binary:
                    await this.Write((byte)0x05);
                    await this.WriteCString(key);
                    var bytes = value.AsBinary;
                    await this.Write(bytes.Length);
                    await this.Write((byte)0x00); // subtype 00 - Generic binary subtype
                    await this.Write(bytes, 0, bytes.Length);
                    break;

                case BsonType.Guid:
                    await this.Write((byte)0x05);
                    await this.WriteCString(key);
                    var guid = value.AsGuid;
                    await this.Write(16);
                    await this.Write((byte)0x04); // UUID
                    await this.Write(guid);
                    break;

                case BsonType.ObjectId:
                    await this.Write((byte)0x07);
                    await this.WriteCString(key);
                    await this.Write(value.AsObjectId);
                    break;

                case BsonType.Boolean:
                    await this.Write((byte)0x08);
                    await this.WriteCString(key);
                    await this.Write((byte)(value.AsBoolean ? 0x01 : 0x00));
                    break;

                case BsonType.DateTime:
                    await this.Write((byte)0x09);
                    await this.WriteCString(key);
                    var date = value.AsDateTime;
                    // do not convert to UTC min/max date values - #19
                    var utc = (date == DateTime.MinValue || date == DateTime.MaxValue) ? date : date.ToUniversalTime();
                    var ts = utc - BsonValue.UnixEpoch;
                    await this.Write(Convert.ToInt64(ts.TotalMilliseconds));
                    break;

                case BsonType.Null:
                    await this.Write((byte)0x0A);
                    await this.WriteCString(key);
                    break;

                case BsonType.Int32:
                    await this.Write((byte)0x10);
                    await this.WriteCString(key);
                    await this.Write(value.AsInt32);
                    break;

                case BsonType.Int64:
                    await this.Write((byte)0x12);
                    await this.WriteCString(key);
                    await this.Write(value.AsInt64);
                    break;

                case BsonType.Decimal:
                    await this.Write((byte)0x13);
                    await this.WriteCString(key);
                    await this.Write(value.AsDecimal);
                    break;

                case BsonType.MinValue:
                    await this.Write((byte)0xFF);
                    await this.WriteCString(key);
                    break;

                case BsonType.MaxValue:
                    await this.Write((byte)0x7F);
                    await this.WriteCString(key);
                    break;
            }
        }

        public ValueTask DisposeAsync()
        {
            return _source?.DisposeAsync() ?? default;
        }

        #endregion
    }
}