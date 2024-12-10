namespace SCDisc;

public class Program{
    
    //entry point
    public static async Task Main(){
        //load .env
        //this is a terrible way to do this, old way of doing this broke???, old way (File.ReadAllLines(".env")))
        var envpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","..","..",".env");
        foreach(string line in File.ReadAllLines(envpath)){
            //From NAME="value" Env.Set(NAME,value,process)
            Environment.SetEnvironmentVariable(line[0..line.IndexOf('=')],line[(line.IndexOf('=')+2)..line.LastIndexOf('"')],EnvironmentVariableTarget.Process);
        }

        //strongly defined to avoid nullable string
        string token = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN") ?? "";
        var config = new DiscordSocketConfig{
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


        AzureVM vm = new("Ubuntu-Server-1","13.89.185.75","Moonfire-VM-1");
        var _application = new Bot(token,vm,config,commands);
        await _application.StartBotAsync();
    }
}