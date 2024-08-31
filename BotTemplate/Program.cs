namespace SCDisc;

public class Program{
    
    //entry point
    public static async Task Main(){
        var token = File.ReadAllText("token.txt");
        var config = new DiscordSocketConfig{
            GatewayIntents = GatewayIntents.None | GatewayIntents.Guilds
        };

        //Need to match SlashCommandHandler switch cases to be caught
        var commands = new List<Command>{
            new("help", "Prints help information", Rank.User),
            new("start", "#Admin - Starts the server", Rank.Admin),
            new("stop", "#Admin - Stops the server", Rank.Admin),
            new("console", "#Admin - Sends input to the server process", Rank.Admin, 
                [new(name: "input", description: "Input sent to the console", isRequired: true )]),
            new("repopulate", "#Owner - Refreshes bot commands", Rank.Owner)
        };

        var _application = new Bot(token,config,commands);
        await _application.StartBotAsync();
    }
}