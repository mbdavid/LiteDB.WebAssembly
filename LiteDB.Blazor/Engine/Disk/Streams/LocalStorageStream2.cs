using Microsoft.JSInterop;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using static LiteDB.Constants;

namespace LiteDB.Engine
{
    public class LocalStorageStream2 : Stream, IAsyncInitialize
    {
        private const string LENGTH_KEY = "ldb_length";
        private const string PAGE_KEY = "ldb_page_{0:00000}";

        private readonly IJSRuntime _runtime;
        private long _position = 0;
        private long _length = 0;

        private readonly Dictionary<long, byte[]> _cache = new Dictionary<long, byte[]>();
        private readonly HashSet<long> _dirty = new HashSet<long>();

        private string GetKey(long? p = null) => string.Format(PAGE_KEY, (p ?? _position) / PAGE_SIZE);

        public LocalStorageStream2(IJSRuntime runtime)
        {
            _runtime = runtime;
        }

        public async Task InitializeAsync()
        {
            var length = await _runtime.InvokeAsync<JsonElement>("localStorage.getItem", LENGTH_KEY);

            _length = 
                length.ValueKind == JsonValueKind.Null ? 0 :
                length.ValueKind == JsonValueKind.String ? Convert.ToInt32(length.GetString()) :
                length.GetInt32();
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => true;

        public override long Length => _length;

        public override long Position { get => _position; set => _position = value; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return this.ReadAsync(buffer, offset, count).Result;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_cache.TryGetValue(_position, out var pageBytes))
            {
                System.Buffer.BlockCopy(pageBytes, 0, buffer, offset, count);

                _position += count;

                return count;
            }
            else
            {
                var content = await _runtime.InvokeAsync<JsonElement>("localStorage.getItem", this.GetKey());

                if (content.ValueKind == JsonValueKind.Null)
                {
                    // read empty (not created) page
                    for (var i = offset; i < offset + count; i++)
                    {
                        buffer[i] = 0;
                    }

                    return count;
                }

                // there is no method to read base64 into buffer array directly???
                var data = Convert.FromBase64String(content.GetString());

                _cache[_position] = data;

                System.Buffer.BlockCopy(data, 0, buffer, offset, count);

                _position += count;

                return data.Length;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            var position =
                origin == SeekOrigin.Begin ? offset :
                origin == SeekOrigin.Current ? _position + offset :
                _position - offset;

            _position = position;

            return _position;
        }

        public override void SetLength(long value)
        {
            var current = _length;

            _length = value;

            // run async
            Task.Run(async () =>
            {
                for (var i = value; i < current; i += PAGE_SIZE)
                {
                    await _runtime.InvokeAsync<object>("localStorage.removeItem", this.GetKey(i));
                }

                await _runtime.InvokeAsync<object>("localStorage.setItem", LENGTH_KEY, _length);
            });
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this.WriteAsync(buffer, offset, count).Wait();
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var data = new byte[count];

            Buffer.BlockCopy(buffer, offset, data, 0, count);

            _cache[_position] = data;

            _dirty.Add(_position);

            _position += count;
        }

        public override void Flush() => this.FlushAsync().Wait();

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            await _runtime.InvokeAsync<object>("localStorageStream.flush", new
            {
                pages = _dirty.Select(p => new
                {
                    position = p,
                    content = Convert.ToBase64String(_cache[p], 0, 8192, Base64FormattingOptions.None)
                }).ToDictionary(x => x.position, x => x.content),
                length = _length
            }); ;

            _dirty.Clear();
        }


        /// <summary>
        /// Clear all content from LocalStorage
        /// </summary>
        public async Task Clear()
        {
            for(var i = 0; i < _length; i+= PAGE_SIZE)
            {
                await _runtime.InvokeAsync<object>("localStorage.removeItem", this.GetKey(i));
            }

            await _runtime.InvokeAsync<object>("localStorage.removeItem", LENGTH_KEY);
        }
    }
}