using Newtonsoft.Json;
using Azure.ResourceManager.Compute.Models;

namespace AzureAllocator;

public class AzureSettings{
    //make sure this property is changeable
    //all tagged due to nullable and private setters
    [JsonProperty(nameof(Region))]
    public string? Region;
    
    [JsonProperty(nameof(ImageReference))]
    public ImageReference? ImageReference { get; private set; }
    
    [JsonProperty(nameof(VmSize))]
    public string? VmSize { get; private set; }

    [JsonProperty(nameof(SecurityRules))]
    public List<SecurityRuleSettings>? SecurityRules { get; private set; }

    [JsonProperty(nameof(DataDisks))]
    public List<DataDiskSettings>? DataDisks { get; private set; }


    private AzureSettings(){}

    //rawJson string
    public static async Task<AzureSettings> CreateAsync(string jsonString) =>
        await Task.Run(() => JsonConvert.DeserializeObject<AzureSettings>(jsonString) ?? new());

    public async Task<bool> SetVmSize(string vmSize){
        var valid = await ValidVmName(vmSize);

        if(valid) VmSize = vmSize;

        return valid;
    }

    private static Task<bool> ValidVmName(string vmSize) => (vmSize) switch { 
        "Standard_B1s" => Task.FromResult(true),
        "Standard_B2s" => Task.FromResult(true),
        "Standard_B2ms" => Task.FromResult(true),
        "Standard_B4als_v2" => Task.FromResult(true),

        _ => Task.FromResult(false)
    };

}

public class SecurityRuleSettings{
    public string? Name { get; set; }
    public int? Priority { get; set; }
    public string? Access { get; set; }
    public string? Direction { get; set; }
    public string? Protocol { get; set; }
    public string? SourcePortRange { get; set; }
    public string? DestinationPortRange { get; set; }
    public string? SourceAddressPrefix { get; set; }
    public string? DestinationAddressPrefix { get; set; }
}

public class DataDiskSettings{
    public int? Size { get; set; }
    public string? Type { get; set; }
}