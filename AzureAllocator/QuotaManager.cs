using Newtonsoft.Json;

namespace AzureAllocator;

public class QuotaManager
{
    private class AzureRegion{
        public string? InternalRegion { get; set; }
        public string? Region { get; set; }
        public int? RegionalQuota { get; set; }
        public List<VmCategory> VmCategories { get; set; } = [];

    }
    private class VmCategory{
        public string? Category { get; set; }
        public int? Quota { get; set; }
    }

    [JsonProperty(nameof(Quotas))]
    private readonly List<AzureRegion> Quotas = [];

    private class CategorySizes{
        public string? CategoryName { get; set; }
        public List<VmSize> AvailableSizes { get; set; } = [];
    }

    [JsonProperty(nameof(AzureVmData))]
    private readonly List<CategorySizes> AzureVmData = [];

    private QuotaManager(){}

    public static async Task<QuotaManager> CreateAsync(string jsonString) =>
        await Task.Run(() => JsonConvert.DeserializeObject<QuotaManager>(jsonString) ?? new());

    public async Task<(AzureVmSize?, VMAvailability)> RequestQuota(AzureSettings settings){
        if(settings.VmSize is null){
            return (null, VMAvailability.INVALID);
        }
        if(settings.Region is null){
            return (null, VMAvailability.INVALID);
        }

        var PreferredRegions = await GetPreferableRegions(settings.Region);
        if(PreferredRegions is null){
            return (null, VMAvailability.INVALID);
        }

        (var vmSize, var region, var availability) = await GetAvaiableVm(settings.VmSize, PreferredRegions);

        if(vmSize is null || region is null){
            return (null, VMAvailability.INVALID);
        }

        return (new(vmSize, region),availability);
    }

    private async Task<(VmCategory,AzureRegion,int)?> GetCategoryRegionAndCpuRequirment(string AzureVmSize, string AzureRegion){
        var regionTask = GetRegion(AzureRegion);
        var categoryNameTask = GetCategoryName(AzureVmSize);
        var vCpuRequirementTask = GetVmSizeRequirement(AzureVmSize);

        await Task.WhenAll(regionTask, categoryNameTask); //vCpuRequirement Task is awaited later

        var region = await regionTask;
        var categoryName = await categoryNameTask;

        if(region is null || categoryName is null){
            await Console.Out.WriteLineAsync($"Failed to get Region/CategoryName:AvmSize:{AzureVmSize}:Aregion:{AzureRegion}");
            await vCpuRequirementTask; //ensure task synchronizes
            return null;
        }

        var categoryTask = GetVmCategory(region, categoryName);

        await Task.WhenAll(categoryTask,vCpuRequirementTask);

        var category = await categoryTask;
        var vCpuRequirement = await vCpuRequirementTask;

        if(category is null || vCpuRequirement is null){
            await Console.Out.WriteLineAsync($"Failed To get Category/CpuRequirment:AvmSize:{AzureVmSize}:Aregion:{AzureRegion}");
            return null;
        }

        return (category,region,(int)vCpuRequirement);

    }

    public async Task ClaimQuota(string AzureVmSize, string AzureRegion){
        var result = await GetCategoryRegionAndCpuRequirment(AzureVmSize, AzureRegion);

        if(result is null){
            return;
        }

        (var category, var region, var vCpuRequirement) = ((VmCategory,AzureRegion,int))result;

        category.Quota -= vCpuRequirement;
        region.RegionalQuota -= vCpuRequirement;

        await Console.Out.WriteLineAsync($"Claimed Quota:AvmSize:{AzureVmSize}:Aregion:{AzureRegion}");
        return;
    }

    public async Task ReleaseQuota(string AzureVmSize, string AzureRegion){
        var result = await GetCategoryRegionAndCpuRequirment(AzureVmSize, AzureRegion);

        if(result is null){
            return;
        }

        (var category, var region, var vCpuRequirement) = ((VmCategory,AzureRegion,int))result;
        
        category.Quota += vCpuRequirement;
        region.RegionalQuota += vCpuRequirement;

        await Console.Out.WriteLineAsync($"Released Quota:AvmSize:{AzureVmSize}:Aregion:{AzureRegion}");
        return;
    }

    private Task<AzureRegion?> GetRegion(string AzureRegion) =>
        Task.FromResult(Quotas.FirstOrDefault(obj => obj.Region == AzureRegion));
    
    private Task<string?> GetCategoryName(string AzureVmSize) =>
        Task.FromResult(AzureVmData.FirstOrDefault(obj => obj.AvailableSizes.Any(size => size.AzureVmSizeName == AzureVmSize))?.CategoryName);

    private static Task<VmCategory?> GetVmCategory(AzureRegion region, string categoryName) =>
        Task.FromResult(region.VmCategories.FirstOrDefault(obj => obj.Category == categoryName));

    private Task<int?> GetVmSizeRequirement(string AzureVmSize) =>
        Task.FromResult
        (
            AzureVmData
                .FirstOrDefault(obj => obj.AvailableSizes.Any(size => size.AzureVmSizeName == AzureVmSize)) //VmCategory that contains AzureVmSize
                ?.AvailableSizes.FirstOrDefault(obj => obj.AzureVmSizeName == AzureVmSize) //VmSize with name that matches AzureVmSize
                ?.VCpuCount //VCpu Requirment Associated with AzureVmSize
        );

    private Task<List<AzureRegion>?> GetPreferableRegions(string region) =>
        Task.FromResult(Quotas.Where(obj => obj.InternalRegion == region)?.ToList());

    private async Task<(string?,string?,VMAvailability)> GetAvaiableVm(VmSize vmSize, List<AzureRegion> PreferredRegions){
        string? region;
        string? azureVmSizeName;

        (azureVmSizeName, region) = await SelectFromRegions(vmSize, PreferredRegions); //get available vm from preferred regions

        if(region is not null && azureVmSizeName is not null) {
            await ClaimQuota(azureVmSizeName, region);
            return(azureVmSizeName, region, VMAvailability.AVAILABLE);
        }

        (azureVmSizeName, region) = await SelectFromRegions(vmSize, Quotas); //if failed to find region get from all regions

        if(region is not null && azureVmSizeName is not null) {
            await ClaimQuota(azureVmSizeName, region);
            return(azureVmSizeName, region, VMAvailability.PREFERREDREGIONFULL); //return preferred region full flag
        }

        return(null, null, VMAvailability.GLOBALFULL); //if failed to find any available vm return full flag
    }

    private Task<(string?, string?)> SelectFromRegions(VmSize vmSize, List<AzureRegion> Regions){
        var result = (
            from region in Regions
            where region.RegionalQuota >= vmSize.VCpuCount  // Enough regional quota available
            from vmCategory in region.VmCategories
            where vmCategory.Quota >= vmSize.VCpuCount      // Enough quota in the specific category
            join azureCategory in AzureVmData 
                on vmCategory.Category equals azureCategory.CategoryName
            from availableSize in azureCategory.AvailableSizes
            where availableSize.VCpuCount == vmSize.VCpuCount &&
                availableSize.GiBRamCount == vmSize.GiBRamCount
            select new 
            {
                region.Region,
                availableSize.AzureVmSizeName 
            })
            .FirstOrDefault();

        return Task.FromResult((result?.AzureVmSizeName,result?.Region));
    }

    public enum VMAvailability{
        AVAILABLE,
        PREFERREDREGIONFULL,
        GLOBALFULL,
        INVALID,
    }
}
