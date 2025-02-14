namespace AzureAllocator.Types;

public class AzureVmSize(string azureVmName, string region)
{
    public string AzureVmName { get; } = azureVmName;
    public string Region { get; } = region;
}
