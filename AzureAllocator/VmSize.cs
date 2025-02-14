namespace AzureAllocator;

public class VmSize(int vCpuCount, int giBRamCount)
{
    public int? VCpuCount = vCpuCount;
    public int? GiBRamCount = giBRamCount;
    public string? AzureVmSizeName;

    public static Task<VmSize?> AzureNameToVmSize(string vmSize){
        VmSize? VmSize = vmSize switch{
            "Standard_B1s" => new(1,1),
            "Standard_B2s" => new(2,4),
            "Standard_B2ms" => new(2,8),
            "Standard_B4als_v2" => new(4,8),
            _ => null
        };

        return Task.FromResult(VmSize);
    }

    public Task<string> ToAzureName(){
        if(VCpuCount == 1 && GiBRamCount == 1){
            return Task.FromResult("Standard_B1s");
        }
        else if(VCpuCount == 2 && GiBRamCount == 4){
            return Task.FromResult("Standard_B2s");
        }
        else if(VCpuCount == 2 && GiBRamCount == 8){
            return Task.FromResult("Standard_B2ms");
        }
        else if(VCpuCount == 4 && GiBRamCount == 8){
            return Task.FromResult("Standard_B4als_v2");
        }

        return Task.FromResult("Standard_B2s");
    }
}
