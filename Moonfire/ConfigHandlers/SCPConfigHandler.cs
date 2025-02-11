using Moonfire.Types.Json;
using Newtonsoft.Json;

namespace Moonfire.ConfigHandlers;

public static class SCPConfigHandler
{
    public const string SCP_CONFIGS_TABLE_NAME = nameof(Moonfire) + "SCP";
    
    public static async Task<SCPSettings> GetGameSettings(string guildId, CancellationToken token = default){
        _ = Console.Out.WriteLineAsync($"{nameof(SCPConfigHandler)}:{nameof(GetGameSettings)}:{guildId}");
        var gameSettingsJson = await TableManager.GetTableEntity(SCP_CONFIGS_TABLE_NAME,guildId,"config","scpgame",token);

        SCPSettings? gameSettings;
        if(gameSettingsJson is null){
            _ = Console.Out.WriteLineAsync($"{nameof(SCPConfigHandler)}: No Game Settings Found For {guildId} Storing Defaults");

            //use template settings if none are found
            gameSettings = new();
            gameSettingsJson = JsonConvert.SerializeObject(gameSettings);

            await TableManager.StoreTableEntity(SCP_CONFIGS_TABLE_NAME,guildId,"config","scpgame",gameSettingsJson,token);
        } else {
            //deserialize json string into obj
            gameSettings = JsonConvert.DeserializeObject<SCPSettings>((string)gameSettingsJson);
            //ensure not null
            gameSettings ??= new();
        }

        //log settings retreived
        _ = Console.Out.WriteLineAsync($"{(string)gameSettingsJson}");
        return gameSettings;
    }

    public static async Task<SCPSettings> GetGameSettings(ulong? guildId, CancellationToken token = default) =>
        await GetGameSettings(guildId.ToString() ?? "", token);

    public static async Task SetGameSettings(string guildId, SCPSettings scpgameSettings, CancellationToken token = default) =>
        await TableManager.StoreTableEntity(SCP_CONFIGS_TABLE_NAME,guildId,"config","scpgame",JsonConvert.SerializeObject(scpgameSettings),token);

    public static async Task SetGameSettings(ulong? guildId, SCPSettings scpgameSettings, CancellationToken token = default) =>
        await SetGameSettings(guildId.ToString() ?? "", scpgameSettings, token);

    public static async Task<AzureSettings> GetHardwareSettings(string guildId, CancellationToken token = default){
        _ = Console.Out.WriteLineAsync($"{nameof(SCPConfigHandler)}:{nameof(GetHardwareSettings)}:{guildId}");
        var hardwareSettingsJsonTask = TableManager.GetTableEntity(SCP_CONFIGS_TABLE_NAME,guildId,"config","scphardware",token);
        var globalSettingsTask = GLOBALConfigHandler.GetSettings(guildId, token);

        await Task.WhenAll(hardwareSettingsJsonTask,globalSettingsTask);

        var hardwareSettingsJson = await hardwareSettingsJsonTask;
        var globalSettings = await globalSettingsTask;

        //if settings are null load and store template settings
        if(hardwareSettingsJson==null){
            _ = Console.Out.WriteLineAsync($"{nameof(SCPConfigHandler)}: No Hardware Settings Found For {guildId} Storing Defaults");

            //use template settings if none are found
            var hardwareTemplatePath = Path.Combine(
                Environment.GetEnvironmentVariable("CONFIG_PATH") ?? "",
                "Config",
                "SCPSettings.json"
            );
            hardwareSettingsJson = await File.ReadAllTextAsync(hardwareTemplatePath,token);

            await TableManager.StoreTableEntity(SCP_CONFIGS_TABLE_NAME,guildId,"config","scphardware",hardwareSettingsJson,token);
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

    public static async Task SetHardwareSettings(string guildId, AzureSettings scphardwareSettings, CancellationToken token = default) =>
        await TableManager.StoreTableEntity(SCP_CONFIGS_TABLE_NAME,guildId,"config","scphardware",JsonConvert.SerializeObject(scphardwareSettings),token);

    public static async Task SetHardwareSettings(ulong? guildId, AzureSettings scphardwareSettings, CancellationToken token = default) =>
        await SetHardwareSettings(guildId.ToString() ?? "", scphardwareSettings, token);

    public static async Task<bool> AssignRole(ulong? guildId, ulong SteamId, string roleName, CancellationToken token = default){
        var gameSettings = await GetGameSettings(guildId,token);

        //cap admins at 25 due to discord select menu limitations
        if(gameSettings.AdminUsers.Count is 25) return false;

        var existingUser = gameSettings.AdminUsers.Where(AdminUser => AdminUser.Id == SteamId).FirstOrDefault();
        if(existingUser is not null) gameSettings.AdminUsers.Remove(existingUser);

        gameSettings.AdminUsers.Add(new SCPSettings.AdminUser { Id = SteamId, Role = roleName } );
        await SetGameSettings(guildId,gameSettings,token);
        return true;
    }

    public static async Task RemoveRole(ulong? guildId, ulong? SteamId, CancellationToken token = default){
        var gameSettings = await GetGameSettings(guildId,token);

        var user = gameSettings.AdminUsers.Where(AdminUser => AdminUser.Id == SteamId).FirstOrDefault();

        if(user is not null){
            gameSettings.AdminUsers.Remove(user);
            await SetGameSettings(guildId,gameSettings,token);
        }
    }

    public static async Task SetBranch(ulong? guildId, SCPBranch branch, CancellationToken token = default){
        var gameSettings = await GetGameSettings(guildId,token);

        gameSettings.Branch = branch;

        await SetGameSettings(guildId,gameSettings,token);
    }

    public static async Task<bool> SetBranch(ulong? guildId, string branch, CancellationToken token = default){
        SCPBranch? Branch = branch switch {
            "public" => SCPBranch.PUBLIC,
            _ => null
        };
        if(Branch is null) return false;

        await SetBranch(guildId,(SCPBranch)Branch,token);
        return true;
    }

    public static async Task<bool> SetServerSize(ulong? guildId, string vmSize, CancellationToken token = default){
        var hardwareSettings = await GetHardwareSettings(guildId,token);

        var valid = await hardwareSettings.SetVmSize(vmSize);

        if(valid) await SetHardwareSettings(guildId,hardwareSettings,token);

        return valid;
    }
}
