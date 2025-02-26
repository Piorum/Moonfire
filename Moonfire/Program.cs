using Moonfire.Types.Discord;
using Moonfire.Credit;

namespace Moonfire;

public class Program{
    
    //entry point
    public static async Task Main(){
        var CONFIG_PATH = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            nameof(Moonfire) //change in azure allocator program.cs
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

        var token = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN") ?? "";
        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.None | GatewayIntents.Guilds,
            LogLevel = LogSeverity.Info
        };

        //Discord Bot Commands
        //Need to match SlashCommandHandler switch cases to be caught
        //Choices need to match Game Enum in Bot.cs
        List<string>? _choices = ["SCP", "MINECRAFT"];
        var commands = new List<MoonfireCommand>{
            new("help", "Prints help information", MoonfireCommandRank.User),

            new("start", "#Admin - Provisions and starts game server", MoonfireCommandRank.Admin,
                [new(name: "game", description: "select game", isRequired: true, choices: _choices)]),
            new("stop", "#Admin - Deprovisions and stops game server", MoonfireCommandRank.Admin,
                [new(name: "game", description: "select game", isRequired: true, choices: _choices)]),
            new("configure", "#Admin - Opens game configuration dialogue", MoonfireCommandRank.Admin,
                [new(name: "game", description: "select game", isRequired: true, choices: _choices)]),
            new("region", "#Admin - Change preferred server region", MoonfireCommandRank.Admin),

            new("console", "#Owner - WIP", MoonfireCommandRank.Owner,
                [new(name: "input", description: "Input sent to the console", isRequired: true )]),
            new("repopulate", "#Owner - Refreshes bot commands", MoonfireCommandRank.Owner),
        };

        var _application = new Bot(token, config, commands);

        //top level exception sinks
        AppDomain.CurrentDomain.UnhandledException += (sender, args) => {
            Console.WriteLine($"[ERROR] AppDomain.CurrentDomain.UnhandledException:args.ExceptionObject:{args.ExceptionObject}");
        };

        TaskScheduler.UnobservedTaskException += (sender, args) => {
            Console.Out.WriteLine($"[ERROR] TaskScheduler.UnobservedTaskException:args.Exception:{args.Exception}");
            args.SetObserved(); //may prevent process termination?
        };

        try{
            await _application.StartBotAsync(); //discord bot start point

            //main loop to prevent exit
            while(true){

                //action credit every 180 seconds
                await CreditService.ActionCredit();

                await Task.Delay(180);

            }
            
        } catch (Exception e){
            await Console.Out.WriteLineAsync($"[ERROR] catch:_application.StartBotAsync:e:{e}");
        }

        await Console.Out.WriteLineAsync("Done.");
    }
}