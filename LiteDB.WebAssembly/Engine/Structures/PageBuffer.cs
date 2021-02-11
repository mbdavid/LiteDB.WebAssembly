using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using static LiteDB.Constants;

namespace LiteDB.Engine
{
    /// <summary>
    /// Represent page buffer to be read/write using FileMemory
    /// </summary>
    internal class PageBuffer : BufferSlice
    {
        /// <summary>
        /// Get, on initialize, a unique ID in all database instance for this PageBufer. Is a simple global incremented counter
        /// </summary>
        public readonly int UniqueID;

        /// <summary>
        /// Get/Set page position. If page are writable, this postion CAN be MaxValue (has not defined position yet)
        /// </summary>
        public long Position;

        /// <summary>
        /// Get/Set timestamp from last request
        /// </summary>
        public long Timestamp;

        /// <summary>
        /// Get/Set if page is writable
        /// </summary>
        public bool IsWritable;

        public PageBuffer(byte[] buffer, int offset, int uniqueID)
            : base(buffer, offset, PAGE_SIZE)
        {
            this.UniqueID = uniqueID;
            this.Position = long.MaxValue;
            this.Timestamp = 0;
            this.IsWritable = false;
        }


#if DEBUG
        ~PageBuffer()
        {
            ENSURE(this.IsWritable == false, $"page must be writable = false before destroy");
        }
#endif

        public override string ToString()
        {
            var p = this.Position == long.MaxValue ? "<empty>" : this.Position.ToString();
            var w = this.IsWritable ? "W" : "R";
            var pageID = this.ReadUInt32(0);
            var pageType = this[4];

            return $"ID: {this.UniqueID} - Position: {p} {w} - ({base.ToString()}) :: Content: [{pageID.ToString("0:0000")}/{(PageType)pageType}]";
        }
    }
}