using Azure.Identity;
using Azure.ResourceManager;

namespace Moonfire;

public class Program{
    
    //entry point
    public static async Task Main()
    {
        //load .env
        var envPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Moonfire",
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
        List<string>? _choices = ["SCP", "GMOD"];
        var commands = new List<Command>{
            new("help", "Prints help information", Rank.User),
            new("start", "#Admin - Starts specified game server", Rank.Admin,
                [new(name: "game", description: "select game", isRequired: true, choices: _choices)]),
            new("stop", "#Admin - Stops VM and server", Rank.Admin,
                [new(name: "game", description: "select game", isRequired: true, choices: _choices)]),
            new("console", "#Owner - Sends input direct to Azure VM", Rank.Owner,
                [new(name: "input", description: "Input sent to the console", isRequired: true )]),
            new("repopulate", "#Owner - Refreshes bot commands", Rank.Owner),
            new("poweronazure", "#Owner - Starts Azure Virtual Machine", Rank.Owner),
            new("poweroffazure", "#Owner - Stops Azure Virtual Machine", Rank.Owner)
        };

        //creating ArmClient
        var clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID") ?? "";
        var clientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET") ?? "";
        var tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID") ?? "";
        var subscription = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID") ?? "";
        ClientSecretCredential credential = new(tenantId, clientId, clientSecret);
        ArmClient client = new(credential, subscription);

        //loading vm settings
        var templatePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Moonfire",
            "Config",
            "Template.json"
        );
        var settings = await AzureSettings.CreateAsync(templatePath);

        var vm = await AzureManager.Allocate(client, settings, "SCPTestRG", "SCPTestVM");
        if (vm != null)
        {
            var _application = new Bot(token, vm, config, commands);
            await _application.StartBotAsync();
        } else{
            await Console.Out.WriteLineAsync("Azure Provisioning Failed");
        }
    }
}