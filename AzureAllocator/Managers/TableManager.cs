using System.Collections.Concurrent;
using System.Linq.Expressions;
using Azure;
using Azure.Data.Tables;

namespace AzureAllocator.Managers;

public static class TableManager
{
    private static readonly ConcurrentDictionary<string, TableClient> tableClients = new();

    private async static Task<TableClient> CreateTableClient(string tableName, CancellationToken token = default){

        string connectionString = Environment.GetEnvironmentVariable("MOONFIRE_STORAGE_STRING") ?? "";
        var client = new TableClient(connectionString, tableName);

        //create table if doesn't exist
        await client.CreateIfNotExistsAsync(cancellationToken:token);

        //store in dictionary
        tableClients.TryAdd(tableName,client);


        return client;
    }

    private async static Task<TableClient> GetTableClient(string tableName, CancellationToken token = default) =>
        tableClients.TryGetValue(tableName,out var client) ? client : await CreateTableClient(tableName,token);
    
    public static async Task<List<T>> QueryTableAsync<T>(string tableName, Expression<Func<T, bool>>? filter = default) 
        where T : class, ITableEntity, new()
    {
        var queryResults = (await GetTableClient(tableName)).QueryAsync(filter).AsPages();

        var results = new List<T>();

        await foreach(var page in queryResults)
            results.AddRange(page.Values);

        return results;
    }

    private static async Task<T?> GetITableEntity<T>(TableClient client, string key, string row,  CancellationToken token = default)
        where T : class, ITableEntity, new()
    {
        try{
            var entity = await client.GetEntityIfExistsAsync<T>(key,row,cancellationToken:token);
            return entity.HasValue ? entity.Value : null;
        }catch{
            return null;
        }
    }

    public static async Task<T?> GetITableEntity<T>(string client, string key, string row,  CancellationToken token = default)
        where T : class, ITableEntity, new() =>
        await GetITableEntity<T>(await GetTableClient(client,token),key,row,token);

    private static async Task<TableEntity?> GetTableEntity(TableClient client, string key, string row, CancellationToken token = default){
        try{
            var entity = await client.GetEntityIfExistsAsync<TableEntity>(key,row,cancellationToken:token);
            return entity.HasValue ? entity.Value : null;
        }catch{
            return null;
        }
    }
    
    public static async Task<TableEntity?> GetTableEntity(string client, string key, string row, CancellationToken token = default){
        try{
            return await GetTableEntity(await GetTableClient(client,token),key,row,token);
        }catch{
            return null;
        }
    }
    
    private static async Task<object?> GetTableEntity(TableClient client, string key, string row, string column, CancellationToken token = default){
        try{
            var value = (await GetTableEntity(client,key,row,token))?[column];
            return value;
        }catch{
            return null;
        }
    }
    
    public static async Task<object?> GetTableEntity(string client, string key, string row, string column, CancellationToken token = default){
        try{
            var value = await GetTableEntity(await GetTableClient(client,token),key,row,column,token);
            return value;
        }catch{
            return null;
        }
    }
    
    private static async Task<bool> GetBoolDefaultFalse(TableClient client, string key, string row, string column, CancellationToken token = default){
        try{
            var value = await GetTableEntity(client,key,row,column,token);
            return (bool?)value ?? false;
        }catch{
            return false;
        }
    }
    
    public static async Task<bool> GetBoolDefaultFalse(string client, string key, string row, string column, CancellationToken token = default){
        try{
            return await GetBoolDefaultFalse(await GetTableClient(client,token),key,row,column,token);
        }catch{
            return false;
        }
    }

    private static async Task StoreITableEntity(TableClient client, ITableEntity entity, CancellationToken token = default){
        var _old = await GetTableEntity(client,entity.PartitionKey,entity.RowKey,token);
        if(_old!=null) await UpdateITableEntity(client,entity,token);
        else await AddITableEntity(client,entity,token);
    }

    public static async Task StoreITableEntity(string client, ITableEntity entity, CancellationToken token = default){
        await StoreITableEntity(await GetTableClient(client,token),entity,token);
    }

    private static async Task StoreTableEntity(TableClient client, string key, string row, string column, object value, CancellationToken token = default){
        await StoreITableEntity(client,new TableEntity(key,row){{column,value}},token);
    }

    public static async Task StoreTableEntity(string client, string key, string row, string column, object value, CancellationToken token = default){
        await StoreTableEntity(await GetTableClient(client,token),key,row,column,value,token);
    }
    
    private static async Task DeleteTableEntity(TableClient client, string key, string row, CancellationToken token = default) =>
        await client.DeleteEntityAsync(key,row,cancellationToken:token);
    
    public static async Task DeleteTableEntity(string tableName, string key, string row, CancellationToken token = default) =>
        await DeleteTableEntity(await GetTableClient(tableName,token),key,row,token);

    private static async Task AddITableEntity(TableClient client, ITableEntity entity, CancellationToken token = default) =>
        await client.AddEntityAsync(entity,cancellationToken:token);
    
    private static async Task UpdateITableEntity(TableClient client, ITableEntity entity, CancellationToken token = default) =>
        await client.UpdateEntityAsync(entity,ETag.All,TableUpdateMode.Merge,cancellationToken:token);
}
