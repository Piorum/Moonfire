using Moonfire.Types.Json;
using Newtonsoft.Json;
using AzureAllocator.Managers;

namespace Moonfire.ConfigHandlers;

public static class GLOBALConfigHandler
{
    public const string GLOBAL_CONFIGS_TABLE_NAME = nameof(Moonfire) + "GLOBAL";

    public static async Task<GLOBALSettings> GetSettings(string guildId, CancellationToken token = default){
        _ = Console.Out.WriteLineAsync($"{nameof(GLOBALConfigHandler)}:{nameof(GetSettings)}:{guildId}");
        var globalSettingsJson = await TableManager.GetTableEntity(GLOBAL_CONFIGS_TABLE_NAME,guildId,"config","settings",token);

        GLOBALSettings? globalSettings;
        if(globalSettingsJson is null){
            _ = Console.Out.WriteLineAsync($"{nameof(GLOBALConfigHandler)}: No Global Settings Found For {guildId} Storing Defaults");

            //use template settings if none are found
            globalSettings = new();
            globalSettingsJson = JsonConvert.SerializeObject(globalSettings);

            await TableManager.StoreTableEntity(GLOBAL_CONFIGS_TABLE_NAME,guildId,"config","settings",globalSettingsJson,token);
        } else {
            //deserialize json string into obj
            globalSettings = JsonConvert.DeserializeObject<GLOBALSettings>((string)globalSettingsJson);
            //ensure not null
            globalSettings ??= new();
        }

        //log settings retreived
        _ = Console.Out.WriteLineAsync($"{(string)globalSettingsJson}");
        return globalSettings;
    }

    public static async Task<GLOBALSettings> GetSettings(ulong? guildId, CancellationToken token = default) =>
        await GetSettings(guildId.ToString() ?? "", token);

    public static async Task SetSettings(string guildId, GLOBALSettings Settings, CancellationToken token = default) =>
        await TableManager.StoreTableEntity(GLOBAL_CONFIGS_TABLE_NAME,guildId,"config","settings",JsonConvert.SerializeObject(Settings),token);

    public static async Task SetSettings(ulong? guildId, GLOBALSettings Settings, CancellationToken token = default) =>
        await SetSettings(guildId.ToString() ?? "", Settings, token);

}
