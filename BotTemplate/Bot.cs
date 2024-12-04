using SCDisc.Utility;

namespace SCDisc;

public class Bot(string token, AzureVM vm, DiscordSocketConfig? config = null, List<Command>? _commands = null) : BotBase(token,config,_commands)
{
    private readonly SCPInterface _server = new(vm);

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

        // Checks if any user commands match name of caught command
        if(commands.Any(p => p.Name == command.Data.Name && p.Rank == Rank.User)){

            //User commands switch
            switch (command.Data.Name){
                case helpCmd:
                    run(
                        PrintHelpAsync(SendSlashReply));
                    break;
                default:
                    run(SendSlashReply($"Caught {command.Data.Name} by user but found no command"));
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
                    switch((string)command.Data.Options.First().Value){
                        case "1": //SCP
                            runTimed(
                                () => _server.StartServerAsync(vm),
                                "Starting",
                                () => $"Started at '{_server.PublicIp}'",
                                "You shouldn't be here - Bot.cs start command case",
                                elapsed => false);
                            break;
                        case "2": //GMOD
                            run(SendSlashReply("GMOD not available"));
                            break;
                        default:
                            run(SendSlashReply($"Caught {command.Data.Options.First().Value} by start command but found no game"));
                            break;
                    }
                    break;
                case "stop":
                    switch((string)command.Data.Options.First().Value){
                        case "1": //SCP
                            runTimed(
                                () => _server.StopServerAsync(vm),
                                "Stopping",
                                () => $"Stopping SCP Server'",
                                "You shouldn't be here - Bot.cs stop command case",
                                elapsed => false);
                            break;
                        case "2": //GMOD
                            run(SendSlashReply("GMOD not available"));
                            break;
                        default:
                            run(SendSlashReply($"Caught {command.Data.Options.First().Value} by stop command but found no game"));
                            break;
                    }
                    break;
                default:
                    run(SendSlashReply($"Caught {command.Data.Name} by admin but found no command"));
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
                case "console":
                    run(SendSlashReply("To-Do Add to SCP Process"));
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
                    run(SendSlashReply($"Caught {command.Data.Name} by owner but found no command"));
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
        await _SendMessage(help[(help.IndexOf('[')+1)..(help.LastIndexOf(']'))]);
    }
    
}