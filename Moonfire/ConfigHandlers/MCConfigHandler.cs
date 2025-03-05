using Moonfire.Types.Json;
using AzureAllocator.Types;
using AzureAllocator.Managers;
using Newtonsoft.Json;

namespace Moonfire.ConfigHandlers;

public class MCConfigHandler
{
    public const string MC_CONFIGS_TABLE_NAME = nameof(Moonfire) + "MINECRAFT";

    public static Task<MCSettings> GetGameSettings(string guildId, CancellationToken token = default){
        throw new NotImplementedException();
    }
    public static async Task<MCSettings> GetGameSettings(ulong? guildId, CancellationToken token = default) =>
        await GetGameSettings(guildId.ToString() ?? "", token);

    public static async Task SetGameSettings(string guildId, MCSettings mcGameSettings, CancellationToken token = default) =>
        await TableManager.StoreTableEntity(MC_CONFIGS_TABLE_NAME,guildId,"config","mcgame",JsonConvert.SerializeObject(mcGameSettings),token);

    public static async Task SetGameSettings(ulong? guildId, MCSettings mcGameSettings, CancellationToken token = default) =>
        await SetGameSettings(guildId.ToString() ?? "", mcGameSettings, token);

    public static Task<AzureSettings> GetHardwareSettings(string guildId, CancellationToken token = default){
        throw new NotImplementedException();
    }
    public static async Task<AzureSettings> GetHardwareSettings(ulong? guildId, CancellationToken token = default) =>
        await GetHardwareSettings(guildId.ToString() ?? "", token);

    public static async Task SetHardwareSettings(string guildId, AzureSettings mcHardwareSettings, CancellationToken token = default) =>
        await TableManager.StoreTableEntity(MC_CONFIGS_TABLE_NAME,guildId,"config","mchardware",JsonConvert.SerializeObject(mcHardwareSettings),token);

    public static async Task SetHardwareSettings(ulong? guildId, AzureSettings mcHardwareSettings, CancellationToken token = default) =>
        await SetHardwareSettings(guildId.ToString() ?? "", mcHardwareSettings, token);

    
}
