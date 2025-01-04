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

    // Parameterless constructor
    public ServerEntity() { }

    public ServerEntity(string guildId, string serverType, bool isRunning)
    {
        PartitionKey = guildId;
        RowKey = serverType;
        IsRunning = isRunning;
    }
}
