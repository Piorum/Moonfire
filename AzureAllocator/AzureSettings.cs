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

    public static async Task<AzureSettings> CreateAsync(string filepath, string region){
        var settings = await CreateAsync(filepath);
        settings.Region = region;
        return settings;
    }

    //Json filepath
    public static async Task<AzureSettings> CreateAsync(string jsonPath) =>
        await Task.Run(async () => JsonConvert.DeserializeObject<AzureSettings>(await File.ReadAllTextAsync(jsonPath)) ?? new());

    //rawJson string
    public static async Task<AzureSettings> CreateAsync(string jsonRaw, bool _) =>
        await Task.Run(() => JsonConvert.DeserializeObject<AzureSettings>(jsonRaw) ?? new());

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