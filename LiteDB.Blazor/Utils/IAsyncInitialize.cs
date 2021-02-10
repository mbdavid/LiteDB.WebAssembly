using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using static LiteDB.Constants;

namespace LiteDB
{
    public interface IAsyncInitialize
    {
        Task InitializeAsync();
    }
}