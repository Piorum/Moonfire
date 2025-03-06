using Moonfire.Types.Json;
using AzureAllocator.Types;
using AzureAllocator.Managers;
using Newtonsoft.Json;

namespace Moonfire.ConfigHandlers;

public class MCConfigHandler
{
    public const string MC_CONFIGS_TABLE_NAME = nameof(Moonfire) + "MINECRAFT";

    public static async Task<MCSettings> GetGameSettings(string guildId, CancellationToken token = default){
        _ = Console.Out.WriteLineAsync($"{nameof(MCConfigHandler)}:{nameof(GetGameSettings)}:{guildId}");
        var gameSettingsJson = await TableManager.GetTableEntity(MC_CONFIGS_TABLE_NAME,guildId,"config","game",token);

        MCSettings? gameSettings;
        if(gameSettingsJson is null){
            _ = Console.Out.WriteLineAsync($"{nameof(MCConfigHandler)}: No Game Settings Found For {guildId} Storing Defaults");

            //use template settings if none are found
            gameSettings = new();
            gameSettingsJson = JsonConvert.SerializeObject(gameSettings);

            await TableManager.StoreTableEntity(MC_CONFIGS_TABLE_NAME,guildId,"config","game",gameSettingsJson,token);
        } else {
            //deserialize json string into obj
            gameSettings = JsonConvert.DeserializeObject<MCSettings>((string)gameSettingsJson);
            //ensure not null
            gameSettings ??= new();
        }

        //log settings retreived
        _ = Console.Out.WriteLineAsync($"{(string)gameSettingsJson}");
        return gameSettings;
    }
    public static async Task<MCSettings> GetGameSettings(ulong? guildId, CancellationToken token = default) =>
        await GetGameSettings(guildId.ToString() ?? "", token);

    public static async Task SetGameSettings(string guildId, MCSettings mcGameSettings, CancellationToken token = default) =>
        await TableManager.StoreTableEntity(MC_CONFIGS_TABLE_NAME,guildId,"config","game",JsonConvert.SerializeObject(mcGameSettings),token);

    public static async Task SetGameSettings(ulong? guildId, MCSettings mcGameSettings, CancellationToken token = default) =>
        await SetGameSettings(guildId.ToString() ?? "", mcGameSettings, token);

    public static async Task<AzureSettings> GetHardwareSettings(string guildId, CancellationToken token = default){
        _ = Console.Out.WriteLineAsync($"{nameof(MCConfigHandler)}:{nameof(GetHardwareSettings)}:{guildId}");
        var hardwareSettingsJsonTask = TableManager.GetTableEntity(MC_CONFIGS_TABLE_NAME,guildId,"config","hardware",token);
        var globalSettingsTask = GLOBALConfigHandler.GetSettings(guildId, token);

        await Task.WhenAll(hardwareSettingsJsonTask,globalSettingsTask);

        var hardwareSettingsJson = await hardwareSettingsJsonTask;
        var globalSettings = await globalSettingsTask;

        //if settings are null load and store template settings
        if(hardwareSettingsJson==null){
            _ = Console.Out.WriteLineAsync($"{nameof(MCConfigHandler)}: No Hardware Settings Found For {guildId} Storing Defaults");

            //use template settings if none are found
            var hardwareTemplatePath = Path.Combine(
                Environment.GetEnvironmentVariable("CONFIG_PATH") ?? "",
                "Config",
                "MCSettings.json"
            );
            hardwareSettingsJson = await File.ReadAllTextAsync(hardwareTemplatePath,token);

            await TableManager.StoreTableEntity(MC_CONFIGS_TABLE_NAME,guildId,"config","hardware",hardwareSettingsJson,token);
        }

        //log settings retreived
        _ = Console.Out.WriteLineAsync($"{(string)hardwareSettingsJson}");
        //create azure settings object
        var hardwareSettings = await AzureSettings.CreateAsync((string)hardwareSettingsJson);

        hardwareSettings.Region = globalSettings.region;

        return hardwareSettings;
    }
    public static async Task<AzureSettings> GetHardwareSettings(ulong? guildId, CancellationToken token = default) =>
        await GetHardwareSettings(guildId.ToString() ?? "", token);

    public static async Task SetHardwareSettings(string guildId, AzureSettings mcHardwareSettings, CancellationToken token = default) =>
        await TableManager.StoreTableEntity(MC_CONFIGS_TABLE_NAME,guildId,"config","hardware",JsonConvert.SerializeObject(mcHardwareSettings),token);

    public static async Task SetHardwareSettings(ulong? guildId, AzureSettings mcHardwareSettings, CancellationToken token = default) =>
        await SetHardwareSettings(guildId.ToString() ?? "", mcHardwareSettings, token);

    
}
