using Azure;
using Azure.Data.Tables;

namespace Moonfire.TableEntities;

public class ServerEntity : ITableEntity
{
    public string? PartitionKey { get; set; } // GuildId
    public string? RowKey { get; set; }       // ServerType
    public ETag ETag { get; set; }
    public DateTimeOffset? Timestamp { get; set; }

    // Additional properties
    public bool IsRunning { get; set; } = false;
    public string? AzureRegion { get; set; } = null;
    public string? AzureVmName { get; set; } = null;
    public string? RgName { get; set; } = null;

    // Parameterless constructor
    public ServerEntity() { }

    public ServerEntity(string guildId, string serverType, bool isRunning, string? azureRegion = null, string? azureVmName = null, string? rgName = null)
    {
        PartitionKey = guildId;
        RowKey = serverType;
        IsRunning = isRunning;
        AzureRegion = azureRegion;
        AzureVmName = azureVmName;
        RgName = rgName;
    }
}
