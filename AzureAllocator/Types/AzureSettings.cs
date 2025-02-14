using Newtonsoft.Json;
using Azure.ResourceManager.Compute.Models;

namespace AzureAllocator.Types;

public class AzureSettings{
    //make sure this property is changeable
    //all tagged due to nullable and private setters
    [JsonProperty(nameof(Region))]
    public string? Region;
    
    [JsonProperty(nameof(ImageReference))]
    public ImageReference? ImageReference { get; private set; }
    
    [JsonProperty(nameof(VmSize))]
    public InternalVmSize? VmSize { get; private set; }
    
    [JsonProperty(nameof(RequestedPrice))]
    public int? RequestedPrice { get; private set; }

    [JsonProperty(nameof(SecurityRules))]
    public List<SecurityRuleSettings>? SecurityRules { get; private set; }

    [JsonProperty(nameof(DataDisks))]
    public List<DataDiskSettings>? DataDisks { get; private set; }


    private AzureSettings(){}

    //rawJson string
    public static async Task<AzureSettings> CreateAsync(string jsonString) =>
        await Task.Run(() => JsonConvert.DeserializeObject<AzureSettings>(jsonString) ?? new());

    public Task SetVmSize(InternalVmSize vmSize){
        VmSize = vmSize;
        return Task.CompletedTask;
    }

    public Task<string> GetAzureRegion(){
        return 
            Task.FromResult
            (
                Region switch 
                {
                    "NA" => "centralus",
                    _ => null
                } 
                ?? "centralus" //default to 'centralus'
            );
    }

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