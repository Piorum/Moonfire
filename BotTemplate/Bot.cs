using SCDisc.Utility;

namespace SCDisc;

public class Bot(string token, AzureVM _vm, DiscordSocketConfig? config = null, List<Command>? _commands = null) : BotBase(token,config,_commands)
{
    private readonly AzureVM vm = _vm;
    private readonly SCPProcessInterface _server = new();

    // Uncomment to do initial population of commands
    /*protected async override Task ClientReadyHandler(){
        await PopulateCommandsAsync(ownerServerId);
        return;
    }*/

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


        // Maybe we should move respective switch to their own functions? but... 
        // We will need to move all MessageSenders/Formatters and TaskRunners and pass command
        // Checks if any user commands match name of caught command
        if(commands.Any(p => p.Name == command.Data.Name && p.Rank == Rank.User)){

            //User commands switch
            switch (command.Data.Name){
                case helpCmd:
                    run(
                        PrintHelpAsync(SendSlashReply));
                    break;
                default:
                    break;
            }
            return Task.CompletedTask;
        }
        // Checks if any admin commands match name of caught command
        else if(commands.Any(p => p.Name == command.Data.Name && p.Rank == Rank.Admin)){
            //Check for admin perm (we could change this to another perm, or add more ranks and switches)
            if(!((SocketGuildUser)command.User).GuildPermissions.Administrator){
                run(
                    SendSlashReply("You are not an admin"));
                return Task.CompletedTask;
            }
        
            //Admin commands switch
            switch(command.Data.Name){
                case "start":
                    runTimed(
                        _server.StartServerAsync,
                        "Starting",
                        () => $"Started @{_server.PublicIp}",
                        "Unusually fast, server started?",
                        elapsed => elapsed.Seconds < 1);
                    break;
                case "stop":
                    runTimed(
                        _server.StopServerAsync,
                        "Stopping",
                        "Stopped",
                        "Unusually fast, server stopped?",
                        elapsed => elapsed.Milliseconds < 10);
                    break;
                case "console":
                    runTimed(
                        () => _server.SendConsoleInputAsync((string)command.Data.Options.First().Value),
                        "Sending",
                        $"Sent \"{(string)command.Data.Options.First().Value}\"");
                    break;
                default:
                    break;
            }
            return Task.CompletedTask;
        }
        // Checks if any owner commands match name of caught command
        // These commands should only be for bot management
        else if (commands.Any(p => p.Name == command.Data.Name && p.Rank == Rank.Owner)){
            if(!(ownerId == command.User.Id)){
                run(
                    SendSlashReply("You are not bot owner"));
                return Task.CompletedTask;
            }

            //Owner commands switch
            switch(command.Data.Name){
                case "repopulate":
                    runTimed(
                        async () => {await UnregisterCommandsAsync(); await PopulateCommandsAsync(ownerServerId);},
                        "Starting Task",
                        "Commands Repopulated");
                    break;
                case "poweronazure":
                    runTimed(
                        vm.Start,
                        "Starting Azure VM",
                        "Azure VM Started");
                    break;
                case "poweroffazure":
                    runTimed(
                        vm.Stop,
                        "Stopping Azure VM",
                        "Azure VM Stopped");
                    break;
                default:
                    break;
            }
            return Task.CompletedTask;
        }
        else {
            run(SendSlashReply("No switch captured command"));
            return Task.CompletedTask;
        }
    }

    private async Task PrintHelpAsync(Func<string, Task> _SendMessage){
        //empty string in case no commands
        string help = "";
        //formats and get information for every command
        foreach(var command in commands.Where(p => p.Rank == Rank.User || p.Rank == Rank.Admin).ToList())
            help += $"[{command.Name} - {command.Description}]\n";
        //to reduce code this removes the first/last bracket and last newline to match expected formatting
        await _SendMessage(help[(help.IndexOf('[')+1)..(help.LastIndexOf(']')-1)]);
    }
    
}