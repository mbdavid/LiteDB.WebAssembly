# LiteDB for Blazor - WebAssembly

This repository contains some cool tests about LiteDB and ASP.NET Blazor in client side (WebAssembly). This is not a final product! (yet :smile:)

# How will works

LiteDB.WebAssembly forks LiteDB v5.1 branch with master updates to start a smaller and focused version for wasm. Some LiteDB was removed in this version (maybe can back in future). 

Blazor runs in browser using WebAssembly. There is no disk access in browsers so all your data will be sotored into `IndexedDB` and/or `LocalStorage`. Works with a single database per domain and async read/write operations only. All locks will be removed and there is no support for concurrency or transactions (exclusive lock only).

All methods are converted into async and has an `Async` sufix. Also, this version needs to run `await OpenAsync()` before use database.

#### Removed features
- Encryption
- Transactions
- Shared Connection
- FileStorage
- SortDisk

# How to use

Register `ILiteDatabase` service with a custom `Stream`. You can use `MemoryStream`, `LocalStorageStream` or `IndexedDBStream` (not yet).

```C#
// in memory database
builder.Services.AddScoped<ILiteDatabase>(sp => new LiteDatabase(new MemoryStream()));

// store data in local storage
builder.Services.AddScoped<ILiteDatabase>(sp => new LiteDatabase(new LocalStorageStream()));
```

You must call open database before use

```C#
@page "/db-demo"
@inject ILiteDatabase _db;

    private ILiteCollection<Person> _personCollection;

    protected override async Task OnInitializedAsync()
    {
        await _db.OpenAsync();
        
        _personCollection = _db.GetCollection<Person>();
    }
```