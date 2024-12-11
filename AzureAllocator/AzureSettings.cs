using Newtonsoft.Json;
using Azure.Core;
using Azure.ResourceManager.Compute.Models;

namespace AzureAllocator;

public class AzureSettings
{
    //make sure this property is changeable
    [JsonProperty(nameof(Region))]
    public string? Region;
    
    [JsonProperty(nameof(ImageReference))]
    public ImageReference? ImageReference { get; private set; }
    
    [JsonProperty(nameof(VmSize))]
    public string? VmSize { get; private set; }


    private AzureSettings(){}

    public static async Task<AzureSettings> CreateAsync(string filepath, string region){
        var settings = await CreateAsync(filepath);
        settings.Region = region;
        return settings;
    }
    public static async Task<AzureSettings> CreateAsync(string filepath) =>
        await Task.Run(async () => JsonConvert.DeserializeObject<AzureSettings>(await File.ReadAllTextAsync(filepath)) ?? new());

}
