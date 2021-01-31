# LiteDB for Blazor - WebAssembly

This repository contains some cool tests about LiteDB and ASP.NET Blazor in client side (WebAssembly). This is not a final product! (yet :smile:)

# How will works

LiteDB for Blazor will fork v5.1 branch with master updates to start a smaller and focused version for Blazor.  Some LiteDB features will be removed in Blazor version (maybe can back in future). 

Blazor runs in browser using WebAssembly. There is no disk access in browsers so all your data will be sotored into `IndexedDB` and/or `LocalStorage`. Will works with a single database per domain and async read/write operations only. All locks will be removed and there is no support for concurrency (single thread only).

All methods will be converted into Async and will be added a "Async" sufix

#### Removed features
- Encryption
- Transactions?
- Concurrency
- Shared Connection
- FileStorage
- MemoryCache
