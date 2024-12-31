using Azure;
using Azure.Data.Tables;


namespace AzureAllocator;

public static class TableManager
{
    public async static Task<TableClient> GetTableClient(string table){
        string connectionString = Environment.GetEnvironmentVariable("MOONFIRE_STORAGE_STRING") ?? "";
        var client = new TableClient(connectionString, table);
        await client.CreateIfNotExistsAsync();
        return client;
    }
    
    public static async Task<TableEntity?> GetTableEntity(TableClient client, string key, string row){
        try{
            return (await client.GetEntityIfExistsAsync<TableEntity>(key,row)).Value;
        }catch{
            return null;
        }
    }
    
    public static async Task<TableEntity?> GetTableEntity(string client, string key, string row){
        try{
            return await GetTableEntity(await GetTableClient(client),key,row);
        }catch{
            return null;
        }
    }
    
    public static async Task<object?> GetTableEntity(TableClient client, string key, string row, string column){
        try{
            var value = (await GetTableEntity(client,key,row))?[column];
            return value;
        }catch{
            return null;
        }
    }
    
    public static async Task<object?> GetTableEntity(string client, string key, string row, string column){
        try{
            var value = await GetTableEntity(await GetTableClient(client),key,row,column);
            return value;
        }catch{
            return null;
        }
    }
    
    public static async Task<bool> GetBoolDefaultFalse(TableClient client, string key, string row, string column){
        try{
            var value = await GetTableEntity(client,key,row,column);
            return (bool?)value ?? false;
        }catch{
            return false;
        }
    }
    
    public static async Task<bool> GetBoolDefaultFalse(string client, string key, string row, string column){
        try{
            return await GetBoolDefaultFalse(await GetTableClient(client),key,row,column);
        }catch{
            return false;
        }
    }

    public static async Task StoreTableEntity(TableClient client, TableEntity entity){
        var _old = await GetTableEntity(client,entity.PartitionKey,entity.RowKey);
        if(_old!=null) await UpdateTableEntity(client,entity);
        else await AddTableEntity(client,entity);
    }

    public static async Task StoreTableEntity(TableClient client, string key, string row, string column, object value){
        await StoreTableEntity(client,new(key,row){{column,value}});
    }

    public static async Task StoreTableEntity(string client, string key, string row, string column, object value){
        await StoreTableEntity(await GetTableClient(client),key,row,column,value);
    }
    
    public static async Task DeleteTableEntity(TableClient client, string key, string row) =>
        await client.DeleteEntityAsync(key,row);

    private static async Task AddTableEntity(TableClient client, TableEntity entity) =>
        await client.AddEntityAsync(entity);
    
    private static async Task UpdateTableEntity(TableClient client, TableEntity entity) =>
        await client.UpdateEntityAsync(entity,ETag.All,TableUpdateMode.Merge);
}
