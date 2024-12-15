using Moonfire.Utility;
using Moonfire.Interfaces;
using Azure.ResourceManager;

namespace Moonfire;

public class Bot(string token, ArmClient azureClient, DiscordSocketConfig? config = null, List<Command>? _commands = null) : BotBase(token,config,_commands)
{
    private readonly Dictionary<ulong, SCPInterface?> servers = [];

    // Uncomment to do initial population of commands
    /*protected async override Task ClientReadyHandler(){
        await PopulateCommandsAsync(ownerServerId);
        return;
    }*/

    protected override Task SlashCommandHandler(SocketSlashCommand command){

        //finds first command that matches name of passed command
        //if a command was found gets the rank of that command
        _ = commands.FirstOrDefault(p => p.Name == command.Data.Name)?.Rank switch
        {
            Rank.User => _ = UserCommandHandler(command),

            Rank.Admin => _ = AdminCommandHandler(command),

            Rank.Owner => _ = OwnerCommandHandler(command),

            _ => _ = SendSlashReply("Failed to find command in commands list",command)
        };

        return Task.CompletedTask;
    }

    private Task UserCommandHandler(SocketSlashCommand command){
        _ = command.Data.Name switch
        {
            helpCmd => PrintHelpAsync(command),
                
            _ => SendSlashReply($"Caught {command.Data.Name} by user handler but found no command",command),
        };

        return Task.CompletedTask;
    }

    private Task AdminCommandHandler(SocketSlashCommand command){
        if(!((SocketGuildUser)command.User).GuildPermissions.Administrator){
            _ = SendSlashReply("You are not an admin",command);
            return Task.CompletedTask;
        }

        _ = Task.Run(() => command.Data.Name switch
        {
            "start" => StartCommandHandler(command),
            "stop" => StopCommandHandler(command),
            _ => SendSlashReply($"Caught {command.Data.Name} by admin handler but found no command",command),
        });

        return Task.CompletedTask;
    }

    private Task OwnerCommandHandler(SocketSlashCommand command){
        if(!(ownerId == command.User.Id)){
            _ = SendSlashReply("You are not bot owner",command);
            return Task.CompletedTask;
        }

        _ = command.Data.Name switch
        {
            "repopulate" => RepopulateTaskAsync(command),

            "console" => SendSlashReply("WIP",command),

            _ => SendSlashReply($"Caught {command.Data.Name} by owner handler but found no command",command),
        };

        return Task.CompletedTask;
    }

    private Task StartCommandHandler(SocketSlashCommand command){
        _ = (string)command.Data.Options.First().Value switch
        {
            //SCP
            "1" => StartScpTaskAsync(command),
            //GMOD
            "2" => SendSlashReply("GMOD not available",command),

            _ => SendSlashReply($"Caught {command.Data.Options.First().Value} by start command handler but found no game",command),
        };

        return Task.CompletedTask;
    }

    private Task StopCommandHandler(SocketSlashCommand command){
        _ = (string)command.Data.Options.First().Value switch
        {
            //SCP
            "1" => StopScpTaskAsync(command),
            //GMOD
            "2" => SendSlashReply("GMOD not available",command),

            _ => SendSlashReply($"Caught {command.Data.Options.First().Value} by stop command handler but found no game",command),
        };

        return Task.CompletedTask;
    }

    private async Task PrintHelpAsync(SocketSlashCommand command){
        //empty string in case no commands
        string help = "";
        //formats and get information for every command
        foreach(var cmd in commands.Where(p => p.Rank == Rank.User || p.Rank == Rank.Admin).ToList())
            help += $"[{cmd.Name} - {cmd.Description}]\n";
        //to reduce code this removes the first/last bracket and last newline to match expected formatting
        await SendSlashReply(help[(help.IndexOf('[')+1)..help.LastIndexOf(']')],command);
    }

    private async Task StartScpTaskAsync(SocketSlashCommand command){
        //ensure initial reply is sent first
        await SendSlashReply("Handling Command",command);
        //convert from ulong? to ulong
        var guid = command.GuildId ?? 0;
        //if no value, or value = null
        if(!servers.TryGetValue(guid,out var server) || servers[guid]==null){
            //set server and dictionary value to scpinterface object
            _ = ModifySlashReply("Provisioning Server",command);
            server = await SCPInterface.CreateInterface(azureClient,$"{guid}");
            servers[guid] = server;
        }
        //check if provisioning failed
        if(server==null){
            _ = ModifySlashReply("Azure Provisioning Failed",command);
            return;
        }
        //start server
        await server.StartServerAsync((string a)=>ModifySlashReply(a,command));
        await ModifySlashReply($"Started Server at '{server.PublicIp}'",command);
    }

    private async Task StopScpTaskAsync(SocketSlashCommand command){
        //ensure initial reply is sent first
        await SendSlashReply("Stopping SCP Server",command);
        var guid = command.GuildId ?? 0;
        if(!servers.TryGetValue(guid,out var server)){
            _ = ModifySlashReply("No Server Found",command);
            return;
        }
        if(server==null){
            _ = ModifySlashReply("Server Was Null",command);
            return;
        }
        await server.StopServerAsync((string a)=>ModifySlashReply(a,command));
        servers[guid]=null;
        _ = ModifySlashReply("Stopped Server",command);
    }

    private async Task RepopulateTaskAsync(SocketSlashCommand command){
        //ensure initial reply is sent first
        await SendSlashReply("Repopulating Commands",command);
        await UnregisterCommandsAsync();
        await PopulateCommandsAsync(ownerServerId);
        _ = ModifySlashReply("Commands Repopulated",command);
    }

    private static Task<EmbedBuilder> EmbedMessage(string input, SocketSlashCommand command){
        EmbedBuilder embed = new();
        embed.AddField($"**[{command.Data.Name.ToUpper()}]**",$"**```[{input}]```**");
        return Task.FromResult(embed);
    }

    private static async Task SendSlashReply(string input, SocketSlashCommand command)=>
        await command.RespondAsync(" ", embed: (await EmbedMessage(input,command)).Build(), ephemeral: true);

    private static async Task ModifySlashReply (string a, SocketSlashCommand command) =>
        await command.ModifyOriginalResponseAsync(async msg => msg.Embed = (await EmbedMessage(a,command)).Build());
}