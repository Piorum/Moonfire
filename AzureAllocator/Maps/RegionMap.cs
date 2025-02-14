namespace AzureAllocator.Maps;

public static class RegionMap
{
    private static readonly Dictionary<string, string> regionCodes =
        new(StringComparer.OrdinalIgnoreCase)
        {   
            { "australiacentral", "035" },
            { "australiaeast", "005" },
            { "australiasoutheast", "036" },
            { "brazilsouth", "027" },
            { "canadacentral", "017" },
            { "canadaeast", "042" },
            { "centralindia", "013" },
            { "centralus", "011" },
            { "eastasia", "014" },
            { "eastus", "000" },
            { "eastus2", "001" },
            { "francecentral", "018" },
            { "germanywestcentral", "019" },
            { "israelcentral", "028" },
            { "italynorth", "020" },
            { "japaneast", "015" },
            { "japanwest", "032" },
            { "jioindiawest", "033" },
            { "koreacentral", "016" },
            { "koreasouth", "038" },
            { "mexicocentral", "025" },
            { "newzealandnorth", "037" },
            { "northcentralus", "030" },
            { "northeurope", "007" },
            { "norwayeast", "021" },
            { "polandcentral", "022" },
            { "qatarcentral", "029" },
            { "southafricanorth", "012" },
            { "southcentralus", "002" },
            { "southindia", "039" },
            { "southeastasia", "006" },
            { "spaincentral", "023" },
            { "swedencentral", "008" },
            { "switzerlandnorth", "024" },
            { "uaenorth", "026" },
            { "uksouth", "009" },
            { "ukwest", "041" },
            { "westcentralus", "034" },
            { "westeurope", "010" },
            { "westindia", "040" },
            { "westus", "031" },
            { "westus2", "003" },
            { "westus3", "004" },

            //all codes must be unique
            //0..42 taken
            
        };

    public static Task<string> GetRegionCode(string region){
        if(regionCodes.TryGetValue(region, out var code)){
            return Task.FromResult(code);
        } else {
            return Task.FromResult("");
        }
    }
}
