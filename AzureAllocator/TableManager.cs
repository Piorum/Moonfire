using System.Collections.Concurrent;
using Azure;
using Azure.Data.Tables;

namespace AzureAllocator;

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
    
    public static async Task<TableEntity?> GetTableEntity(TableClient client, string key, string row, CancellationToken token = default){
        try{
            return (await client.GetEntityIfExistsAsync<TableEntity>(key,row,cancellationToken:token)).Value;
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
    
    public static async Task<object?> GetTableEntity(TableClient client, string key, string row, string column, CancellationToken token = default){
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
    
    public static async Task<bool> GetBoolDefaultFalse(TableClient client, string key, string row, string column, CancellationToken token = default){
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

    public static async Task StoreTableEntity(TableClient client, TableEntity entity, CancellationToken token = default){
        var _old = await GetTableEntity(client,entity.PartitionKey,entity.RowKey,token);
        if(_old!=null) await UpdateTableEntity(client,entity,token);
        else await AddTableEntity(client,entity,token);
    }

    public static async Task StoreTableEntity(TableClient client, string key, string row, string column, object value, CancellationToken token = default){
        await StoreTableEntity(client,new(key,row){{column,value}},token);
    }

    public static async Task StoreTableEntity(string client, string key, string row, string column, object value, CancellationToken token = default){
        await StoreTableEntity(await GetTableClient(client,token),key,row,column,value,token);
    }
    
    public static async Task DeleteTableEntity(TableClient client, string key, string row, CancellationToken token = default) =>
        await client.DeleteEntityAsync(key,row,cancellationToken:token);

    private static async Task AddTableEntity(TableClient client, TableEntity entity, CancellationToken token = default) =>
        await client.AddEntityAsync(entity,cancellationToken:token);
    
    private static async Task UpdateTableEntity(TableClient client, TableEntity entity, CancellationToken token = default) =>
        await client.UpdateEntityAsync(entity,ETag.All,TableUpdateMode.Merge,cancellationToken:token);
}
