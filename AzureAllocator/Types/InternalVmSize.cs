namespace AzureAllocator.Types;

public class InternalVmSize(int vCpuCount, int giBRamCount)
{
    public int? VCpuCount = vCpuCount;
    public int? GiBRamCount = giBRamCount;
    public string? AzureVmSizeName;
}
