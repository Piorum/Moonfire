using Azure.Data.Tables;

namespace Sunfire;

public class Program{
    
    //entry point
    public static async Task Main(){
        var CONFIG_PATH = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            nameof(Sunfire) //change in azure allocator program.cs
        );
        Environment.SetEnvironmentVariable(nameof(CONFIG_PATH),CONFIG_PATH,EnvironmentVariableTarget.Process);

        //load .env
        var envPath = Path.Combine(
            Environment.GetEnvironmentVariable("CONFIG_PATH") ?? "",
            ".env"
        );
        foreach (string line in File.ReadAllLines(envPath))
        {
            //From NAME="value" Env.Set(NAME,value,process)
            if(line[0]=='#') continue;
            Environment.SetEnvironmentVariable(line[0..line.IndexOf('=')], line[(line.IndexOf('=') + 2)..line.LastIndexOf('"')], EnvironmentVariableTarget.Process);
        }

        //strongly defined to avoid nullable string
        var token = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN") ?? "";
        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.None | GatewayIntents.Guilds
        };

        //Discord Bot Commands
        //Need to match SlashCommandHandler switch cases to be caught
        //Choices need to match Game Enum in Bot.cs
        List<string>? _choices = ["SCP", "MINECRAFT"];
        var commands = new List<Command>{
            new("help", "Prints help information", Rank.User),
            new("start", "#Admin - Provisions and Starts Game Server", Rank.Admin,
                [new(name: "game", description: "select game", isRequired: true, choices: _choices)]),
            new("stop", "#Admin - Deprovisions and Stops Game Server", Rank.Admin,
                [new(name: "game", description: "select game", isRequired: true, choices: _choices)]),
            new("console", "#Owner - WIP", Rank.Owner,
                [new(name: "input", description: "Input sent to the console", isRequired: true )]),
            new("repopulate", "#Owner - Refreshes bot commands", Rank.Owner)
        };


        /*var client = await AzureManager.GetTableClient($"{nameof(Sunfire)}table");

        TableEntity var;
        var = new("guild1","scp")
        {
            { "LocalSettings", @"{LocalSettingsJsonString}"},

            { "RemoteSettings", @"{RemoteSettingsJsonString}"},
        };
        await AzureManager.StoreTableEntity(client,var);
        
        var = new("guild2","scp")
        {
            { "LocalSettings", @"{LocalSettingsJsonString2}"},

            { "RemoteSettings", @"{RemoteSettingsJsonString2}"},
        };
        await AzureManager.StoreTableEntity(client,var);

        var = new("guild1","scp")
        {
            { "LocalSettings", @"{NewLocalSettings}"}
        };
        await AzureManager.StoreTableEntity(client,var);

        var value = await AzureManager.GetTableEntity(client,"guild1","scp");
        var value2 = await AzureManager.GetTableEntity(client,"guild2","scp");
        _ = Console.Out.WriteLineAsync($"{value?["LocalSettings"]} and {value2?["RemoteSettings"]}");
        await Task.Delay(-1);*/

        var _application = new Bot(token, config, commands);
        await _application.StartBotAsync();
    }
}