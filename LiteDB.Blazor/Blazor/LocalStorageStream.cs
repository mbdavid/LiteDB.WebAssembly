using Microsoft.JSInterop;

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LiteDB
{
    internal class LocalStorageStream : Stream
    {
        private IJSRuntime _runtime;
        private long _position = 0;
        private long _length = 0;

        public LocalStorageStream(IJSRuntime runtime, long length)
        {
            _runtime = runtime;
            _length = length;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => true;
        public override void Flush() { }

        public override long Length => _length;

        public override long Position { get => _position; set => _position = value; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var content = _runtime.InvokeAsync<string>("localStorageDb.readBytes", this.Position).Result;

            // there is no method to read base64 into buffer array directly???
            var data = Convert.FromBase64String(content);

            System.Buffer.BlockCopy(data, 0, buffer, offset, count);

            return data.Length;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            lock (_runtime)
            {
                var position =
                    origin == SeekOrigin.Begin ? offset :
                    origin == SeekOrigin.Current ? _position + offset :
                    _position - offset;

                _position = position;

                return _position;
            }
        }

        public override void SetLength(long value)
        {
            _length = value;

            var _ = _runtime.InvokeAsync<object>("localStorageDb.setLength", _length).Result;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var content = Convert.ToBase64String(buffer, offset, count, Base64FormattingOptions.None);

            var _ = _runtime.InvokeAsync<object>("localStorageDb.writeBytes", _position, content).Result;

            _position += count;

            if (_position > this.Length)
            {
                _length = _position;
            }
        }
    }
}