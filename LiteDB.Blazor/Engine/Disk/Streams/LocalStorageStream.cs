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
    public class LocalStorageStream : Stream
    {
        private const int PAD_PAGE = 5;

        private IJSRuntime _runtime;
        private long _position = 0;
        private long _length = 0;

        private string _pageKey => "ldb_page_" + (_position / PAGE_SIZE).ToString().PadLeft(PAD_PAGE, '0');

        private LocalStorageStream(IJSRuntime runtime, long length)
        {
            _runtime = runtime;
            _length = length;
        }

        public static async Task<LocalStorageStream> CreateAsync(IJSRuntime runtime)
        {
            var length = await runtime.InvokeAsync<JsonElement>("localStorage.getItem", "ldb_length");

            var l = 
                length.ValueKind == JsonValueKind.Null ? 0 :
                length.ValueKind == JsonValueKind.String ? Convert.ToInt32(length.GetString()) :
                length.GetInt32();

            return new LocalStorageStream(runtime, l);
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
            var content = await _runtime.InvokeAsync<string>("localStorage.getItem", _pageKey);

            // there is no method to read base64 into buffer array directly???
            var data = Convert.FromBase64String(content);

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
            _length = value;

            // run async
            Task.Run(async () => await _runtime.InvokeAsync<object>("localStorage.setItem", "ldb_length", _length));
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this.WriteAsync(buffer, offset, count).Wait();
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var content = Convert.ToBase64String(buffer, offset, count, Base64FormattingOptions.None);

            await _runtime.InvokeAsync<object>("localStorage.setItem", _pageKey, content);

            _position += count;

            if (_position > this.Length)
            {
                _length = _position;

                await _runtime.InvokeAsync<object>("localStorage.setItem", "ldb_length", _length);
            }
        }
    }
}