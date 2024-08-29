using System.Collections.Immutable;
using SCDisc.Utility;

namespace SCDisc;

public class Bot(string t, DiscordSocketConfig? c = null) : BotBase(t,c)
{
    private const ulong ownerId = 208430103384948737; //<Discord UserId>, @piorum
    private const ulong ownerServerId = 581787007144165386; //<Discord ServerId>, --
    private static readonly ImmutableList<ulong> adminIds = [208430103384948737, 739176967064256542]; //<Discord UserIds>, @piorum, @crownsofstars
    private readonly SCPProcessInterface _server = new();

    protected override Task SlashCommandHandler(SocketSlashCommand command){
        //Local Functions
        //MessgeSenders/Formatters
        Task<EmbedBuilder> embedMessage (string a) {
            EmbedBuilder embed = new();
            embed.AddField($"**[{command.Data.Name.ToUpper()}]**",$"**```[{a}]```**");
            return Task.FromResult(embed);
        }
        async Task SendSlashReply (string a) =>
            await command.RespondAsync(" ", embed: (await embedMessage(a)).Build(), ephemeral: true);
        async Task ModifySlashReply (string a) =>
            await command.ModifyOriginalResponseAsync(async msg => msg.Embed = (await embedMessage(a)).Build());

        //Task Runners
        Task run(Task a){
            return Task.Run(async () => await a);
        }
        Task runTimed(Func<Task> a, object b, object c, object? d = default, Func<TimeSpan, bool>? e = default){
            return run(FuncExt.Time(SendSlashReply, ModifySlashReply, a, b, c, d, e));
        }

        //This has to match in 4 different places - here, switches, helpFunc, populateCmdsFunc
        //Need a better solution
        List<string> userCommands = [helpCmd];
        List<string> adminCommands = ["start", "stop", "console"];
        List<string> ownerCommands = ["repopulate"];

        if(userCommands.Contains(command.Data.Name)){

            //user commands switch
            switch (command.Data.Name){
                case helpCmd:
                    run(
                        PrintHelp(SendSlashReply));
                    break;
                default:
                    break;
            }
        }
        //Need to check for some server perm instead of ids
        else if(adminCommands.Contains(command.Data.Name)){
            if(!adminIds.Contains(command.User.Id)){
                run(
                    SendSlashReply("You are not an admin"));
                return Task.CompletedTask;
            }
        
            //Admin commands switch
            switch(command.Data.Name){
                case "start":
                    runTimed(
                        _server.StartServer,
                        "Starting",
                        () => $"Started @{_server.PublicIp}",
                        "Unusually fast, server started?",
                        elapsed => elapsed.Seconds < 1);
                    break;
                case "stop":
                    runTimed(
                        _server.StopServer,
                        "Stopping",
                        "Stopped",
                        "Unusually fast, server stopped?",
                        elapsed => elapsed.Milliseconds < 10);
                    break;
                case "console":
                    runTimed(
                        () => _server.SendConsoleInput((string)command.Data.Options.First().Value),
                        "Sending",
                        $"Sent \"{(string)command.Data.Options.First().Value}\"");
                    break;
                default:
                    break;
            }
        }
        //these commands should only be for bot management
        else if (ownerCommands.Contains(command.Data.Name)){
            if(!(ownerId == command.User.Id)){
                run(
                    SendSlashReply("You are not bot owner"));
                return Task.CompletedTask;
            }

            //Owner commands switch
            switch(command.Data.Name){
                case "repopulate":
                    runTimed(
                        async () => {await UnregisterCommands(); await PopulateCommands();},
                        "Starting Task",
                        "Commands Repopulated");
                    break;
                default:
                    break;
            }
        }
        run(SendSlashReply("No switch captured command"));
        return Task.CompletedTask;
    }

    private static async Task PrintHelp(Func<string, Task> _SendMessage){
        string help = //Leave out start/end bracket and end \n
             "start   - starts the server              #Admin]\n" +
            "[stop    - stops the server               #Admin]\n" +
            "[console - sends remaining args to server #Admin"    ;
        await _SendMessage(help);
    }
    
    //Better way to do this?
    private async Task PopulateCommands(){

        //Add all user/admin commands to all guilds
        foreach(var guild in _client.Guilds){
            await PopulateCommand(new("help", "Prints help information", guild));
            await PopulateCommand(new("start", "#Admin - Starts the server", guild));
            await PopulateCommand(new("stop", "#Admin - Stops the server", guild));
            await PopulateCommand(new("console", "#Admin - Sends input to the server process", guild),
                [new(name: "input", description: "Input sent to the console", isRequired: true)]);
        }

        //adds owner commands only to specified server
        var ownerGuild = _client.GetGuild(ownerServerId);
        await PopulateCommand(new("repopulate", "#Owner - Refreshes bot commands", ownerGuild));

        Console.WriteLine("All Commands Registered");
    }
    
}