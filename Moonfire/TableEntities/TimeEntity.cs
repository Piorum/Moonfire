using Azure;
using Azure.Data.Tables;

namespace Moonfire.TableEntities;

public class TimeEntity : ITableEntity
{
    public string? PartitionKey { get; set; }
    public string? RowKey { get; set; }
    public ETag ETag { get; set; }
    public DateTimeOffset? Timestamp { get; set; }

    // Additional properties
    public DateTime Time { get; set; } = DateTime.Now;

    // Parameterless constructor
    public TimeEntity() { }

    public TimeEntity(string partitionKey, string rowKey, DateTime time)
    {
        PartitionKey = partitionKey;
        RowKey = rowKey;
        Time = time;
    }
}
