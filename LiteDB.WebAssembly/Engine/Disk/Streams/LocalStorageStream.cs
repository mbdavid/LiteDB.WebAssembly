using Microsoft.JSInterop;

using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using static LiteDB.Constants;

namespace LiteDB.Engine
{
    public class LocalStorageStream : Stream, IAsyncInitialize
    {
        private const string LENGTH_KEY = "ldb_length";
        private const string PAGE_KEY = "ldb_page_{0:00000}";

        private readonly IJSRuntime _runtime;
        private long _position = 0;
        private long _length = 0;

        private string GetKey(long? p = null) => string.Format(PAGE_KEY, (p ?? _position) / PAGE_SIZE);

        public LocalStorageStream(IJSRuntime runtime)
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
        public override void Flush() { }

        public override long Length => _length;

        public override long Position { get => _position; set => _position = value; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return this.ReadAsync(buffer, offset, count).Result;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var content = await _runtime.InvokeAsync<JsonElement>("localStorage.getItem", this.GetKey());

            _position += count;

            if (content.ValueKind == JsonValueKind.Null)
            {
                // read empty (not created) page
                for(var i = offset; i < offset + count; i++)
                {
                    buffer[i] = 0;
                }
                
                return count;
            }

            // there is no method to read base64 into buffer array directly???
            var data = Convert.FromBase64String(content.GetString());

            System.Buffer.BlockCopy(data, 0, buffer, offset, count);

            return data.Length;
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
            var content = Convert.ToBase64String(buffer, offset, count, Base64FormattingOptions.None);

            await _runtime.InvokeAsync<object>("localStorage.setItem", this.GetKey(), content);

            _position += count;

            if (_position > this.Length)
            {
                _length = _position;

                await _runtime.InvokeAsync<object>("localStorage.setItem", LENGTH_KEY, _length);
            }
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