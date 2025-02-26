using Azure;
using Azure.Data.Tables;

namespace Moonfire.TableEntities;

public class CreditEntity : ITableEntity
{
    public string? PartitionKey { get; set; } //AccountKey
    public string? RowKey { get; set; } = "0"; 
    public ETag ETag { get; set; }
    public DateTimeOffset? Timestamp { get; set; }

    // Additional properties
    public double Credit { get; set; }

    // Parameterless constructor
    public CreditEntity() { }

    public CreditEntity(string accountKey, double credit)
    {
        PartitionKey = accountKey;
        Credit = credit;
    }
}
