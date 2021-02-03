using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using static LiteDB.Constants;

namespace LiteDB.Engine
{
    /// <summary>
    /// All engine settings used to starts new engine
    /// </summary>
    public class EngineSettings
    {
        /// <summary>
        /// Get/Set custom stream to be used as datafile (can be MemoryStream or TempStream). Do not use FileStream - to use physical file, use "filename" attribute (and keep DataStream/WalStream null)
        /// </summary>
        public Stream DataStream { get; set; } = null;

        /// <summary>
        /// Create database with custom string collection (used only to create database) (default: Collation.Default)
        /// </summary>
        public Collation Collation { get; set; }
    }
}