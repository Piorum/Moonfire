using Moonfire.Utility;
using Moonfire.Interfaces;
using Azure.ResourceManager;

namespace Moonfire;

public class Bot(string token, ArmClient azureClient, DiscordSocketConfig? config = null, List<Command>? _commands = null) : BotBase(token,config,_commands)
{
    private Dictionary<ulong, SCPInterface?> servers = [];

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
                    _ = run(
                        PrintHelpAsync(SendSlashReply));
                    break;
                default:
                    _ = run(SendSlashReply($"Caught {command.Data.Name} by user but found no command"));
                    break;
            }
            return Task.CompletedTask;
        }
        // Checks if any admin commands match name of caught command
        else if(commands.Any(p => p.Name == command.Data.Name && p.Rank == Rank.Admin)){
            //Check for admin perm (we could change this to another perm, or add more ranks and switches)
            if(!((SocketGuildUser)command.User).GuildPermissions.Administrator){
                _ = run(
                    SendSlashReply("You are not an admin"));
                return Task.CompletedTask;
            }
        
            //Admin commands switch
            switch(command.Data.Name){
                case "start":
                    switch((string)command.Data.Options.First().Value){
                        case "1": //SCP
                            var scpStartTask = Task.Run(async () => {
                                _ = SendSlashReply("Starting SCP Server");
                                var guid = command.GuildId ?? 0;
                                if(!servers.TryGetValue(guid,out var server)){
                                    server = await SCPInterface.CreateInterface(azureClient,$"{guid}");
                                    servers[guid] = server;
                                }
                                if(server==null){
                                    _ = ModifySlashReply("Azure Provisioning Failed");
                                    return;
                                }
                                await server.StartServerAsync(azureClient);
                                await ModifySlashReply($"Started Server at '{server.PublicIp}'");
                            });
                            _ = scpStartTask;
                            break;
                        case "2": //GMOD
                            _ = run(SendSlashReply("GMOD not available"));
                            break;
                        default:
                            _ = run(SendSlashReply($"Caught {command.Data.Options.First().Value} by start command but found no game"));
                            break;
                    }
                    break;
                case "stop":
                    switch((string)command.Data.Options.First().Value){
                        case "1": //SCP
                            var scpStopTask = Task.Run(async () => {
                                _ = SendSlashReply("Stopping SCP Server");
                                var guid = command.GuildId ?? 0;
                                if(!servers.TryGetValue(guid,out var server)){
                                    _ = ModifySlashReply("No Server Found");
                                    return;
                                }
                                if(server==null){
                                    _ = ModifySlashReply("Server Was Null");
                                    return;
                                }
                                await server.StopServerAsync();
                                servers[guid]=null;
                                _ = ModifySlashReply("Stopped Server");
                            });
                            _ = scpStopTask;
                            break;
                        case "2": //GMOD
                            _ = run(SendSlashReply("GMOD not available"));
                            break;
                        default:
                            _ = run(SendSlashReply($"Caught {command.Data.Options.First().Value} by stop command but found no game"));
                            break;
                    }
                    break;
                default:
                    _ = run(SendSlashReply($"Caught {command.Data.Name} by admin but found no command"));
                    break;
            }
            return Task.CompletedTask;
        }
        // Checks if any owner commands match name of caught command
        // These commands should only be for bot management
        else if (commands.Any(p => p.Name == command.Data.Name && p.Rank == Rank.Owner)){
            if(!(ownerId == command.User.Id)){
                _ = run(
                    SendSlashReply("You are not bot owner"));
                return Task.CompletedTask;
            }

            //Owner commands switch
            switch(command.Data.Name){
                case "repopulate":
                    _ = runTimed(
                        async () => {await UnregisterCommandsAsync(); await PopulateCommandsAsync(ownerServerId);},
                        "Starting Task",
                        "Commands Repopulated");
                    break;
                case "console":
                    _ = run(SendSlashReply("To-Do Add to SCP Process"));
                    break;
                default:
                    _ = run(SendSlashReply($"Caught {command.Data.Name} by owner but found no command"));
                    break;
            }
            return Task.CompletedTask;
        }
        else {
            _ = run(SendSlashReply("No switch captured command"));
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