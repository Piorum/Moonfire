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
            new("credit", "#Admin - Check available credit", MoonfireCommandRank.Admin),

            new("addcredit", "#Owner - Add amount/100 credit to a guild", MoonfireCommandRank.Owner,
                [new(name: "amount", description: "Amount/100 of credit to add", isRequired: true ),
                new(name: "guildId", description: "Id of guild to add credit to", isRequired: true)]),
            new("removecredit", "#Owner - Remove amount/100  credit from a guild", MoonfireCommandRank.Owner,
                [new(name: "amount", description: "Amount/100 of credit to remove", isRequired: true ),
                new(name: "guildId", description: "Id of guild to remove credit from", isRequired: true)]),
            new("checkcredit", "#Owner - Check credit of a guild", MoonfireCommandRank.Owner,
                [new(name: "guildId", description: "Id of guild to get credit from", isRequired: true)]),

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

                //action credit every loop
                try{
                    await CreditService.ActionCredit();

                } catch (Exception e) {
                    await Console.Out.WriteLineAsync($"[ERROR] \"ActioningCreditFailed\"\n{e}");
                }

                //3 minute delay to avoid excessive action 
                await Task.Delay(180 * 1000);
                
            }
            
        } catch (Exception e){
            await Console.Out.WriteLineAsync($"[ERROR] catch:_application.StartBotAsync:e:{e}");
        }

        await Console.Out.WriteLineAsync("Done.");
    }
}