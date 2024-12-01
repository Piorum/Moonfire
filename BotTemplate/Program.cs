namespace SCDisc;
using System.Collections;

public class Program{
    
    //entry point
    public static async Task Main(){
        //load .env
        foreach(string line in File.ReadAllLines(".env")){
            Environment.SetEnvironmentVariable(line[0..line.IndexOf('=')],line[(line.IndexOf('=')+2)..line.LastIndexOf('"')],EnvironmentVariableTarget.Process);
        }

        await AzureVM.StartVM();

        var token = File.ReadAllText("token.txt");
        var config = new DiscordSocketConfig{
            GatewayIntents = GatewayIntents.None | GatewayIntents.Guilds
        };

        //Discord Bot Commands
        //Need to match SlashCommandHandler switch cases to be caught
        var commands = new List<Command>{
            new("help", "Prints help information", Rank.User),
            new("start", "#Admin - Starts a server", Rank.Admin,
                [new(name: "game", description: "select game", isRequired: true, choices: ["SCP", "GMOD"])]),
            new("stop", "#Admin - Stops a server", Rank.Admin),
            new("console", "#Admin - Sends input to the server process", Rank.Admin, 
                [new(name: "input", description: "Input sent to the console", isRequired: true )]),
            new("repopulate", "#Owner - Refreshes bot commands", Rank.Owner)
        };

        var _application = new Bot(token,config,commands);
        await _application.StartBotAsync();
    }
}